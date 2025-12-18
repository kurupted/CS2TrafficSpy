using Colossal;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Input;
using Game.Net;
using Game.Pathfind;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Game.Vehicles;
using System;
using System.Collections.Generic;
using TrafficSpy.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Diagnostics;
using Entity = Unity.Entities.Entity;

namespace TrafficSpy.Systems
{
    public enum TrafficType
    {
        Citizen,
        Cargo,
        PublicTransport,
        Service
    }

    public struct TrafficRenderData
    {
        public Entity entity;
        public Game.Citizens.Purpose purpose;
        public TrafficType type;
        public bool isOrigin;

        public bool isVehicle;
        public bool isPedestrian;
        public bool isDestination;
        public bool isMovingIn;
    }

    [UpdateAfter(typeof(ToolSystem))]
    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem toolSystem;
        private DefaultToolSystem defaultToolSystem;

        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;
        private ValueBinding<bool> highlightAgentsBinding;
        private ValueBinding<bool> showPedestriansBinding;
        private ValueBinding<bool> showVehiclesBinding;
        private ValueBinding<bool> showRoutesBinding;
        private ValueBinding<int> directionModeBinding;

        private bool highlightAgents = false;
        private bool showPedestrians = false;
        private bool showVehicles = true;
        public static List<Entity> AnalyzedLanes = new List<Entity>();
        public bool HighlightAgents => highlightAgents;
        public bool ShowRoutes { get; private set; } = false;
        private int directionMode = 0; // 0 = Both, 1 = Side A (Fwd), 2 = Side B (Bwd) 

        private bool isToolActive = false;
        private bool defaultDebugSelectState = false;
        private bool usePathBasedAnalysis = true;

        private bool wasToggleKeyDown = false;

        private List<TrafficRenderData> allAnalysisResults = new List<TrafficRenderData>();
        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        public static bool IsDirty = false;

        private string currentFilter = "";

        private Entity lastSelectedEntity = Entity.Null;
        private EntityQuery pathOwnerQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_InfoUISystem.AddMiddleSection(this);

            this.toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            this.defaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();

            this.pathOwnerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.activityDataBinding = new ValueBinding<string>("TrafficSpy", "activityData", "{}");
            this.toolActiveBinding = new ValueBinding<bool>("TrafficSpy", "toolActive", false);
            this.highlightAgentsBinding = new ValueBinding<bool>("TrafficSpy", "highlightAgents", false);
            this.showPedestriansBinding = new ValueBinding<bool>("TrafficSpy", "showPedestrians", false);
            this.showVehiclesBinding = new ValueBinding<bool>("TrafficSpy", "showVehicles", true);
            this.showRoutesBinding = new ValueBinding<bool>("TrafficSpy", "showRoutes", false);
            this.directionModeBinding = new ValueBinding<int>("TrafficSpy", "directionMode", 0);

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);
            AddBinding(this.highlightAgentsBinding);
            AddBinding(this.showPedestriansBinding);
            AddBinding(this.showVehiclesBinding);
            AddBinding(this.showRoutesBinding);
            AddBinding(this.directionModeBinding);

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "sethighlightAgents", (bool active) => {
                this.highlightAgents = active;
                this.highlightAgentsBinding.Update(active);
                ApplyFilter();
            }));

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setShowPedestrians", (bool active) => {
                this.showPedestrians = active;
                this.showPedestriansBinding.Update(active);
                CalculateStats();
                ApplyFilter();
            }));

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setShowVehicles", (bool active) => {
                this.showVehicles = active;
                this.showVehiclesBinding.Update(active);
                CalculateStats();
                ApplyFilter();
            }));
            
            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setShowRoutes", (bool active) => {
                this.ShowRoutes = active;
                this.showRoutesBinding.Update(active);
                // We set IsDirty to true so the RouteSystem knows to check immediately
                IsDirty = true; 
                ApplyFilter();
            }));

            // Handler for setting direction mode
            AddBinding(new TriggerBinding<int>("TrafficSpy", "setDirectionMode", (int mode) => {
                this.directionMode = mode;
                this.directionModeBinding.Update(mode);
                if (lastSelectedEntity != Entity.Null)
                {
                    RunAnalysis(lastSelectedEntity);
                }
            }));

            AddBinding(new TriggerBinding<string>("TrafficSpy", "setTrafficFilter", (string filter) => {
                try
                {
                    if (filter == "RESET" || string.IsNullOrEmpty(filter))
                    {
                        this.currentFilter = "";
                    }
                    else if (this.currentFilter == filter)
                    {
                        this.currentFilter = "";
                    }
                    else
                    {
                        this.currentFilter = filter;
                    }

                    Mod.log.Info($"TrafficSpy: Filter set to '{(string.IsNullOrEmpty(currentFilter) ? "ALL" : currentFilter)}'");
                    ApplyFilter();
                }
                catch (Exception ex)
                {
                    Mod.log.Error($"TrafficSpy: Error setting filter: {ex.Message}");
                }
            }));

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setToolActive", SetToolActive));
        }

        protected override string group => "TrafficSpy.Systems.TrafficUISystem";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }

        protected bool ShouldBeVisible(Entity entity)
        {
            return EntityManager.Exists(entity)
                && EntityManager.HasBuffer<SubLane>(entity)
                && !EntityManager.HasComponent<Building>(entity)
                // Allow either Road Segments (Edges) OR Intersections (Nodes)
                && (EntityManager.HasComponent<Game.Net.Edge>(entity) || EntityManager.HasComponent<Game.Net.Node>(entity));
        }

        protected override void OnUpdate()
        {
            if (!Enabled) Enabled = true;

            // 1. CHECK KEYBOARD INPUT
            if (Mod.m_ToggleAction != null)
            {
                bool isPressed = Mod.m_ToggleAction.IsPressed();
                // Only trigger if pressed NOW but wasn't pressed LAST frame
                if (isPressed && !wasToggleKeyDown)
                {
                    SetToolActive(!isToolActive);
                }
                wasToggleKeyDown = isPressed;
            }

            base.OnUpdate();

            Entity selected = this.toolSystem.selected;

            if (ShouldBeVisible(selected))
            {
                this.visible = true;
            }
            else
            {
                this.visible = false;
                ClearData();
                return;
            }

            if (selected != lastSelectedEntity)
            {
                lastSelectedEntity = selected;
                currentFilter = "";

                // Reset direction to All Sides (0)
                this.directionMode = 0;
                this.directionModeBinding.Update(0);

                Mod.log.Info($"TrafficSpy: Selection changed to {selected.Index}. Running analysis...");
                RunAnalysis(selected);
            }
        }

        private void SetToolActive(bool active)
        {
            this.isToolActive = active;
            this.toolActiveBinding.Update(active);

            if (active)
            {
                if (defaultToolSystem != null)
                {
                    this.defaultDebugSelectState = this.defaultToolSystem.debugSelect;
                    this.defaultToolSystem.debugSelect = true;
                }
            }
            else
            {
                if (defaultToolSystem != null)
                {
                    this.defaultToolSystem.debugSelect = this.defaultDebugSelectState;
                }
                ClearData();
            }
        }

        private void ClearData()
        {
            lastSelectedEntity = Entity.Null;
            currentFilter = "";
            if (this.activityDataBinding.value != "{}")
            {
                this.activityDataBinding.Update("{}");
                allAnalysisResults.Clear();
                CurrentRenderList.Clear();
                AnalyzedLanes.Clear(); // Clear lanes
                IsDirty = true;
            }
        }

        private void ApplyFilter()
        {
            CurrentRenderList.Clear();

            if (allAnalysisResults == null) return;

            foreach (var item in allAnalysisResults)
            {
                if (item.isPedestrian && !this.showPedestrians) continue;
                if (item.isVehicle && !this.showVehicles) continue;

                bool matchesFilter = false;
                if (string.IsNullOrEmpty(currentFilter))
                {
                    matchesFilter = true;
                }
                else
                {
                    matchesFilter = MatchesFilter(item, currentFilter);
                }

                if (!matchesFilter) continue;

                if (item.isDestination)
                {
                    CurrentRenderList.Add(item);
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentFilter) || this.highlightAgents || this.ShowRoutes)
                    {
                        CurrentRenderList.Add(item);
                    }
                }
            }

            IsDirty = true;
        }
        
        

        private bool MatchesFilter(TrafficRenderData item, string filter)
        {
            switch (filter)
            {
                case "none": return item.purpose == Purpose.None && item.type != TrafficType.Service && item.type != TrafficType.Cargo && item.type != TrafficType.PublicTransport;
                case "shopping": return item.purpose == Purpose.Shopping;
                case "leisure": return item.purpose == Purpose.Leisure || item.purpose == Purpose.Relaxing || item.purpose == Purpose.Sleeping || item.purpose == Purpose.WaitingHome;
                case "goingHome": return item.purpose == Purpose.GoingHome;
                case "goingToWork": return item.purpose == Purpose.GoingToWork || item.purpose == Purpose.Working;
                case "movingIn": return item.isMovingIn;
                case "movingAway": return item.purpose == Purpose.MovingAway;
                case "school": return item.purpose == Purpose.GoingToSchool || item.purpose == Purpose.Studying;

                case "transporting":
                    return (item.type == TrafficType.Cargo && item.purpose == Purpose.Delivery) ||
                           (item.type == TrafficType.Citizen && (item.purpose == Purpose.Delivery || item.purpose == Purpose.Exporting || item.purpose == Purpose.UpkeepDelivery || item.purpose == Purpose.StorageTransfer || item.purpose == Purpose.Collect || item.purpose == Purpose.CompanyShopping));

                case "returning":
                    return item.type == TrafficType.Cargo && item.purpose == Purpose.None;

                case "tourism": return item.purpose == Purpose.Sightseeing || item.purpose == Purpose.Traveling || item.purpose == Purpose.VisitAttractions;

                case "services":
                    return item.type == TrafficType.Service ||
                           item.purpose == Purpose.Hospital || item.purpose == Purpose.InHospital ||
                           item.purpose == Purpose.Deathcare || item.purpose == Purpose.ReturnGarbage ||
                           item.purpose == Purpose.InDeathcare || item.purpose == Purpose.ReturnUnsortedMail ||
                           item.purpose == Purpose.ReturnLocalMail || item.purpose == Purpose.ReturnOutgoingMail || item.purpose == Purpose.SendMail;

                case "other":
                    if (item.type == TrafficType.Cargo || item.type == TrafficType.Service) return false;
                    if (item.type == TrafficType.PublicTransport) return true;

                    return item.type == TrafficType.Citizen &&
                           item.purpose != Purpose.None &&
                           item.purpose != Purpose.Shopping &&
                           !(item.purpose == Purpose.Leisure || item.purpose == Purpose.Relaxing || item.purpose == Purpose.Sleeping || item.purpose == Purpose.WaitingHome) &&
                           item.purpose != Purpose.GoingHome &&
                           !(item.purpose == Purpose.GoingToWork || item.purpose == Purpose.Working) &&
                           item.purpose != Purpose.MovingAway &&
                           !(item.purpose == Purpose.GoingToSchool || item.purpose == Purpose.Studying) &&
                           !(item.purpose == Purpose.Sightseeing || item.purpose == Purpose.Traveling || item.purpose == Purpose.VisitAttractions) &&
                           !(item.purpose == Purpose.Delivery || item.purpose == Purpose.Exporting || item.purpose == Purpose.UpkeepDelivery || item.purpose == Purpose.StorageTransfer || item.purpose == Purpose.Collect || item.purpose == Purpose.CompanyShopping) &&
                           !(item.purpose == Purpose.Hospital || item.purpose == Purpose.InHospital || item.purpose == Purpose.Deathcare || item.purpose == Purpose.ReturnGarbage || item.purpose == Purpose.InDeathcare || item.purpose == Purpose.ReturnUnsortedMail || item.purpose == Purpose.ReturnLocalMail || item.purpose == Purpose.ReturnOutgoingMail || item.purpose == Purpose.SendMail);

                default: return false;
            }
        }
        
        private NativeHashSet<Entity> GetTargetEntities(Entity segment, Allocator allocator, int directionMode)
        {
            NativeHashSet<Entity> targets = new NativeHashSet<Entity>(16, allocator);
            targets.Add(segment);

            // 1. Get the main road segment's geometry
            if (EntityManager.HasComponent<Curve>(segment) && 
                EntityManager.TryGetBuffer(segment, true, out DynamicBuffer<SubLane> lanes))
            {
                Curve segmentCurve = EntityManager.GetComponentData<Curve>(segment);
                
                // Calculate the "Center" and "Right Vector" of the road at the midpoint (t=0.5)
                // Position(0.5)
                float3 segmentPos = Colossal.Mathematics.MathUtils.Position(segmentCurve.m_Bezier, 0.5f);
                // Tangent(0.5) gives the forward direction
                float3 segmentTan = Colossal.Mathematics.MathUtils.Tangent(segmentCurve.m_Bezier, 0.5f);
                // Cross product with Up (0,1,0) gives the Right Vector
                float3 segmentRight = math.cross(segmentTan, new float3(0, 1, 0));

                for (int i = 0; i < lanes.Length; i++)
                {
                    Entity subLaneEntity = lanes[i].m_SubLane;
                    
                    if (directionMode != 0)
                    {
                        // We check the geometry of the sub-lane
                        if (EntityManager.HasComponent<Curve>(subLaneEntity))
                        {
                            Curve laneCurve = EntityManager.GetComponentData<Curve>(subLaneEntity);
                            float3 lanePos = Colossal.Mathematics.MathUtils.Position(laneCurve.m_Bezier, 0.5f);

                            // Calculate vector from Road Center -> Lane Center
                            float3 diff = lanePos - segmentPos;
                            
                            // Dot product determines side:
                            // > 0 means the lane is on the Right side of the road center
                            // < 0 means the lane is on the Left side of the road center
                            float dot = math.dot(diff, segmentRight);

                            // Threshold 0.1f ignores tiny floating point errors for exact center lanes
                            
                            // Mode 1 (Side A): We want ONE side (e.g. Left). So skip if dot is Positive (Right).
                            if (directionMode == 1 && dot > 0.1f) continue;

                            // Mode 2 (Side B): We want OTHER side (e.g. Right). So skip if dot is Negative (Left).
                            if (directionMode == 2 && dot < -0.1f) continue;
                        }
                    }

                    targets.Add(subLaneEntity);
                }
            }
            return targets;
        }

        private void CalculateStats()
        {
            int cntNone = 0;
            int cntShopping = 0;
            int cntLeisure = 0;
            int cntGoingHome = 0;
            int cntGoingToWork = 0;
            int cntMovingIn = 0;
            int cntMovingAway = 0;
            int cntSchool = 0;
            int cntTransporting = 0;
            int cntReturning = 0;
            int cntTourism = 0;
            int cntOther = 0;
            int cntServices = 0;

            foreach (var item in allAnalysisResults)
            {
                if (item.isDestination) continue;
                if (item.isPedestrian && !this.showPedestrians) continue;
                if (item.isVehicle && !this.showVehicles) continue;

                if (item.type == TrafficType.Service)
                {
                    cntServices++;
                    continue;
                }

                if (item.type == TrafficType.PublicTransport)
                {
                    cntOther++;
                    continue;
                }

                if (item.type == TrafficType.Cargo)
                {
                    if (item.purpose == Purpose.Delivery) cntTransporting++;
                    else cntReturning++;
                    continue;
                }

                switch (item.purpose)
                {
                    case Purpose.None: cntNone++; break;
                    case Purpose.Shopping: cntShopping++; break;
                    case Purpose.Leisure:
                    case Purpose.Sleeping:
                    case Purpose.WaitingHome:
                    case Purpose.Relaxing: cntLeisure++; break;
                    case Purpose.GoingHome:
                        if (item.isMovingIn) cntMovingIn++;
                        else cntGoingHome++;
                        break;
                    case Purpose.GoingToWork:
                    case Purpose.Working: cntGoingToWork++; break;
                    case Purpose.MovingAway: cntMovingAway++; break;
                    case Purpose.GoingToSchool:
                    case Purpose.Studying: cntSchool++; break;
                    case Purpose.Sightseeing:
                    case Purpose.Traveling:
                    case Purpose.VisitAttractions: cntTourism++; break;
                    case Purpose.Delivery:
                    case Purpose.Exporting:
                    case Purpose.UpkeepDelivery:
                    case Purpose.StorageTransfer:
                    case Purpose.Collect:
                    case Purpose.CompanyShopping: cntTransporting++; break;
                    case Purpose.ReturnGarbage:
                    case Purpose.Deathcare:
                    case Purpose.InDeathcare:
                    case Purpose.ReturnUnsortedMail:
                    case Purpose.ReturnLocalMail:
                    case Purpose.ReturnOutgoingMail:
                    case Purpose.SendMail:
                    case Purpose.Hospital:
                    case Purpose.InHospital: cntServices++; break;
                    default: cntOther++; break;
                }
            }

            string json = $@"{{
                ""none"": {cntNone},
                ""shopping"": {cntShopping},
                ""leisure"": {cntLeisure},
                ""goingHome"": {cntGoingHome},
                ""goingToWork"": {cntGoingToWork},
                ""movingIn"": {cntMovingIn},
                ""movingAway"": {cntMovingAway},
                ""school"": {cntSchool},
                ""transporting"": {cntTransporting},
                ""returning"": {cntReturning},
                ""tourism"": {cntTourism},
                ""other"": {cntOther},
                ""services"": {cntServices}
            }}";

            this.activityDataBinding.Update(json);
        }

        private void RunAnalysis(Entity selectedSegment)
        {
            if (usePathBasedAnalysis)
            {
                Mod.log.Info("TrafficSpy: Starting Path-Based Analysis");
                NativeQueue<TrafficRenderData> resultsQueue = new NativeQueue<TrafficRenderData>(Allocator.TempJob);
                
                // Pass directionMode to GetTargetEntities
                NativeHashSet<Entity> targets = GetTargetEntities(selectedSegment, Allocator.TempJob, this.directionMode);

                AnalyzedLanes.Clear();

                // highlight the road segment or its specific lanes
                if (this.directionMode != 0)
                {
                    NativeArray<Entity> targetArray = targets.ToNativeArray(Allocator.Temp);
                    foreach (var entity in targetArray)
                    {
                        if (entity != selectedSegment) AnalyzedLanes.Add(entity);
                    }
                    targetArray.Dispose();
                }
                else
                {
                    AnalyzedLanes.Add(selectedSegment);
                }


                PathActivityJob pathJob = new PathActivityJob
                {
                    targets = targets,
                    travelPurposeLookup = SystemAPI.GetComponentLookup<TravelPurpose>(true),
                    targetLookup = SystemAPI.GetComponentLookup<Target>(true),
                    householdLookup = SystemAPI.GetComponentLookup<Household>(true),
                    householdMemberLookup = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                    workerLookup = SystemAPI.GetComponentLookup<Worker>(true),
                    studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                    creatureResidentLookup = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(true),
                    propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    ownerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                    buildingLookup = SystemAPI.GetComponentLookup<Building>(true),
                    currentVehicleLookup = SystemAPI.GetComponentLookup<CurrentVehicle>(true),
                    currentTransportLookup = SystemAPI.GetComponentLookup<CurrentTransport>(true),
                    taxiLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Taxi>(true),
                    passengerLookup = SystemAPI.GetBufferLookup<Passenger>(true),

                    deliveryTruckLookup = SystemAPI.GetComponentLookup<DeliveryTruck>(true),
                    cargoTransportLookup = SystemAPI.GetComponentLookup<CargoTransport>(true),
                    publicTransportLookup = SystemAPI.GetComponentLookup<PublicTransport>(true),
                    personalCarLookup = SystemAPI.GetComponentLookup<PersonalCar>(true),

                    hearseLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Hearse>(true),
                    garbageTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.GarbageTruck>(true),
                    policeCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PoliceCar>(true),
                    fireEngineLookup = SystemAPI.GetComponentLookup<Game.Vehicles.FireEngine>(true),
                    ambulanceLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                    postVanLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PostVan>(true),
                    maintenanceVehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.MaintenanceVehicle>(true),

                    results = resultsQueue.AsParallelWriter()
                };

                pathJob.ScheduleParallel(pathOwnerQuery, default).Complete();

                allAnalysisResults.Clear();
                int queueCount = 0;
                while (resultsQueue.TryDequeue(out TrafficRenderData item))
                {
                    allAnalysisResults.Add(item);
                    queueCount++;
                }

                Mod.log.Info($"TrafficSpy: Queue had {queueCount} items");

                targets.Dispose();
                resultsQueue.Dispose();
            }
            /*else
            {
                // Fallback for SegmentActivityJob
                SegmentActivityJob segmentJob = new SegmentActivityJob
                {
                    selectedSegment = selectedSegment,
                    directionFilter = this.directionMode,
                    edgeLaneLookup = SystemAPI.GetComponentLookup<EdgeLane>(true),
                    // ... (rest of your lookups) ...
                };
                // segmentJob.Run();
            }*/

            CalculateStats();
            ApplyFilter();
        }
    }
}
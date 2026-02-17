using Colossal;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Input;
using Game.Net;
using Game.Objects;
//using Game.Prefabs; // for GrayWorld
//using System.Reflection; // for GrayWorld
using Game.Routes;
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
        public Entity sourceAgent; // The agent responsible for this entry (for distance checks)
        public Game.Citizens.Purpose purpose;
        public TrafficType type;
        public bool isOrigin;

        public bool isVehicle;
        public bool isPedestrian;
        public bool isDestination;
        public bool isMovingIn;
        public bool isTourist;
    }

    [UpdateAfter(typeof(ToolSystem))]
    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem toolSystem;
        private DefaultToolSystem defaultToolSystem;
        private TrafficSpyToolSystem trafficSpyToolSystem;
        private bool _isSpyModeActive = false;

        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;
        private ValueBinding<bool> highlightAgentsBinding;
        private ValueBinding<int> displayModeBinding; // 0 = Vehicles, 1 = Pedestrians
        private ValueBinding<bool> showRoutesBinding;
        private ValueBinding<int> directionModeBinding;
        private ValueBinding<int> rangeModeBinding; // 0=Short, 1=Med, 2=Long, 3=Unlimited

        private bool highlightAgents = false;
        private int displayMode = 0; // Default to Vehicles
        
        public static List<Entity> AnalyzedLanes = new List<Entity>();
        public bool HighlightAgents => highlightAgents;
        public bool ShowRoutes { get; private set; } = true; // Default to True
        private int directionMode = 0; // 0 = Both, 1 = Side A (Fwd), 2 = Side B (Bwd) 
        private int rangeMode = 1; // Default to Medium

        // Statics for the Route System to read
        public static float3 FilterPosition = float3.zero;
        public static float FilterDistance = 3000f; // Default Medium

        private bool isToolActive = false;
        private bool usePathBasedAnalysis = true;

        private bool wasToggleKeyDown = false;

        private List<TrafficRenderData> allAnalysisResults = new List<TrafficRenderData>();
        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        public static bool IsDirty = false;

        private string currentFilter = "";

        private Entity lastSelectedEntity = Entity.Null;
        private EntityQuery pathOwnerQuery;
        
        // Structures for Stop Selection
        private struct StopOption
        {
            public Entity Entity;
            public string Name;
        }
        private List<StopOption> m_AssociatedStops = new List<StopOption>();
        private EntityQuery m_AllStopsQuery;
        
        // for 'gray world'
        /*private EntityQuery infoviewQuery;
        private Entity fakeInfoviewEntity = Entity.Null;
        private ValueBinding<bool> grayWorldBinding;
        private bool grayWorldEnabled = false;*/
        

        protected override void OnCreate()
        {
            base.OnCreate();
            m_InfoUISystem.AddMiddleSection(this);

            this.toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            this.defaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            this.trafficSpyToolSystem = World.GetOrCreateSystemManaged<TrafficSpyToolSystem>();
            
            // Listen for tool changes
            this.toolSystem.EventToolChanged += OnToolChanged;
            
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
            
            this.displayModeBinding = new ValueBinding<int>("TrafficSpy", "displayMode", 0);
            
            this.showRoutesBinding = new ValueBinding<bool>("TrafficSpy", "showRoutes", true);
            this.directionModeBinding = new ValueBinding<int>("TrafficSpy", "directionMode", 0);
            this.rangeModeBinding = new ValueBinding<int>("TrafficSpy", "rangeMode", 1);
            
            // NEW: Query to find all stops
            m_AllStopsQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Routes.TransportStop>(),
                ComponentType.ReadOnly<Game.Routes.Connected>()
            );

            // NEW: Binding to send stops to UI
            AddBinding(new GetterValueBinding<string>("trafficSpy", "associatedStops", () => 
            {
                var sb = new System.Text.StringBuilder("[");
                foreach(var stop in m_AssociatedStops)
                {
                    // We use Index/Version which is safer for UI
                    sb.Append($"{{\"index\":{stop.Entity.Index}, \"version\":{stop.Entity.Version}, \"name\":\"{stop.Name}\"}},");
                }
                if (m_AssociatedStops.Count > 0) sb.Length--; 
                sb.Append("]");
                return sb.ToString();
            }));

            // NEW: Binding to let UI select a stop
            AddBinding(new TriggerBinding<Entity>("trafficSpy", "selectStop", (entity) => {
                toolSystem.selected = entity;
            }));

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);
            AddBinding(this.highlightAgentsBinding);
            AddBinding(this.displayModeBinding);
            AddBinding(this.showRoutesBinding);
            AddBinding(this.directionModeBinding);
            AddBinding(this.rangeModeBinding);

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "sethighlightAgents", (bool active) => {
                this.highlightAgents = active;
                this.highlightAgentsBinding.Update(active);
                ApplyFilter();
            }));

            AddBinding(new TriggerBinding<int>("TrafficSpy", "setDisplayMode", (int mode) => {
                this.displayMode = mode;
                this.displayModeBinding.Update(mode);
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

            // Handler for setting range mode
            AddBinding(new TriggerBinding<int>("TrafficSpy", "setRangeMode", (int mode) => {
                this.rangeMode = mode;
                this.rangeModeBinding.Update(mode);
                UpdateRangeDistance();
                CalculateStats();
                ApplyFilter();
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
            
            /*this.grayWorldBinding = new ValueBinding<bool>("TrafficSpy", "grayWorld", false);
            AddBinding(this.grayWorldBinding);
            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setGrayWorld", (bool active) => {
                this.grayWorldEnabled = active;
                this.grayWorldBinding.Update(active);
                if (this.isToolActive)
                {
                    ToggleGrayWorld(active);
                }
            }));*/

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setToolActive", SetToolActive));
        }

        protected override string group => "TrafficSpy.Systems.TrafficUISystem";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }

        protected bool ShouldBeVisible(Entity entity)
        {
            return EntityManager.Exists(entity)
                && EntityManager.HasBuffer<Game.Net.SubLane>(entity)
                && !EntityManager.HasComponent<Building>(entity)
                // Allow either Road Segments (Edges) OR Intersections (Nodes)
                && (EntityManager.HasComponent<Game.Net.Edge>(entity) || EntityManager.HasComponent<Game.Net.Node>(entity));
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (this.toolSystem != null)
            {
                this.toolSystem.EventToolChanged -= OnToolChanged;
            }
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
            
            // Auto-Reactivate Tool
            if (_isSpyModeActive)
            {
                // If we are currently in Default Tool (viewing info or idle)
                if (toolSystem.activeTool == defaultToolSystem)
                {
                    // And nothing is selected (User just pressed Esc to close Info Panel)
                    if (toolSystem.selected == Entity.Null)
                    {
                        // Reactivate the Spy Tool immediately
                        trafficSpyToolSystem.Enable();
                    }
                }
            }

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
                
                // NEW: Find stops attached to this road/station
                FindAssociatedStops(selected);

                Mod.log.Info($"TrafficSpy: Selection changed to {selected.Index}. Running analysis...");
                RunAnalysis(selected);
            }
        }
        
        private void OnToolChanged(ToolBaseSystem newTool)
        {
            // If we are in Spy Mode...
            if (_isSpyModeActive)
            {
                // ...and the user switches to a tool that is NEITHER TrafficSpy NOR Default...
                if (newTool != trafficSpyToolSystem && newTool != defaultToolSystem)
                {
                    // ...then they probably clicked the Bulldozer or Road tool. Turn off Spy Mode.
                    SetSpyMode(false);
                }
            }
        }

        public void SetSpyMode(bool active)
        {
            if (_isSpyModeActive != active)
            {
                _isSpyModeActive = active;
                this.isToolActive = active;
                this.toolActiveBinding.Update(active);

                if (active)
                {
                    trafficSpyToolSystem.Enable();
                }
                else
                {
                    // If disabling, ensure we switch back to default tool if we were spying
                    if (toolSystem.activeTool == trafficSpyToolSystem)
                    {
                        trafficSpyToolSystem.Disable();
                    }
                    ClearData();
                }
            }
        }
        
        private void SetToolActive(bool active) => SetSpyMode(active);
        public void SyncToolState(bool active) => SetSpyMode(active);
        
        private void ClearData()
        {
            lastSelectedEntity = Entity.Null;
            currentFilter = "";
            FilterPosition = float3.zero;
            if (this.activityDataBinding.value != "{}")
            {
                this.activityDataBinding.Update("{}");
                allAnalysisResults.Clear();
                CurrentRenderList.Clear();
                AnalyzedLanes.Clear(); // Clear lanes
                IsDirty = true;
            }
        }

        private void UpdateRangeDistance()
        {
            switch (rangeMode)
            {
                case 0: FilterDistance = 1000f; break; // Short (1km)
                case 1: FilterDistance = 3000f; break; // Medium (3km)
                case 2: FilterDistance = 10000f; break; // Long (10km)
                case 3: FilterDistance = float.MaxValue; break; // Unlimited
                default: FilterDistance = 3000f; break;
            }
        }
        
        private bool IsItemVisible(TrafficRenderData item, ComponentLookup<Transform> transformLookup)
        {
            // 1. Display Mode Check
            if (this.displayMode == 0 && !item.isVehicle) return false;
            if (this.displayMode == 1 && !item.isPedestrian) return false;

            // 2. Range Check
            if (FilterDistance < 1000000f)
            {
                Entity entityToCheck = item.sourceAgent != Entity.Null ? item.sourceAgent : item.entity;
                if (transformLookup.TryGetComponent(entityToCheck, out Transform trans))
                {
                    if (math.distancesq(trans.m_Position, FilterPosition) > (FilterDistance * FilterDistance))
                        return false;
                }
            }
            return true;
        }

        private void ApplyFilter()
        {
            CurrentRenderList.Clear();

            if (allAnalysisResults == null) return;

            ComponentLookup<Transform> transformLookup = SystemAPI.GetComponentLookup<Transform>(true);

            foreach (var item in allAnalysisResults)
            {
                // Use centralized visibility check
                if (!IsItemVisible(item, transformLookup)) continue;

                bool matchesFilter = false;
                if (string.IsNullOrEmpty(currentFilter))
                    matchesFilter = true;
                else
                    matchesFilter = MatchesFilter(item, currentFilter);

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
                case "goingHome": return item.purpose == Purpose.GoingHome && !item.isTourist;
                case "goingToWork": return item.purpose == Purpose.GoingToWork || item.purpose == Purpose.Working;
                case "movingIn": return item.isMovingIn;
                case "movingAway": return item.purpose == Purpose.MovingAway && !item.isTourist;
                case "school": return item.purpose == Purpose.GoingToSchool || item.purpose == Purpose.Studying;

                case "transporting":
                    return (item.type == TrafficType.Cargo && item.purpose == Purpose.Delivery) ||
                           (item.type == TrafficType.Citizen && (item.purpose == Purpose.Delivery || item.purpose == Purpose.Exporting || item.purpose == Purpose.UpkeepDelivery || item.purpose == Purpose.StorageTransfer || item.purpose == Purpose.Collect || item.purpose == Purpose.CompanyShopping));

                case "returning":
                    return item.type == TrafficType.Cargo && item.purpose == Purpose.None;

                case "tourism": 
                    // Includes standard tourism purposes OR Tourists going home (leaving city)
                    return item.purpose == Purpose.Sightseeing || item.purpose == Purpose.Traveling || item.purpose == Purpose.VisitAttractions || ((item.purpose == Purpose.GoingHome || item.purpose == Purpose.MovingAway) && item.isTourist);  

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
            
            // 1. HANDLE TRANSPORT STOPS (Runs when you click the "Bus Stop" button in UI)
            if (EntityManager.HasComponent<Game.Routes.TransportStop>(segment))
            {
                // Use HasComponent + GetComponentData (TryGetComponent doesn't work for data components)
                if (EntityManager.HasComponent<Game.Routes.Connected>(segment))
                {
                    Game.Routes.Connected connected = EntityManager.GetComponentData<Game.Routes.Connected>(segment);
                    Entity mainLane = connected.m_Connected;
                    targets.Add(mainLane);

                    // Find Sibling Lanes (Platforms/Sidewalks)
                    if (EntityManager.HasComponent<Owner>(mainLane))
                    {
                        Owner laneOwner = EntityManager.GetComponentData<Owner>(mainLane);
                        Entity edgeEntity = laneOwner.m_Owner;
            
                        // Add the Edge
                        targets.Add(edgeEntity);

                        // Add all SubLanes (Platform/Sidewalk)
                        if (EntityManager.TryGetBuffer(edgeEntity, true, out DynamicBuffer<Game.Net.SubLane> subLanes))
                        {
                            foreach (var subLane in subLanes)
                            {
                                targets.Add(subLane.m_SubLane);
                            }
                        }
                    }
                }
                return targets;
            }

            // 1. Get the main road segment's geometry
            if (EntityManager.HasComponent<Curve>(segment) && 
                EntityManager.TryGetBuffer(segment, true, out DynamicBuffer<Game.Net.SubLane> lanes))
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
        
        
        private void FindAssociatedStops(Entity selected)
        {
            m_AssociatedStops.Clear();
            if (selected == Entity.Null) return;

            // Efficiently grab all stops in the city
            var stopEntities = m_AllStopsQuery.ToEntityArray(Allocator.Temp);
            var connectedComps = m_AllStopsQuery.ToComponentDataArray<Game.Routes.Connected>(Allocator.Temp);

            for (int i = 0; i < stopEntities.Length; i++)
            {
                Entity stopEntity = stopEntities[i];
                Entity laneEntity = connectedComps[i].m_Connected;
                bool isMatch = false;

                // CHECK 1: Is the Stop connected to a Lane owned by the Selection? (Roads/Tracks)
                if (EntityManager.HasComponent<Owner>(laneEntity))
                {
                    if (EntityManager.GetComponentData<Owner>(laneEntity).m_Owner == selected)
                        isMatch = true;
                }

                // CHECK 2: Is the Stop directly owned by the Selection? (Stations/Buildings)
                if (!isMatch && EntityManager.HasComponent<Owner>(stopEntity))
                {
                    if (EntityManager.GetComponentData<Owner>(stopEntity).m_Owner == selected)
                        isMatch = true;
                }

                if (isMatch)
                {
                    // Give it a nice name
                    string typeName = "Stop";
                    if (EntityManager.HasComponent<Game.Net.TrainTrack>(laneEntity)) typeName = "Platform";
                    else if (EntityManager.HasComponent<Game.Net.SubwayTrack>(laneEntity)) typeName = "Subway";
                    else if (EntityManager.HasComponent<Game.Net.PedestrianLane>(laneEntity)) typeName = "Bus/Tram";

                    m_AssociatedStops.Add(new StopOption { 
                        Entity = stopEntity, 
                        Name = $"{typeName} {m_AssociatedStops.Count + 1}" 
                    });
                }
            }
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

            ComponentLookup<Transform> transformLookup = SystemAPI.GetComponentLookup<Transform>(true);

            foreach (var item in allAnalysisResults)
            {
                if (item.isDestination) continue;
                
                // Use centralized visibility check (respects Range and DisplayMode)
                if (!IsItemVisible(item, transformLookup)) continue;

                if (item.type == TrafficType.Service) { cntServices++; continue; }
                if (item.type == TrafficType.PublicTransport) { cntOther++; continue; }
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
                        else if (item.isTourist) cntTourism++; // Tourists leaving counted as Tourism
                        else cntGoingHome++;
                        break;
                    case Purpose.GoingToWork:
                    case Purpose.Working: cntGoingToWork++; break;
                    case Purpose.MovingAway:
                        if (item.isTourist) cntTourism++; 
                        else cntMovingAway++; 
                        break;
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
                // Calculate Center Position for Range Filtering
                if (EntityManager.HasComponent<Curve>(selectedSegment))
                {
                    Curve curve = EntityManager.GetComponentData<Curve>(selectedSegment);
                    FilterPosition = Colossal.Mathematics.MathUtils.Position(curve.m_Bezier, 0.5f);
                }
                else
                {
                    FilterPosition = float3.zero;
                }
                
                UpdateRangeDistance();

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
                    touristHouseholdLookup = SystemAPI.GetComponentLookup<TouristHousehold>(true), 
                    propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    ownerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                    buildingLookup = SystemAPI.GetComponentLookup<Building>(true),
                    currentVehicleLookup = SystemAPI.GetComponentLookup<CurrentVehicle>(true),
                    currentTransportLookup = SystemAPI.GetComponentLookup<CurrentTransport>(true),
                    taxiLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Taxi>(true),
                    passengerLookup = SystemAPI.GetBufferLookup<Passenger>(true),

                    deliveryTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true),
                    cargoTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.CargoTransport>(true),
                    publicTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PublicTransport>(true),
                    personalCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(true),

                    hearseLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Hearse>(true),
                    garbageTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.GarbageTruck>(true),
                    policeCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PoliceCar>(true),
                    fireEngineLookup = SystemAPI.GetComponentLookup<Game.Vehicles.FireEngine>(true),
                    ambulanceLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                    postVanLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PostVan>(true),
                    maintenanceVehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.MaintenanceVehicle>(true),
                    
                    // for those on the selected segment
                    carLaneLookup = SystemAPI.GetComponentLookup<CarCurrentLane>(true),
                    humanLaneLookup = SystemAPI.GetComponentLookup<HumanCurrentLane>(true),

                    results = resultsQueue.AsParallelWriter()
                };

                pathJob.ScheduleParallel(pathOwnerQuery, default).Complete();

                allAnalysisResults.Clear();
                while (resultsQueue.TryDequeue(out TrafficRenderData item))
                {
                    allAnalysisResults.Add(item);
                }

                targets.Dispose();
                resultsQueue.Dispose();
            }
            
            CalculateStats();
            ApplyFilter();
        }
        
        
        /*private void ToggleGrayWorld(bool active)
        {
            if (active)
            {
                // Create the Fake Infoview if it doesn't exist
                if (fakeInfoviewEntity == Entity.Null)
                {
                    var entities = infoviewQuery.ToEntityArray(Allocator.Temp);
                    var prefabs = SystemAPI.GetComponentLookup<PrefabData>(true);
                    var infos = SystemAPI.GetComponentLookup<InfoviewData>(true);
                    
                    Entity sourceTrafficView = Entity.Null;
                    foreach (var e in entities)
                    {
                        if (prefabs.TryGetComponent(e, out PrefabData pData))
                        {
                            // Use the inherited m_PrefabSystem
                            var prefabBase = m_PrefabSystem.GetPrefab<PrefabBase>(pData);
                            if (prefabBase != null && prefabBase.name == "Traffic")
                            {
                                sourceTrafficView = e;
                                break;
                            }
                        }
                    }
                    
                    if (sourceTrafficView != Entity.Null)
                    {
                        fakeInfoviewEntity = EntityManager.CreateEntity();
                        
                        // 1. Add Visual Settings (Gray World)
                        EntityManager.AddComponentData(fakeInfoviewEntity, infos[sourceTrafficView]);
                        
                        // 2. [FIX] Add Prefab Data (Enables Selection/Raycasting)
                        // This tells the ToolSystem "We are the Traffic View" so it allows selecting roads,
                        // but since we exclude InfoviewNetStatusData, it won't draw the green/red overlay.
                        EntityManager.AddComponentData(fakeInfoviewEntity, prefabs[sourceTrafficView]);
                    }
                    entities.Dispose();
                }

                // Activate it
                if (fakeInfoviewEntity != Entity.Null)
                {
                    ForceSetActiveInfoview(fakeInfoviewEntity);
                }
            }
            else
            {
                // Deactivate
                ForceSetActiveInfoview(Entity.Null);
                
                if (fakeInfoviewEntity != Entity.Null)
                {
                    EntityManager.DestroyEntity(fakeInfoviewEntity);
                    fakeInfoviewEntity = Entity.Null;
                }
            }
        }

        // HELPER METHOD (Reflects into ToolSystem to set the read-only property)
        private void ForceSetActiveInfoview(Entity entity)
        {
            // Try setting via Property (if private setter exists)
            var prop = typeof(ToolSystem).GetProperty("activeInfoview");
            var setter = prop?.GetSetMethod(true);
            if (setter != null)
            {
                setter.Invoke(toolSystem, new object[] { entity });
            }
            else
            {
                // Fallback: Set the backing field directly (usually 'm_ActiveInfoview')
                var field = typeof(ToolSystem).GetField("m_ActiveInfoview", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(toolSystem, entity);
                }
            }
        }*/
    }
}
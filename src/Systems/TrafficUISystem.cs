using Colossal;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
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
    }

    [UpdateAfter(typeof(ToolSystem))]
    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem toolSystem;
        private DefaultToolSystem defaultToolSystem;

        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;
        private ValueBinding<bool> showAllVehiclesBinding;
        private bool showAllVehicles = false;

        private bool isToolActive = false;
        private bool defaultDebugSelectState = false;
        private bool usePathBasedAnalysis = true;

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
            this.showAllVehiclesBinding = new ValueBinding<bool>("TrafficSpy", "showAllVehicles", false);

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);
            AddBinding(this.showAllVehiclesBinding);

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setShowAllVehicles", (bool active) => {
                this.showAllVehicles = active;
                this.showAllVehiclesBinding.Update(active);
                ApplyFilter(); // Re-apply filter immediately
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

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setToolActive", (bool active) => {
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
            }));
        }

        protected override string group => "TrafficSpy.Systems.TrafficUISystem";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }

        protected bool ShouldBeVisible(Entity entity)
        {
            return EntityManager.Exists(entity) && EntityManager.HasBuffer<SubLane>(entity);
        }

        protected override void OnUpdate()
        {
            if (!Enabled) Enabled = true;
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
                Mod.log.Info($"TrafficSpy: Selection changed to {selected.Index}. Running analysis...");
                RunAnalysis(selected);
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
                IsDirty = true;
            }
        }

        private void ApplyFilter()
        {
            CurrentRenderList.Clear();

            if (allAnalysisResults == null) return;

            if (string.IsNullOrEmpty(currentFilter))
            {
                // DEFAULT: Show Destinations only, Hide Vehicles
                foreach (var item in allAnalysisResults)
                {
                    if (!item.isVehicle)
                    {
                        // Always show destinations
                        CurrentRenderList.Add(item);
                    }
                    else if (this.showAllVehicles) // NEW CHECK
                    {
                        // Show vehicles if toggle is ON
                        CurrentRenderList.Add(item);
                    }
                }
            }
            else
            {
                // FILTERED: Show Destinations AND Vehicles matching filter
                foreach (var item in allAnalysisResults)
                {
                    if (MatchesFilter(item, currentFilter))
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
                case "movingIn": return item.purpose == Purpose.MovingAway && item.type == TrafficType.Citizen;
                case "movingAway": return item.purpose == Purpose.MovingAway;
                case "school": return item.purpose == Purpose.GoingToSchool || item.purpose == Purpose.Studying;

                // Fix 2: Explicitly check for Cargo Type and the Purpose tag set in the Job
                case "transporting":
                    // Cargo vehicle marked as transporting (Purpose.Delivery from Job), or a Citizen purpose related to transport
                    return (item.type == TrafficType.Cargo && item.purpose == Purpose.Delivery) ||
                           (item.type == TrafficType.Citizen && (item.purpose == Purpose.Delivery || item.purpose == Purpose.Exporting || item.purpose == Purpose.UpkeepDelivery || item.purpose == Purpose.StorageTransfer || item.purpose == Purpose.Collect || item.purpose == Purpose.CompanyShopping));

                case "returning":
                    // Cargo vehicle marked as returning (Purpose.None from Job)
                    return item.type == TrafficType.Cargo && item.purpose == Purpose.None;

                case "tourism": return item.purpose == Purpose.Sightseeing || item.purpose == Purpose.Traveling || item.purpose == Purpose.VisitAttractions;

                case "services":
                    // Service Vehicles OR Citizen services purpose
                    return item.type == TrafficType.Service ||
                           item.purpose == Purpose.Hospital || item.purpose == Purpose.InHospital ||
                           item.purpose == Purpose.Deathcare || item.purpose == Purpose.ReturnGarbage ||
                           item.purpose == Purpose.InDeathcare || item.purpose == Purpose.ReturnUnsortedMail ||
                           item.purpose == Purpose.ReturnLocalMail || item.purpose == Purpose.ReturnOutgoingMail || item.purpose == Purpose.SendMail;

                // Fix 3: Strict catch-all for anything not explicitly covered (Public Transport + truly 'other' citizen purposes)
                case "other":
                    // Exclude types handled by dedicated filters
                    if (item.type == TrafficType.Cargo || item.type == TrafficType.Service) return false;

                    // Public Transport is counted as 'other' in the job
                    if (item.type == TrafficType.PublicTransport) return true;

                    // Exclude all specific citizen purposes that have their own filter (including sub-purposes)
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

        private NativeHashSet<Entity> GetTargetEntities(Entity segment, Allocator allocator)
        {
            NativeHashSet<Entity> targets = new NativeHashSet<Entity>(16, allocator);
            targets.Add(segment);
            if (EntityManager.TryGetBuffer(segment, true, out DynamicBuffer<SubLane> lanes))
            {
                for (int i = 0; i < lanes.Length; i++)
                    targets.Add(lanes[i].m_SubLane);
            }
            return targets;
        }

        private void RunAnalysis(Entity selectedSegment)
        {
            NativeCounter cntNone = new NativeCounter(Allocator.TempJob);
            NativeCounter cntShopping = new NativeCounter(Allocator.TempJob);
            NativeCounter cntLeisure = new NativeCounter(Allocator.TempJob);
            NativeCounter cntGoingHome = new NativeCounter(Allocator.TempJob);
            NativeCounter cntGoingToWork = new NativeCounter(Allocator.TempJob);
            NativeCounter cntMovingIn = new NativeCounter(Allocator.TempJob);
            NativeCounter cntMovingAway = new NativeCounter(Allocator.TempJob);
            NativeCounter cntSchool = new NativeCounter(Allocator.TempJob);
            NativeCounter cntTransporting = new NativeCounter(Allocator.TempJob);
            NativeCounter cntReturning = new NativeCounter(Allocator.TempJob);
            NativeCounter cntTourism = new NativeCounter(Allocator.TempJob);
            NativeCounter cntOther = new NativeCounter(Allocator.TempJob);
            NativeCounter cntServices = new NativeCounter(Allocator.TempJob);

            if (usePathBasedAnalysis)
            {
                Mod.log.Info("TrafficSpy: Starting Path-Based Analysis");
                NativeQueue<TrafficRenderData> resultsQueue = new NativeQueue<TrafficRenderData>(Allocator.TempJob);
                NativeHashSet<Entity> targets = GetTargetEntities(selectedSegment, Allocator.TempJob);

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

                    cntNone = cntNone.ToConcurrent(),
                    cntShopping = cntShopping.ToConcurrent(),
                    cntLeisure = cntLeisure.ToConcurrent(),
                    cntGoingHome = cntGoingHome.ToConcurrent(),
                    cntGoingToWork = cntGoingToWork.ToConcurrent(),
                    cntMovingIn = cntMovingIn.ToConcurrent(),
                    cntMovingAway = cntMovingAway.ToConcurrent(),
                    cntSchool = cntSchool.ToConcurrent(),
                    cntTransporting = cntTransporting.ToConcurrent(),
                    cntReturning = cntReturning.ToConcurrent(),
                    cntTourism = cntTourism.ToConcurrent(),
                    cntOther = cntOther.ToConcurrent(),
                    cntServices = cntServices.ToConcurrent(),

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

            ApplyFilter();

            string json = $@"{{
                ""none"": {cntNone.Count},
                ""shopping"": {cntShopping.Count},
                ""leisure"": {cntLeisure.Count},
                ""goingHome"": {cntGoingHome.Count},
                ""goingToWork"": {cntGoingToWork.Count},
                ""movingIn"": {cntMovingIn.Count},
                ""movingAway"": {cntMovingAway.Count},
                ""school"": {cntSchool.Count},
                ""transporting"": {cntTransporting.Count},
                ""returning"": {cntReturning.Count},
                ""tourism"": {cntTourism.Count},
                ""other"": {cntOther.Count},
                ""services"": {cntServices.Count}
            }}";

            this.activityDataBinding.Update(json);

            cntNone.Dispose();
            cntShopping.Dispose();
            cntLeisure.Dispose();
            cntGoingHome.Dispose();
            cntGoingToWork.Dispose();
            cntMovingIn.Dispose();
            cntMovingAway.Dispose();
            cntSchool.Dispose();
            cntTransporting.Dispose();
            cntReturning.Dispose();
            cntTourism.Dispose();
            cntOther.Dispose();
            cntServices.Dispose();
        }
    }
}
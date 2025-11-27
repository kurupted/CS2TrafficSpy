using Colossal;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
//using Game.Objects;
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
    }

    [UpdateAfter(typeof(ToolSystem))]
    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem toolSystem;
        private DefaultToolSystem defaultToolSystem;

        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;

        private bool isToolActive = false;
        private bool defaultDebugSelectState = false;
        private bool usePathBasedAnalysis = true;

        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        public static bool IsDirty = false;

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

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setToolActive", (active) => {
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
                Mod.log.Info($"TrafficSpy: Selection changed to {selected.Index}. Running analysis...");
                RunAnalysis(selected);
            }
        }

        private void ClearData()
        {
            lastSelectedEntity = Entity.Null;
            if (this.activityDataBinding.value != "{}")
            {
                this.activityDataBinding.Update("{}");
                CurrentRenderList.Clear();
                IsDirty = true;
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
                //Debug
                //NativeQueue<DebugEntityInfo> debugQueue = new NativeQueue<DebugEntityInfo>(Allocator.TempJob);

                PathActivityJob pathJob = new PathActivityJob
                {

                    /* Debug
                    debugInfo = debugQueue.AsParallelWriter(),
                    personalCarLookup = SystemAPI.GetComponentLookup<PersonalCar>(true),
                    controllerLookup = SystemAPI.GetComponentLookup<Controller>(true),
                    unspawnedLookup = SystemAPI.GetComponentLookup<Unspawned>(true),
                    petLookup = SystemAPI.GetComponentLookup<Game.Creatures.Pet>(true),
                    wildlifeLookup = SystemAPI.GetComponentLookup<Game.Creatures.Wildlife>(true),
                    citizenLookup = SystemAPI.GetComponentLookup<Game.Citizens.Citizen>(true),
                    deletedLookup = SystemAPI.GetComponentLookup<Deleted>(true),
                    tempLookup = SystemAPI.GetComponentLookup<Temp>(true),
                    */

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

                CurrentRenderList.Clear();
                int queueCount = 0;
                while (resultsQueue.TryDequeue(out TrafficRenderData item))
                {
                    CurrentRenderList.Add(item);
                    queueCount++;
                }

                Mod.log.Info($"TrafficSpy: Queue had {queueCount} items");
                Mod.log.Info($"TrafficSpy: CurrentRenderList now has {CurrentRenderList.Count} items");
                Mod.log.Info($"TrafficSpy: Setting IsDirty = true");

                // Process debug info
                /*Mod.log.Info($"=== Debug Info for None/Unknown Entities ===");
                int debugCount = 0;
                Dictionary<string, int> categoryCounts = new Dictionary<string, int>();

                while (debugQueue.TryDequeue(out DebugEntityInfo info))
                {
                    debugCount++;

                    // Build category string
                    List<string> categories = new List<string>();
                    if (info.isPersonalCar) categories.Add("PersonalCar");
                    if (info.isPet) categories.Add("Pet");
                    if (info.isWildlife) categories.Add("Wildlife");
                    if (info.isDeleted) categories.Add("DELETED");
                    if (info.isTemp) categories.Add("TEMP");
                    if (info.isUnspawned) categories.Add("Unspawned");

                    string category = categories.Count > 0 ? string.Join(", ", categories) : "Unknown";

                    if (!categoryCounts.ContainsKey(category))
                        categoryCounts[category] = 0;
                    categoryCounts[category]++;

                    // Log first 10 in detail
                    if (debugCount <= 10)
                    {
                        Mod.log.Info($"Entity {info.entityIndex} (Citizen: {info.citizenEntityIndex}):");
                        Mod.log.Info($"  Category: {category}");
                        Mod.log.Info($"  HasTravelPurpose: {info.hasTravelPurpose}, IsCitizen: {info.isCitizen}");
                        Mod.log.Info($"  IsController: {info.isController}, IsResident: {info.isResident}");
                        Mod.log.Info($"  HasTarget: {info.hasTarget}, HasCurrentVehicle: {info.hasCurrentVehicle}");
                    }
                }

                Mod.log.Info($"Total None/Unknown entities: {debugCount}");
                foreach (var kvp in categoryCounts)
                {
                    Mod.log.Info($"  {kvp.Key}: {kvp.Value}");
                }
                Mod.log.Info($"=== End Debug Info ===");

                debugQueue.Dispose();
                */

                targets.Dispose();
                resultsQueue.Dispose();
            }
            else
            {
                Mod.log.Info("TrafficSpy: Starting Snapshot Analysis");
                NativeList<TrafficRenderData> resultsList = new NativeList<TrafficRenderData>(Allocator.TempJob);

                SegmentActivityJob job = new SegmentActivityJob
                {
                    selectedSegment = selectedSegment,
                    subLaneLookup = SystemAPI.GetBufferLookup<SubLane>(true),
                    laneObjectLookup = SystemAPI.GetBufferLookup<Game.Net.LaneObject>(true),
                    layoutElementLookup = SystemAPI.GetBufferLookup<LayoutElement>(true),
                    passengerLookup = SystemAPI.GetBufferLookup<Passenger>(true),
                    controllerLookup = SystemAPI.GetComponentLookup<Controller>(true),
                    currentVehicleLookup = SystemAPI.GetComponentLookup<CurrentVehicle>(true),
                    travelPurposeLookup = SystemAPI.GetComponentLookup<TravelPurpose>(true),
                    targetLookup = SystemAPI.GetComponentLookup<Target>(true),
                    ownerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                    householdLookup = SystemAPI.GetComponentLookup<Household>(true),
                    householdMemberLookup = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                    workerLookup = SystemAPI.GetComponentLookup<Worker>(true),
                    studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                    creatureResidentLookup = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(true),
                    propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    //
                    deliveryTruckLookup = SystemAPI.GetComponentLookup<DeliveryTruck>(true),
                    cargoTransportLookup = SystemAPI.GetComponentLookup<CargoTransport>(true),
                    publicTransportLookup = SystemAPI.GetComponentLookup<PublicTransport>(true),
                    buildingLookup = SystemAPI.GetComponentLookup<Building>(true),
                    //
                    hearseLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Hearse>(true),
                    garbageTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.GarbageTruck>(true),
                    policeCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PoliceCar>(true),
                    fireEngineLookup = SystemAPI.GetComponentLookup<Game.Vehicles.FireEngine>(true),
                    ambulanceLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                    postVanLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PostVan>(true),
                    maintenanceVehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.MaintenanceVehicle>(true),

                    cntNone = cntNone,
                    cntShopping = cntShopping,
                    cntLeisure = cntLeisure,
                    cntGoingHome = cntGoingHome,
                    cntGoingToWork = cntGoingToWork,
                    cntMovingIn = cntMovingIn,
                    cntMovingAway = cntMovingAway,
                    cntSchool = cntSchool,
                    cntTransporting = cntTransporting,
                    cntReturning = cntReturning,
                    cntTourism = cntTourism,
                    cntOther = cntOther,
                    cntServices = cntServices,

                    results = resultsList
                };

                job.Run();

                CurrentRenderList.Clear();
                for (int i = 0; i < resultsList.Length; i++)
                    CurrentRenderList.Add(resultsList[i]);

                resultsList.Dispose();
            }

            IsDirty = true;

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
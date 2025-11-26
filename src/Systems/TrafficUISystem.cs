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
using System.Collections.Generic;
using TrafficSpy.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Diagnostics;

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

        // Toggle between methods here
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
                {
                    targets.Add(lanes[i].m_SubLane);
                }
            }
            return targets;
        }

        private void RunAnalysis(Entity selectedSegment)
        {
            // Initialize new counters
            NativeCounter cntNone = new NativeCounter(Allocator.TempJob);
            NativeCounter cntShopping = new NativeCounter(Allocator.TempJob);
            NativeCounter cntLeisure = new NativeCounter(Allocator.TempJob);
            NativeCounter cntGoingHome = new NativeCounter(Allocator.TempJob);
            NativeCounter cntGoingToWork = new NativeCounter(Allocator.TempJob);
            NativeCounter cntMovingAway = new NativeCounter(Allocator.TempJob);
            NativeCounter cntSchool = new NativeCounter(Allocator.TempJob);
            NativeCounter cntDelivery = new NativeCounter(Allocator.TempJob);
            NativeCounter cntTourism = new NativeCounter(Allocator.TempJob);
            NativeCounter cntOther = new NativeCounter(Allocator.TempJob);
            NativeCounter cntServices = new NativeCounter(Allocator.TempJob);

            if (usePathBasedAnalysis)
            {
                // === PATH BASED (Global) ===
                Mod.log.Info("TrafficSpy: Starting Path-Based Analysis");

                // Use NativeQueue for variable result size in parallel job
                NativeQueue<TrafficRenderData> resultsQueue = new NativeQueue<TrafficRenderData>(Allocator.TempJob);
                NativeHashSet<Entity> targets = GetTargetEntities(selectedSegment, Allocator.TempJob);

                PathActivityJob pathJob = new PathActivityJob
                {
                    targets = targets,

                    travelPurposeLookup = SystemAPI.GetComponentLookup<TravelPurpose>(true),
                    targetLookup = SystemAPI.GetComponentLookup<Target>(true),
                    householdMemberLookup = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                    workerLookup = SystemAPI.GetComponentLookup<Worker>(true),
                    studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                    creatureResidentLookup = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(true),
                    propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    ownerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                    buildingLookup = SystemAPI.GetComponentLookup<Building>(true),
                    currentVehicleLookup = SystemAPI.GetComponentLookup<CurrentVehicle>(true),

                    // Concurrent counters
                    cntNone = cntNone.ToConcurrent(),
                    cntShopping = cntShopping.ToConcurrent(),
                    cntLeisure = cntLeisure.ToConcurrent(),
                    cntGoingHome = cntGoingHome.ToConcurrent(),
                    cntGoingToWork = cntGoingToWork.ToConcurrent(),
                    cntMovingAway = cntMovingAway.ToConcurrent(),
                    cntSchool = cntSchool.ToConcurrent(),
                    cntDelivery = cntDelivery.ToConcurrent(),
                    cntTourism = cntTourism.ToConcurrent(),
                    cntOther = cntOther.ToConcurrent(),
                    cntServices = cntServices.ToConcurrent(),

                    results = resultsQueue.AsParallelWriter()
                };

                pathJob.ScheduleParallel(pathOwnerQuery, default).Complete();

                Mod.log.Info($"TrafficSpy: Job Complete.");
                // Move results from Queue to List
                CurrentRenderList.Clear();
                while (resultsQueue.TryDequeue(out TrafficRenderData item))
                {
                    CurrentRenderList.Add(item);
                }

                targets.Dispose();
                resultsQueue.Dispose();
            }
            else
            {
                // === SNAPSHOT BASED (Local) ===
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
                    householdMemberLookup = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                    workerLookup = SystemAPI.GetComponentLookup<Worker>(true),
                    studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                    creatureResidentLookup = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(true),
                    propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    deliveryTruckLookup = SystemAPI.GetComponentLookup<DeliveryTruck>(true),
                    cargoTransportLookup = SystemAPI.GetComponentLookup<CargoTransport>(true),
                    publicTransportLookup = SystemAPI.GetComponentLookup<PublicTransport>(true),
                    buildingLookup = SystemAPI.GetComponentLookup<Building>(true),

                    // Standard counters
                    cntNone = cntNone,
                    cntShopping = cntShopping,
                    cntLeisure = cntLeisure,
                    cntGoingHome = cntGoingHome,
                    cntGoingToWork = cntGoingToWork,
                    cntMovingAway = cntMovingAway,
                    cntSchool = cntSchool,
                    cntDelivery = cntDelivery,
                    cntTourism = cntTourism,
                    cntOther = cntOther,
                    cntServices = cntServices,

                    results = resultsList
                };

                job.Run();

                CurrentRenderList.Clear();
                for (int i = 0; i < resultsList.Length; i++)
                {
                    CurrentRenderList.Add(resultsList[i]);
                }
                resultsList.Dispose();
            }

            // Common cleanup and UI update
            IsDirty = true;

            // Construct updated JSON
            string json = $@"{{
                ""none"": {cntNone.Count},
                ""shopping"": {cntShopping.Count},
                ""leisure"": {cntLeisure.Count},
                ""goingHome"": {cntGoingHome.Count},
                ""goingToWork"": {cntGoingToWork.Count},
                ""movingAway"": {cntMovingAway.Count},
                ""school"": {cntSchool.Count},
                ""delivery"": {cntDelivery.Count},
                ""tourism"": {cntTourism.Count},
                ""other"": {cntOther.Count},
                ""services"": {cntServices.Count}
            }}";

            this.activityDataBinding.Update(json);

            // Dispose counters
            cntNone.Dispose();
            cntShopping.Dispose();
            cntLeisure.Dispose();
            cntGoingHome.Dispose();
            cntGoingToWork.Dispose();
            cntMovingAway.Dispose();
            cntSchool.Dispose();
            cntDelivery.Dispose();
            cntTourism.Dispose();
            cntOther.Dispose();
            cntServices.Dispose();
        }
    }
}
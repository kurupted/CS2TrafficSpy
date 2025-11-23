using Colossal;
using Colossal.Collections; // Required for NativeCounter
using Colossal.UI.Binding;
using Game.Common;
using Game.Net;
using Game.Tools;
using Game.UI.InGame;
using System.Collections.Generic; // Required for List<T>
using Unity.Jobs;
using TrafficSpy.Jobs; // Required for SegmentActivityJob (Namespace from your other file)
using Unity.Collections;    // Required for Allocator
using Unity.Entities;

namespace TrafficSpy.Systems
{
    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem toolSystem;
        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;
        private bool isToolActive = false;

        // RESTORED: These static lists are required by OriginDestRenderSystem
        public static List<Entity> CurrentOrigins = new List<Entity>();
        public static List<Entity> CurrentDestinations = new List<Entity>();

        protected override string group => "TrafficSpy";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_InfoUISystem.AddMiddleSection(this);
            this.toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            this.activityDataBinding = new ValueBinding<string>("TrafficSpy", "segmentActivity", "{}");
            this.toolActiveBinding = new ValueBinding<bool>("TrafficSpy", "toolActive", false);
            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setToolActive", (active) => {
                this.isToolActive = active;
                this.toolActiveBinding.Update(active);
                if (!active) ClearData();
            }));
        }

        protected override void OnUpdate()
        {
            if (visible && this.isToolActive)
            {
                if (this.toolSystem == null) return;

                Entity selected = this.toolSystem.selected;
                if (selected != Entity.Null && EntityManager.HasBuffer<SubLane>(selected))
                {
                    RunAnalysis(selected);
                }
                else
                {
                    ClearData();
                }
            }
            else
            {
                ClearData();
            }
        }

        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(Colossal.UI.Binding.IJsonWriter writer) { }

        private void ClearData()
        {
            this.activityDataBinding.Update("{}");
            CurrentOrigins.Clear();
            CurrentDestinations.Clear();
        }

        private void RunAnalysis(Entity selectedSegment)
        {
            // Using Allocator.TempJob as these need to exist for the duration of the job
            NativeCounter workers = new NativeCounter(Allocator.TempJob);
            NativeCounter students = new NativeCounter(Allocator.TempJob);
            NativeCounter shoppers = new NativeCounter(Allocator.TempJob);
            NativeCounter goingHome = new NativeCounter(Allocator.TempJob);
            NativeCounter healthcare = new NativeCounter(Allocator.TempJob);
            NativeCounter other = new NativeCounter(Allocator.TempJob);

            NativeList<Entity> origins = new NativeList<Entity>(Allocator.TempJob);
            NativeList<Entity> destinations = new NativeList<Entity>(Allocator.TempJob);

            SegmentActivityJob job = new SegmentActivityJob
            {
                selectedSegment = selectedSegment,
                subLaneLookup = SystemAPI.GetBufferLookup<SubLane>(true),
                laneObjectLookup = SystemAPI.GetBufferLookup<LaneObject>(true),
                layoutElementLookup = SystemAPI.GetBufferLookup<Game.Vehicles.LayoutElement>(true),
                passengerLookup = SystemAPI.GetBufferLookup<Game.Vehicles.Passenger>(true),
                controllerLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Controller>(true),
                currentVehicleLookup = SystemAPI.GetComponentLookup<Game.Creatures.CurrentVehicle>(true),
                travelPurposeLookup = SystemAPI.GetComponentLookup<Game.Citizens.TravelPurpose>(true),
                targetLookup = SystemAPI.GetComponentLookup<Game.Common.Target>(true),
                householdMemberLookup = SystemAPI.GetComponentLookup<Game.Citizens.HouseholdMember>(true),
                workerLookup = SystemAPI.GetComponentLookup<Game.Citizens.Worker>(true),
                studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                workers = workers,
                students = students,
                shoppers = shoppers,
                goingHome = goingHome,
                healthcare = healthcare,
                other = other,
                origins = origins,
                destinations = destinations
            };

            job.Run();

            CurrentOrigins.Clear();
            CurrentDestinations.Clear();
            for (int i = 0; i < origins.Length; i++) CurrentOrigins.Add(origins[i]);
            for (int i = 0; i < destinations.Length; i++) CurrentDestinations.Add(destinations[i]);

            string json = $@"{{
                ""workers"": {workers.Count},
                ""students"": {students.Count},
                ""shoppers"": {shoppers.Count},
                ""goingHome"": {goingHome.Count},
                ""healthcare"": {healthcare.Count},
                ""other"": {other.Count}
            }}";

            this.activityDataBinding.Update(json);

            workers.Dispose();
            students.Dispose();
            shoppers.Dispose();
            goingHome.Dispose();
            healthcare.Dispose();
            other.Dispose();
            origins.Dispose();
            destinations.Dispose();
        }
    }
}
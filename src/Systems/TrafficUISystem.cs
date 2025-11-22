using Colossal;
using Colossal.UI.Binding;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Game.UI;
using System.Collections.Generic;
using Traffic_Explorer.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Traffic_Explorer.Systems
{
    public partial class TrafficUISystem : UISystemBase
    {
        private ToolSystem toolSystem;
        private ValueBinding<string> activityDataBinding;

        // NEW: Tool State Bindings
        private ValueBinding<bool> toolActiveBinding;
        private bool isToolActive = false;

        // Shared data for Renderer
        public static List<Entity> CurrentOrigins = new List<Entity>();
        public static List<Entity> CurrentDestinations = new List<Entity>();

        protected override void OnCreate()
        {
            base.OnCreate();
            this.toolSystem = World.GetExistingSystemManaged<ToolSystem>();

            // 1. Create Bindings
            this.activityDataBinding = new ValueBinding<string>("TrafficSpy", "segmentActivity", "{}");
            this.toolActiveBinding = new ValueBinding<bool>("TrafficSpy", "toolActive", false);

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);

            // 2. Create Trigger to receive clicks from UI
            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setToolActive", (active) => {
                this.isToolActive = active;
                this.toolActiveBinding.Update(active);

                // Clear visuals immediately if turned off
                if (!active) ClearData();
            }));
        }

        protected override void OnUpdate()
        {
            // FIXED: Check the InputAction defined in Mod.cs
            // We do NOT check Mod.Settings.ToggleToolBinding because it is just a UI wrapper.
            /*if (Mod.toggleToolAction.WasPressedThisFrame())
            {
                this.isToolActive = !this.isToolActive;
                this.toolActiveBinding.Update(this.isToolActive);
                if (!this.isToolActive) ClearData();
            }*/

            if (!this.isToolActive) return;

            Entity selected = this.toolSystem.selected;
            if (selected == Entity.Null || !EntityManager.HasBuffer<SubLane>(selected))
            {
                ClearData();
                return;
            }

            RunAnalysis(selected);
        }

        private void ClearData()
        {
            this.activityDataBinding.Update("{}");
            CurrentOrigins.Clear();
            CurrentDestinations.Clear();
        }

        private void RunAnalysis(Entity selectedSegment)
        {
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
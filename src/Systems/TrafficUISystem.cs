using Colossal;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Net;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using System.Collections.Generic;
using TrafficSpy.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

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

    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem toolSystem;
        private DefaultToolSystem defaultToolSystem;
        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;

        private bool isToolActive = false;
        private bool defaultDebugSelectState = false;

        // Static data shared with TrafficHighlightSystem
        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        public static bool IsDirty = false; // Flag to prevent flickering

        private Entity lastSelectedEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Register as a MIDDLE section in the info panel
            m_InfoUISystem.AddMiddleSection(this);

            this.toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            this.defaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();

            // Create bindings
            this.activityDataBinding = new ValueBinding<string>("TrafficSpy", "segmentActivity", "{}");
            this.toolActiveBinding = new ValueBinding<bool>("TrafficSpy", "toolActive", false);

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);

            // Tool toggle trigger
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

        // CRITICAL: Must match the TypeScript component key
        protected override string group => "TrafficSpy.Systems.TrafficUISystem";

        // FIX 1: Implement these empty methods to prevent NotImplementedException
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer)
        {
        }

        // FIX 2: Do NOT override OnWriteProperties (or call base). 
        // The base InfoSectionBase handles writing the 'group' property needed for the UI to find this system.
        // removing the override allows the base method to work correctly.

        protected bool ShouldBeVisible(Entity entity)
        {
            // Only visible if a road segment (SubLane) is selected
            return EntityManager.Exists(entity) && EntityManager.HasBuffer<SubLane>(entity);
        }

        protected override void OnUpdate()
        {
            // Ensure system stays enabled
            if (!Enabled) Enabled = true;

            base.OnUpdate();

            Entity selected = this.toolSystem.selected;

            // Check visibility
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

            // Run analysis only when selection changes
            if (selected != lastSelectedEntity)
            {
                lastSelectedEntity = selected;
                RunAnalysis(selected);
            }
        }

        private void ClearData()
        {
            // Only clear if we actually have data to clear
            if (this.activityDataBinding.value != "{}")
            {
                this.activityDataBinding.Update("{}");
                CurrentRenderList.Clear();
                IsDirty = true; // Signal HighlightSystem to clear visuals
            }
        }

        private void RunAnalysis(Entity selectedSegment)
        {
            // Initialize counters
            NativeCounter workers = new NativeCounter(Allocator.TempJob);
            NativeCounter students = new NativeCounter(Allocator.TempJob);
            NativeCounter shoppers = new NativeCounter(Allocator.TempJob);
            NativeCounter goingHome = new NativeCounter(Allocator.TempJob);
            NativeCounter healthcare = new NativeCounter(Allocator.TempJob);
            NativeCounter cargo = new NativeCounter(Allocator.TempJob);
            NativeCounter services = new NativeCounter(Allocator.TempJob);
            NativeCounter publicTransport = new NativeCounter(Allocator.TempJob);
            NativeCounter other = new NativeCounter(Allocator.TempJob);
            NativeCounter noPurpose = new NativeCounter(Allocator.TempJob);

            NativeList<TrafficRenderData> results = new NativeList<TrafficRenderData>(Allocator.TempJob);

            // Setup Job
            SegmentActivityJob job = new SegmentActivityJob
            {
                selectedSegment = selectedSegment,
                subLaneLookup = SystemAPI.GetBufferLookup<SubLane>(true),
                laneObjectLookup = SystemAPI.GetBufferLookup<Game.Net.LaneObject>(true),
                layoutElementLookup = SystemAPI.GetBufferLookup<Game.Vehicles.LayoutElement>(true),
                passengerLookup = SystemAPI.GetBufferLookup<Game.Vehicles.Passenger>(true),
                controllerLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Controller>(true),
                currentVehicleLookup = SystemAPI.GetComponentLookup<Game.Creatures.CurrentVehicle>(true),
                travelPurposeLookup = SystemAPI.GetComponentLookup<Game.Citizens.TravelPurpose>(true),
                targetLookup = SystemAPI.GetComponentLookup<Game.Common.Target>(true),
                ownerLookup = SystemAPI.GetComponentLookup<Game.Common.Owner>(true),
                householdMemberLookup = SystemAPI.GetComponentLookup<Game.Citizens.HouseholdMember>(true),
                workerLookup = SystemAPI.GetComponentLookup<Game.Citizens.Worker>(true),
                studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                creatureResidentLookup = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(true),
                propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                deliveryTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true),
                cargoTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.CargoTransport>(true),
                publicTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PublicTransport>(true),

                workers = workers,
                students = students,
                shoppers = shoppers,
                goingHome = goingHome,
                healthcare = healthcare,
                cargo = cargo,
                services = services,
                publicTransport = publicTransport,
                other = other,
                noPurpose = noPurpose,
                results = results
            };

            job.Run();

            // Update static render list for the HighlightSystem
            CurrentRenderList.Clear();
            for (int i = 0; i < results.Length; i++)
            {
                CurrentRenderList.Add(results[i]);
            }
            IsDirty = true; // Signal HighlightSystem that data has changed

            // Build JSON for UI
            int totalOther = other.Count + noPurpose.Count;
            string json = $@"{{
                ""workers"": {workers.Count},
                ""students"": {students.Count},
                ""shoppers"": {shoppers.Count},
                ""goingHome"": {goingHome.Count},
                ""healthcare"": {healthcare.Count},
                ""cargo"": {cargo.Count},
                ""services"": {services.Count},
                ""publicTransport"": {publicTransport.Count},
                ""other"": {totalOther}
            }}";

            this.activityDataBinding.Update(json);

            // Cleanup
            workers.Dispose();
            students.Dispose();
            shoppers.Dispose();
            goingHome.Dispose();
            healthcare.Dispose();
            cargo.Dispose();
            services.Dispose();
            publicTransport.Dispose();
            other.Dispose();
            noPurpose.Dispose();
            results.Dispose();
        }
    }
}
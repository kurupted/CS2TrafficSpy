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
    // Enums and structs at namespace level
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

        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        private HashSet<Entity> highlightedEntities = new HashSet<Entity>();
        private Entity lastSelectedEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();

            UnityEngine.Debug.Log("[TrafficSpy] OnCreate START");

            // Register as a MIDDLE section
            m_InfoUISystem.AddMiddleSection(this);
            UnityEngine.Debug.Log("[TrafficSpy] Added to middle section");

            this.toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            this.defaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            UnityEngine.Debug.Log("[TrafficSpy] Got tool systems");

            // Create bindings
            this.activityDataBinding = new ValueBinding<string>("TrafficSpy", "segmentActivity", "{}");
            this.toolActiveBinding = new ValueBinding<bool>("TrafficSpy", "toolActive", false);
            UnityEngine.Debug.Log("[TrafficSpy] Created bindings");

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);
            UnityEngine.Debug.Log("[TrafficSpy] Added bindings");

            // Register the trigger
            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setToolActive", (active) => {
                UnityEngine.Debug.Log($"[TrafficSpy] setToolActive triggered: {active}");
                this.isToolActive = active;
                this.toolActiveBinding.Update(active);

                if (active)
                {
                    if (defaultToolSystem != null)
                    {
                        this.defaultDebugSelectState = this.defaultToolSystem.debugSelect;
                        this.defaultToolSystem.debugSelect = true;
                        UnityEngine.Debug.Log("[TrafficSpy] Tool ACTIVATED - debugSelect enabled");
                    }
                }
                else
                {
                    if (defaultToolSystem != null)
                    {
                        this.defaultToolSystem.debugSelect = this.defaultDebugSelectState;
                    }
                    ClearData();
                    UnityEngine.Debug.Log("[TrafficSpy] Tool DEACTIVATED");
                }
            }));

            UnityEngine.Debug.Log("[TrafficSpy] TrafficUISystem.OnCreate() completed successfully");
        }

        // CRITICAL: Must match TypeScript registration key
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
            // Keep system enabled
            if (!Enabled) Enabled = true;

            base.OnUpdate();

            Entity selected = this.toolSystem.selected;

            // Check if we should be visible
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

            // Run analysis when selection changes
            if (selected != lastSelectedEntity)
            {
                lastSelectedEntity = selected;
                UnityEngine.Debug.Log($"[TrafficSpy] Selection changed to: {selected.Index}");
                RunAnalysis(selected);
            }
        }

        private void ClearData()
        {
            if (this.activityDataBinding.value != "{}")
            {
                UnityEngine.Debug.Log("[TrafficSpy] Clearing data");
                this.activityDataBinding.Update("{}");
                CurrentRenderList.Clear();
                ClearHighlights();
            }
        }

        private void ClearHighlights()
        {
            UnityEngine.Debug.Log($"[TrafficSpy] Clearing {highlightedEntities.Count} highlights");
            foreach (var entity in highlightedEntities)
            {
                if (EntityManager.Exists(entity))
                {
                    // Remove Highlighted component
                    if (EntityManager.HasComponent<Highlighted>(entity))
                    {
                        EntityManager.RemoveComponent<Highlighted>(entity);
                    }
                    // Mark for batch update
                    if (!EntityManager.HasComponent<BatchesUpdated>(entity))
                    {
                        EntityManager.AddComponent<BatchesUpdated>(entity);
                    }
                }
            }
            highlightedEntities.Clear();
        }

        private void AddHighlight(Entity entity)
        {
            if (!EntityManager.Exists(entity))
            {
                UnityEngine.Debug.LogWarning($"[TrafficSpy] Cannot highlight - entity does not exist: {entity.Index}");
                return;
            }

            Entity target = entity;

            // If it's a renter (household/company), get the building
            if (EntityManager.HasComponent<PropertyRenter>(entity))
            {
                PropertyRenter renter = EntityManager.GetComponentData<PropertyRenter>(entity);
                target = renter.m_Property;
                UnityEngine.Debug.Log($"[TrafficSpy] Resolved renter {entity.Index} to building {target.Index}");
            }

            if (!EntityManager.Exists(target))
            {
                UnityEngine.Debug.LogWarning($"[TrafficSpy] Target entity does not exist: {target.Index}");
                return;
            }

            if (!highlightedEntities.Contains(target))
            {
                // Add Highlighted component
                if (!EntityManager.HasComponent<Highlighted>(target))
                {
                    EntityManager.AddComponent<Highlighted>(target);
                }

                // Mark for batch update
                if (!EntityManager.HasComponent<BatchesUpdated>(target))
                {
                    EntityManager.AddComponent<BatchesUpdated>(target);
                }

                highlightedEntities.Add(target);
                UnityEngine.Debug.Log($"[TrafficSpy] Highlighted entity: {target.Index}");
            }
        }

        private void RunAnalysis(Entity selectedSegment)
        {
            UnityEngine.Debug.Log($"[TrafficSpy] Running analysis on segment: {selectedSegment.Index}");

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

            // Create and run the job
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

            UnityEngine.Debug.Log($"[TrafficSpy] Job complete. Found {results.Length} entities");

            // Clear old highlights and add new ones
            ClearHighlights();

            for (int i = 0; i < results.Length; i++)
            {
                AddHighlight(results[i].entity);
            }

            UnityEngine.Debug.Log($"[TrafficSpy] Added {highlightedEntities.Count} highlights");

            // Update render list
            CurrentRenderList.Clear();
            for (int i = 0; i < results.Length; i++)
            {
                CurrentRenderList.Add(results[i]);
            }

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

            // Update the binding
            this.activityDataBinding.Update(json);

            UnityEngine.Debug.Log($"[TrafficSpy] Analysis Complete. Highlights: {highlightedEntities.Count}. Data: {json}");

            // Dispose
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
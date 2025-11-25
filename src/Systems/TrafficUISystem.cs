using Colossal;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game.Buildings; // FIXED: Needed for PropertyRenter
using Game.Citizens;
using Game.Common;
using Game.Net;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using System.Collections.Generic;
using TrafficSpy.Jobs; // FIXED: Needed for SegmentActivityJob
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TrafficSpy.Systems
{
    // FIXED: Defined at namespace level so Jobs can see them
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

        // FIXED: Restored these missing variables
        private bool isToolActive = false;
        private bool defaultDebugSelectState = false;

        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        private HashSet<Entity> highlightedEntities = new HashSet<Entity>();
        private Entity lastSelectedEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Register as a native Info Section (like EmploymentTracker)
            m_InfoUISystem.AddMiddleSection(this);

            this.toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            this.defaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();

            this.activityDataBinding = new ValueBinding<string>("TrafficSpy", "segmentActivity", "{}");
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
                        UnityEngine.Debug.Log("[TrafficSpy] Tool ACTIVATED");
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
        }

        protected override string group => "TrafficSpy";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }

        protected bool ShouldBeVisible(Entity entity)
        {
            return EntityManager.Exists(entity) && EntityManager.HasBuffer<SubLane>(entity);
        }

        protected override void OnUpdate()
        {
            // Prevent the system from being disabled by the game
            if (!Enabled) { Enabled = true; }

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
                RunAnalysis(selected);
            }
        }

        private void ClearData()
        {
            if (this.activityDataBinding.value != "{}")
            {
                this.activityDataBinding.Update("{}");
                CurrentRenderList.Clear();
                ClearHighlights();
            }
        }

        private void ClearHighlights()
        {
            foreach (var entity in highlightedEntities)
            {
                if (EntityManager.Exists(entity))
                {
                    EntityManager.RemoveComponent<Highlighted>(entity);
                    EntityManager.AddComponent<BatchesUpdated>(entity);
                }
            }
            highlightedEntities.Clear();
        }

        private void AddHighlight(Entity entity)
        {
            if (!EntityManager.Exists(entity)) return;

            Entity target = entity;

            if (EntityManager.HasComponent<PropertyRenter>(entity))
            {
                PropertyRenter renter = EntityManager.GetComponentData<PropertyRenter>(entity);
                target = renter.m_Property;
            }

            if (EntityManager.Exists(target) && !highlightedEntities.Contains(target))
            {
                EntityManager.AddComponent<Highlighted>(target);
                EntityManager.AddComponent<BatchesUpdated>(target);
                highlightedEntities.Add(target);
            }
        }

        private void RunAnalysis(Entity selectedSegment)
        {
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

            ClearHighlights();

            for (int i = 0; i < results.Length; i++)
            {
                AddHighlight(results[i].entity);
            }

            CurrentRenderList.Clear();
            for (int i = 0; i < results.Length; i++)
            {
                CurrentRenderList.Add(results[i]);
            }

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

            if (results.Length > 0)
            {
                UnityEngine.Debug.Log($"[TrafficSpy] Analysis Complete. Highlights: {results.Length}. Data: {json}");
            }

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
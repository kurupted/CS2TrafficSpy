using Colossal.Collections;
using Colossal.UI.Binding;
using Game.Common;
using Game.Net;
using Game.Tools;
using Game.UI;
using System.Collections.Generic;
using Unity.Jobs;
using TrafficSpy.Jobs;
using Unity.Collections;
using Unity.Entities;
using Colossal;
using UnityEngine;
using Game.Citizens;
using Game.Buildings;

namespace TrafficSpy.Systems
{
    public struct TrafficRenderData
    {
        public Entity entity;
        public Purpose purpose;
        public bool isOrigin;
    }

    public partial class TrafficUISystem : UISystemBase
    {
        private ToolSystem toolSystem;
        private DefaultToolSystem defaultToolSystem;
        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;
        private bool isToolActive = false;
        private bool defaultDebugSelectState = false;

        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        private HashSet<Entity> highlightedEntities = new HashSet<Entity>();

        // NEW: Track selection to prevent spamming updates
        private Entity lastSelectedEntity = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();
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

        protected override void OnUpdate()
        {
            if (toolSystem == null) return;

            Entity selected = this.toolSystem.selected;

            // FIX: Only run logic if selection CHANGED
            if (selected != lastSelectedEntity)
            {
                lastSelectedEntity = selected;

                // Check if it's a road
                bool isRoad = selected != Entity.Null && EntityManager.HasBuffer<SubLane>(selected);

                if (isRoad)
                {
                    UnityEngine.Debug.Log($"[TrafficSpy] Selection Changed: {selected.Index}. Running Analysis.");
                    RunAnalysis(selected);
                }
                else
                {
                    ClearData();
                }
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
                householdMemberLookup = SystemAPI.GetComponentLookup<Game.Citizens.HouseholdMember>(true),
                workerLookup = SystemAPI.GetComponentLookup<Game.Citizens.Worker>(true),
                studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                creatureResidentLookup = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(true),
                propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),

                workers = workers,
                students = students,
                shoppers = shoppers,
                goingHome = goingHome,
                healthcare = healthcare,
                other = other,
                noPurpose = noPurpose,
                results = results
            };

            job.Run();

            // Update Highlights only once per selection change
            ClearHighlights();

            for (int i = 0; i < results.Length; i++)
            {
                AddHighlight(results[i].entity);
            }

            int totalOther = other.Count + noPurpose.Count;
            string json = $@"{{
                ""workers"": {workers.Count},
                ""students"": {students.Count},
                ""shoppers"": {shoppers.Count},
                ""goingHome"": {goingHome.Count},
                ""healthcare"": {healthcare.Count},
                ""other"": {totalOther}
            }}";

            this.activityDataBinding.Update(json);

            if (results.Length > 0)
            {
                UnityEngine.Debug.Log($"[TrafficSpy] Highlighting {results.Length} buildings (Cyan Glow).");
            }

            workers.Dispose();
            students.Dispose();
            shoppers.Dispose();
            goingHome.Dispose();
            healthcare.Dispose();
            other.Dispose();
            noPurpose.Dispose();
            results.Dispose();
        }
    }
}
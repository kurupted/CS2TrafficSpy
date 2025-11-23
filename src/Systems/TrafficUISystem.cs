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
                        UnityEngine.Debug.Log("[TrafficSpy] Tool ACTIVATED - Debug Select ON");
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

            // Optimization: Only run if selection CHANGED or we haven't analyzed it yet
            if (selected != lastSelectedEntity)
            {
                lastSelectedEntity = selected;

                if (selected != Entity.Null && EntityManager.HasBuffer<SubLane>(selected))
                {
                    UnityEngine.Debug.Log($"[TrafficSpy] New Road Selected: {selected.Index}. Running Analysis...");
                    RunAnalysis(selected);
                }
                else
                {
                    if (selected != Entity.Null)
                        UnityEngine.Debug.Log($"[TrafficSpy] Selected {selected.Index} is not a road (No SubLanes). Clearing data.");

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
            int count = 0;
            foreach (var entity in highlightedEntities)
            {
                if (EntityManager.Exists(entity))
                {
                    EntityManager.RemoveComponent<Highlighted>(entity);
                    EntityManager.AddComponent<BatchesUpdated>(entity);
                    count++;
                }
            }
            highlightedEntities.Clear();
            if (count > 0) UnityEngine.Debug.Log($"[TrafficSpy] Cleared {count} highlights.");
        }

        private void AddHighlight(Entity entity, string debugLabel)
        {
            if (!EntityManager.Exists(entity))
            {
                UnityEngine.Debug.LogWarning($"[TrafficSpy] Cannot highlight {debugLabel}: Entity does not exist.");
                return;
            }

            Entity target = entity;

            // RESOLUTION DEBUGGING
            bool isRenter = false;
            if (EntityManager.HasComponent<PropertyRenter>(entity))
            {
                PropertyRenter renter = EntityManager.GetComponentData<PropertyRenter>(entity);
                target = renter.m_Property;
                isRenter = true;
            }

            if (EntityManager.Exists(target))
            {
                if (!highlightedEntities.Contains(target))
                {
                    EntityManager.AddComponent<Highlighted>(target);
                    EntityManager.AddComponent<BatchesUpdated>(target); // Force visual refresh
                    highlightedEntities.Add(target);

                    // Detailed log for the first few to avoid spam
                    if (highlightedEntities.Count <= 5)
                    {
                        string type = isRenter ? $"Virtual(Renter) -> Physical(Building {target.Index})" : "Physical(Building)";
                        UnityEngine.Debug.Log($"[TrafficSpy] Highlighting {debugLabel}: {type}");
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[TrafficSpy] Failed to highlight {debugLabel}: Target building {target.Index} does not exist.");
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

            CurrentRenderList.Clear();
            ClearHighlights();

            UnityEngine.Debug.Log($"[TrafficSpy] Analysis Complete. Processing {results.Length} results for highlighting...");

            for (int i = 0; i < results.Length; i++)
            {
                CurrentRenderList.Add(results[i]);
                // Add Highlight with debug label
                AddHighlight(results[i].entity, $"Item #{i} ({results[i].purpose})");
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

            UnityEngine.Debug.Log($"[TrafficSpy] Stats: Work:{workers.Count} School:{students.Count} Shop:{shoppers.Count} Home:{goingHome.Count} Other:{totalOther}");

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
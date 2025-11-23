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
                        // Optional: Log what we clicked on if it wasn't a road
                        UnityEngine.Debug.Log($"[TrafficSpy] Ignored Selection {selected.Index} (Not a Road/Lane).");

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
            // Only log if we actually cleared something to avoid spam
            if (count > 0) UnityEngine.Debug.Log($"[TrafficSpy] Cleared {count} highlights.");
        }

        private void AddHighlight(Entity entity, string debugLabel)
        {
            // Check 1: Does the source entity exist?
            if (!EntityManager.Exists(entity))
            {
                UnityEngine.Debug.LogWarning($"[TrafficSpy] FAILED {debugLabel}: Source Entity {entity.Index} does not exist.");
                return;
            }

            Entity target = entity;
            bool isRenter = false;

            // Check 2: Is it a Renter (Virtual) or Building (Physical)?
            if (EntityManager.HasComponent<PropertyRenter>(entity))
            {
                PropertyRenter renter = EntityManager.GetComponentData<PropertyRenter>(entity);
                target = renter.m_Property;
                isRenter = true;
            }
            else
            {
                // LOG ELSE: Useful to know if we are trying to highlight a raw building vs a person
                UnityEngine.Debug.Log($"[TrafficSpy] Info {debugLabel}: Entity {entity.Index} has no PropertyRenter. Assuming it is already a Building/Object.");
            }

            // Check 3: Does the FINAL target exist?
            if (EntityManager.Exists(target))
            {
                if (!highlightedEntities.Contains(target))
                {
                    EntityManager.AddComponent<Highlighted>(target);
                    EntityManager.AddComponent<BatchesUpdated>(target);
                    highlightedEntities.Add(target);

                    // Log Success
                    string type = isRenter ? $"Virtual(Renter) -> Physical(Building {target.Index})" : $"Physical({target.Index})";
                    UnityEngine.Debug.Log($"[TrafficSpy] HIGHLIGHT SUCCESS {debugLabel}: {type}");
                }
            }
            else
            {
                // Log Failure: We had a renter, but their building reference was dead/null
                UnityEngine.Debug.LogWarning($"[TrafficSpy] FAILED {debugLabel}: Target Building {target.Index} (from Source {entity.Index}) does not exist.");
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

            UnityEngine.Debug.Log($"[TrafficSpy] Analysis Found {results.Length} entities. Beginning Highlight Loop...");

            for (int i = 0; i < results.Length; i++)
            {
                CurrentRenderList.Add(results[i]);
                // Pass a debug string so we know which item in the list failed
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
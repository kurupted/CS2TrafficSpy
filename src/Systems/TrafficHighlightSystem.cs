using Game;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Tools;
using System.Collections.Generic;
using TrafficSpy.Systems;
using Unity.Collections;
using Unity.Entities;

namespace TrafficSpy.Systems
{
    // This system runs in the main loop to ensure visual updates happen correctly
    public partial class TrafficHighlightSystem : SystemBase
    {
        private HashSet<Entity> highlightedEntities = new HashSet<Entity>();

        protected override void OnCreate()
        {
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            // Only update if the TrafficUISystem signals that data has changed
            if (TrafficUISystem.IsDirty)
            {
                UpdateHighlights(TrafficUISystem.CurrentRenderList);
                TrafficUISystem.IsDirty = false;
            }
        }

        private void UpdateHighlights(List<TrafficRenderData> renderList)
        {
            // 1. Identify all entities that SHOULD be highlighted now
            HashSet<Entity> newSet = new HashSet<Entity>();
            foreach (var item in renderList)
            {
                if (EntityManager.Exists(item.entity))
                {
                    newSet.Add(item.entity);
                }
            }

            // 2. Remove highlight from entities that are NO LONGER in the list
            List<Entity> toRemove = new List<Entity>();
            foreach (var entity in highlightedEntities)
            {
                if (!newSet.Contains(entity))
                {
                    toRemove.Add(entity);
                }
            }

            foreach (var entity in toRemove)
            {
                if (EntityManager.Exists(entity))
                {
                    // Remove Highlighted directly
                    EntityManager.RemoveComponent<Highlighted>(entity);
                    EntityManager.AddComponent<BatchesUpdated>(entity);
                }
                highlightedEntities.Remove(entity);
            }

            // 3. Add highlight to entities that are NEWLY in the list
            foreach (var entity in newSet)
            {
                // If it's not already highlighted, highlight it
                if (!highlightedEntities.Contains(entity))
                {
                    EntityManager.AddComponent<Highlighted>(entity);
                    EntityManager.AddComponent<BatchesUpdated>(entity);
                    highlightedEntities.Add(entity);
                }
                // If it IS already highlighted, do nothing (prevents flickering)
            }
        }
    }
}
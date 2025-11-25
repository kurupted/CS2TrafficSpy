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
            ClearHighlights();

            foreach (var item in renderList)
            {
                AddHighlight(item.entity);
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
    }
}
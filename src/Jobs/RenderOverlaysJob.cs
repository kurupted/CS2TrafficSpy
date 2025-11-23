using Game.Common;
using Game.Rendering;
using Game.Buildings;
using Game.Objects;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public struct RenderOverlaysJob : IJob
    {
        // The buffer we draw into
        public OverlayRenderSystem.Buffer overlayBuffer;

        // Input Data
        [ReadOnly] public NativeList<Entity> origins;
        [ReadOnly] public NativeList<Entity> destinations;

        // Lookups to find physical positions
        // FIXED: Explicitly use Game.Objects.Transform to avoid ambiguity with UnityEngine.Transform
        [ReadOnly] public ComponentLookup<Game.Objects.Transform> transformLookup;
        [ReadOnly] public ComponentLookup<PropertyRenter> renterLookup;

        public void Execute()
        {
            // FIXED: Explicitly use UnityEngine.Color
            UnityEngine.Color green = new UnityEngine.Color(0f, 1f, 0f, 0.5f);
            for (int i = 0; i < origins.Length; i++)
            {
                DrawEntityCircle(origins[i], green);
            }

            UnityEngine.Color red = new UnityEngine.Color(1f, 0f, 0f, 0.5f);
            for (int i = 0; i < destinations.Length; i++)
            {
                DrawEntityCircle(destinations[i], red);
            }
        }

        private void DrawEntityCircle(Entity entity, UnityEngine.Color color)
        {
            if (entity == Entity.Null) return;

            Entity physicalEntity = entity;

            // 1. If the entity is "virtual" (like a Household), find the Property it rents
            if (renterLookup.TryGetComponent(entity, out PropertyRenter renter))
            {
                physicalEntity = renter.m_Property;
            }

            // 2. Get the position
            // FIXED: Explicitly use Game.Objects.Transform
            if (transformLookup.TryGetComponent(physicalEntity, out Game.Objects.Transform transform))
            {
                // Draw circle with 6m radius
                overlayBuffer.DrawCircle(color, transform.m_Position, 6.0f);
            }
        }
    }
}
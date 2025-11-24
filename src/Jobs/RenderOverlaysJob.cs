using Game.Common;
using Game.Rendering;
using Game.Buildings;
using Game.Objects;
using Game.Prefabs;
using Game.Citizens;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Colossal.Mathematics;
using TrafficSpy.Systems;

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public struct RenderOverlaysJob : IJob
    {
        public OverlayRenderSystem.Buffer overlayBuffer;

        [ReadOnly] public NativeList<TrafficRenderData> renderList;

        [ReadOnly] public ComponentLookup<Game.Objects.Transform> transformLookup;
        [ReadOnly] public ComponentLookup<PrefabRef> prefabRefLookup;
        [ReadOnly] public ComponentLookup<ObjectGeometryData> objectGeometryDataLookup;

        public void Execute()
        {
            for (int i = 0; i < renderList.Length; i++)
            {
                TrafficRenderData data = renderList[i];
                UnityEngine.Color color = GetColorForPurpose(data.purpose, data.isOrigin);
                DrawEntityOutline(data.entity, color);
            }
        }

        private UnityEngine.Color GetColorForPurpose(Purpose purpose, bool isOrigin)
        {
            if (isOrigin) return new UnityEngine.Color(0f, 1f, 0f, 1f);

            switch (purpose)
            {
                case Purpose.GoingToWork:
                case Purpose.Working:
                    return new UnityEngine.Color(1f, 0.92f, 0.016f, 1f);
                case Purpose.GoingToSchool:
                case Purpose.Studying:
                    return new UnityEngine.Color(0f, 0.5f, 1f, 1f);
                case Purpose.Shopping:
                case Purpose.Leisure:
                    return new UnityEngine.Color(0.5f, 0f, 0.5f, 1f);
                case Purpose.GoingHome:
                    return new UnityEngine.Color(0f, 1f, 1f, 1f);
                case Purpose.Hospital:
                case Purpose.InHospital:
                    return new UnityEngine.Color(1f, 0f, 0f, 1f);
                default:
                    return new UnityEngine.Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }

        private void DrawEntityOutline(Entity entity, UnityEngine.Color color)
        {
            if (entity == Entity.Null) return;

            if (!transformLookup.TryGetComponent(entity, out Game.Objects.Transform transform))
                return;

            // Debug Circle
            overlayBuffer.DrawCircle(color, transform.m_Position, 10.0f);

            if (prefabRefLookup.TryGetComponent(entity, out PrefabRef prefabRef) &&
                objectGeometryDataLookup.TryGetComponent(prefabRef.m_Prefab, out ObjectGeometryData geometry))
            {
                Bounds3 bounds = geometry.m_Bounds;

                float3 c1 = new float3(bounds.min.x, 0, bounds.min.z);
                float3 c2 = new float3(bounds.max.x, 0, bounds.min.z);
                float3 c3 = new float3(bounds.max.x, 0, bounds.max.z);
                float3 c4 = new float3(bounds.min.x, 0, bounds.max.z);

                float3 w1 = LocalToWorld(transform, c1);
                float3 w2 = LocalToWorld(transform, c2);
                float3 w3 = LocalToWorld(transform, c3);
                float3 w4 = LocalToWorld(transform, c4);

                if (IsValid(w1) && IsValid(w2) && IsValid(w3) && IsValid(w4))
                {
                    float w = 5.0f;
                    overlayBuffer.DrawLine(color, new Line3.Segment(w1, w2), w);
                    overlayBuffer.DrawLine(color, new Line3.Segment(w2, w3), w);
                    overlayBuffer.DrawLine(color, new Line3.Segment(w3, w4), w);
                    overlayBuffer.DrawLine(color, new Line3.Segment(w4, w1), w);
                }
            }
        }

        private bool IsValid(float3 p)
        {
            return math.all(math.isfinite(p));
        }

        private float3 LocalToWorld(Game.Objects.Transform transform, float3 localPos)
        {
            return math.mul(transform.m_Rotation, localPos) + transform.m_Position;
        }
    }
}
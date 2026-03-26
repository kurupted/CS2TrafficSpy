using System;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Routes;
using Game.Pathfind; // Fixes PathElement symbol
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public struct RenderTransitLineOverlayJob : IJobChunk
    {
        public OverlayRenderSystem.Buffer overlayBuffer; // Uses vanilla type
        
        [ReadOnly] public EntityTypeHandle EntityType;
        [ReadOnly] public ComponentTypeHandle<Game.Routes.Color> ColorType;
        [ReadOnly] public BufferTypeHandle<RouteSegment> SegmentBufferType;
        
        [ReadOnly] public BufferLookup<PathElement> PathElementLookup;
        [ReadOnly] public ComponentLookup<Curve> CurveLookup;
        [ReadOnly] public NativeHashSet<Entity> HiddenRoutes;

        [ReadOnly] public ComponentLookup<PrefabRef> PrefabRefLookup;
        [ReadOnly] public ComponentLookup<TransportLineData> TransportLineDataLookup;
        public float ZoomLevel; 

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            NativeArray<Entity> entities = chunk.GetNativeArray(EntityType);
            NativeArray<Game.Routes.Color> colors = chunk.GetNativeArray(ref ColorType);
            BufferAccessor<RouteSegment> segmentAccess = chunk.GetBufferAccessor(ref SegmentBufferType);

            // Scale thickness based on zoom (thicker when zoomed out)
            float minZoom = 1600f;
            float maxZoom = 10000f;
            float normalizedZoom = math.clamp((ZoomLevel - minZoom) / (maxZoom - minZoom), 0f, 1f);
            float baseWidth = 5.0f;
            float maxWidth = baseWidth * 10f; // 18.0f
            float thickness = math.lerp(baseWidth, maxWidth, normalizedZoom);

            for (int i = 0; i < chunk.Count; i++)
            {
                Entity routeEntity = entities[i];
                if (HiddenRoutes.Contains(routeEntity)) continue;

                // Hide Airplane transit lines
                if (PrefabRefLookup.TryGetComponent(routeEntity, out var prefabRef) &&
                    TransportLineDataLookup.TryGetComponent(prefabRef.m_Prefab, out var lineData))
                {
                    if (lineData.m_TransportType == TransportType.Airplane) continue;
                    if (lineData.m_TransportType == TransportType.Ship) continue;
                }

                UnityEngine.Color renderColor = colors[i].m_Color;
                DynamicBuffer<RouteSegment> segments = segmentAccess[i];

                for (int j = 0; j < segments.Length; j++)
                {
                    Entity segmentEntity = segments[j].m_Segment;
                    if (PathElementLookup.TryGetBuffer(segmentEntity, out DynamicBuffer<PathElement> path))
                    {
                        for (int k = 0; k < path.Length; k++)
                        {
                            if (CurveLookup.TryGetComponent(path[k].m_Target, out Curve curve))
                            {
                                overlayBuffer.DrawCurve(renderColor, curve.m_Bezier, thickness, new Unity.Mathematics.float2(0, 1));
                            }
                        }
                    }
                }
            }
        }
    }
}
using System;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Routes;
using Game.Pathfind; 
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public struct RenderTransitLineOverlayJob : IJobChunk
    {
        public OverlayRenderSystem.Buffer overlayBuffer; 
        
        [ReadOnly] public EntityTypeHandle EntityType;
        [ReadOnly] public ComponentTypeHandle<Game.Routes.Color> ColorType;
        [ReadOnly] public BufferTypeHandle<RouteSegment> SegmentBufferType;
        
        [ReadOnly] public BufferTypeHandle<RouteWaypoint> WaypointBufferType;
        [ReadOnly] public ComponentLookup<Game.Routes.Connected> ConnectedLookup; // Fixes stops
        [ReadOnly] public ComponentLookup<Game.Objects.Transform> TransformLookup;
        public bool DrawStops;
        
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
            
            bool hasWaypoints = chunk.Has(ref WaypointBufferType);
            BufferAccessor<RouteWaypoint> waypointAccess = hasWaypoints ? chunk.GetBufferAccessor(ref WaypointBufferType) : default;

            float minZoom = 1600f;
            float maxZoom = 10000f;
            float normalizedZoom = math.clamp((ZoomLevel - minZoom) / (maxZoom - minZoom), 0f, 1f);
            float baseWidth = 5.0f;
            float maxWidth = baseWidth * 10f; 
            float thickness = math.lerp(baseWidth, maxWidth, normalizedZoom);

            for (int i = 0; i < chunk.Count; i++)
            {
                Entity routeEntity = entities[i];
                if (HiddenRoutes.Contains(routeEntity)) continue;

                if (PrefabRefLookup.TryGetComponent(routeEntity, out var prefabRef) &&
                    TransportLineDataLookup.TryGetComponent(prefabRef.m_Prefab, out var lineData))
                {
                    var t = lineData.m_TransportType;
                    if (t == TransportType.Airplane) continue;
                    
                    if (t != TransportType.Bus && t != TransportType.Train && t != TransportType.Tram && 
                        t != TransportType.Subway && t != TransportType.Ship && t != TransportType.Ferry) {
                        continue; 
                    }
                }
                else 
                {
                    continue;
                }

                UnityEngine.Color renderColor = colors[i].m_Color;
                
                // 1. Draw the Route Lines
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

                // 2. Draw the Stations/Stops manually
                if (DrawStops && hasWaypoints)
                {
                    DynamicBuffer<RouteWaypoint> waypoints = waypointAccess[i];
                    for (int w = 0; w < waypoints.Length; w++)
                    {
                        Entity waypointEntity = waypoints[w].m_Waypoint;
                        
                        // FIX: Waypoints themselves don't have transforms. They connect to the physical stop!
                        if (ConnectedLookup.TryGetComponent(waypointEntity, out var connected))
                        {
                            Entity physicalStop = connected.m_Connected;
                            if (TransformLookup.TryGetComponent(physicalStop, out Game.Objects.Transform trans))
                            {
                                overlayBuffer.DrawCircle(renderColor, trans.m_Position, thickness * 3f);
                                overlayBuffer.DrawCircle(new UnityEngine.Color(1f, 1f, 1f, 0.8f), trans.m_Position, thickness * 1.5f);
                            }
                        }
                    }
                }
            }
        }
    }
}
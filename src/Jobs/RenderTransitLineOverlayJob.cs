using Game.Net;
using Game.Rendering;
using Game.Routes;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public struct RenderTransitLineOverlayJob : IJobChunk
    {
        public OverlayRenderSystem.Buffer overlayBuffer;
        
        // FIX: Use EntityTypeHandle instead of ComponentTypeHandle<Entity>
        [ReadOnly] public EntityTypeHandle EntityType;
        [ReadOnly] public ComponentTypeHandle<Game.Routes.Color> ColorType;
        [ReadOnly] public BufferTypeHandle<RouteWaypoint> WaypointBufferType;
        
        [ReadOnly] public ComponentLookup<Curve> CurveLookup;
        // FIX: Need ConnectedLookup to find the road segment from the waypoint
        [ReadOnly] public ComponentLookup<Connected> ConnectedLookup;
        [ReadOnly] public NativeHashSet<Entity> HiddenRoutes;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            NativeArray<Entity> entities = chunk.GetNativeArray(EntityType);
            NativeArray<Game.Routes.Color> colors = chunk.GetNativeArray(ref ColorType);
            BufferAccessor<RouteWaypoint> waypointAccess = chunk.GetBufferAccessor(ref WaypointBufferType);

            for (int i = 0; i < chunk.Count; i++)
            {
                Entity routeEntity = entities[i];
                if (HiddenRoutes.Contains(routeEntity)) continue;

                UnityEngine.Color renderColor = colors[i].m_Color;
                DynamicBuffer<RouteWaypoint> waypoints = waypointAccess[i];

                for (int j = 0; j < waypoints.Length; j++)
                {
                    Entity wpEntity = waypoints[j].m_Waypoint;
                    
                    // FIX: Waypoints use the 'Connected' component to link to the road/track Edge
                    if (ConnectedLookup.TryGetComponent(wpEntity, out Connected connected))
                    {
                        if (CurveLookup.TryGetComponent(connected.m_Connected, out Curve curve))
                        {
                            overlayBuffer.DrawCurve(
                                renderColor, 
                                curve.m_Bezier, 
                                4.0f,            // Line width
                                new float2(0, 1) 
                            );
                        }
                    }
                }
            }
        }
    }
}
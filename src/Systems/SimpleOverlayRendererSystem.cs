using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Rendering;
using Game.Routes;
using Game.Tools;
using TrafficSpy.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Color = UnityEngine.Color;

namespace TrafficSpy.Systems
{
    // A simple system to render lines and curves on the map overlay.
    // Adapted from EmploymentTracker.
    public partial class SimpleOverlayRendererSystem : SystemBase
    {
        private OverlayRenderSystem m_OverlayRenderSystem;
        private TrafficUISystem m_TrafficUISystem;
        private EntityQuery m_TransitLinesQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_OverlayRenderSystem = World.GetExistingSystemManaged<OverlayRenderSystem>();
            
            m_TrafficUISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();
    
            // Define the query for routes that have colors and waypoints
            m_TransitLinesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { 
                    ComponentType.ReadOnly<Route>(), 
                    ComponentType.ReadOnly<Game.Routes.Color>(), 
                    ComponentType.ReadOnly<RouteWaypoint>() 
                },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }

// Inside SimpleOverlayRendererSystem.OnUpdate()
        protected override void OnUpdate()
        {
            if (m_TrafficUISystem == null || !m_TrafficUISystem.IsTransitPanelActive) return;

            var hiddenSet = new NativeHashSet<Entity>(TrafficUISystem.HiddenCustomRoutes.Count, Allocator.TempJob);
            foreach (var e in TrafficUISystem.HiddenCustomRoutes) hiddenSet.Add(e);

            // Assuming m_OverlayRenderSystem is your reference to vanilla OverlayRenderSystem
            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle deps);

            var transitJob = new RenderTransitLineOverlayJob
            {
                overlayBuffer = buffer,
                // FIX: Pass EntityTypeHandle correctly
                EntityType = GetEntityTypeHandle(),
                ColorType = GetComponentTypeHandle<Game.Routes.Color>(true),
                WaypointBufferType = GetBufferTypeHandle<RouteWaypoint>(true),
                CurveLookup = GetComponentLookup<Curve>(true),
                ConnectedLookup = GetComponentLookup<Connected>(true), // Added this
                HiddenRoutes = hiddenSet
            };

            // FIX: Use the local m_TransitLinesQuery
            JobHandle transitHandle = transitJob.Schedule(m_TransitLinesQuery, JobHandle.CombineDependencies(Dependency, deps));
    
            hiddenSet.Dispose(transitHandle);
            Dependency = transitHandle;
            m_OverlayRenderSystem.AddBufferWriter(Dependency);
        }

        public Buffer GetBuffer(out JobHandle dependencies)
        {
            return new Buffer(m_OverlayRenderSystem.GetBuffer(out dependencies));
        }

        public void AddBufferWriter(JobHandle handle)
        {
            m_OverlayRenderSystem.AddBufferWriter(handle);
        }

        public struct Buffer
        {
            private OverlayRenderSystem.Buffer m_Buffer;

            public Buffer(OverlayRenderSystem.Buffer buffer)
            {
                m_Buffer = buffer;
            }

            public void DrawCurve(Color color, Bezier4x3 curve, float width, float2 roundness)
            {
                m_Buffer.DrawCurve(color, curve, width, roundness);
            }

            public void DrawLine(Color color, Line3.Segment line, float width)
            {
                m_Buffer.DrawLine(color, line, width);
            }
        }
    }
}
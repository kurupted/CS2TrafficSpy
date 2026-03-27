using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Rendering;
using Game.Routes;
using Game.Tools;
using Game.Pathfind; // Required for PathElement
using Game.Prefabs; // Required for TransportLineData
using TrafficSpy.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Color = UnityEngine.Color;

namespace TrafficSpy.Systems
{
    public partial class SimpleOverlayRendererSystem : SystemBase
    {
        private OverlayRenderSystem m_OverlayRenderSystem;
        private TrafficUISystem m_TrafficUISystem;
        private CameraUpdateSystem m_CameraUpdateSystem; 
        private EntityQuery m_TransitLinesQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_OverlayRenderSystem = World.GetExistingSystemManaged<OverlayRenderSystem>();
            m_TrafficUISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();
            m_CameraUpdateSystem = World.GetExistingSystemManaged<CameraUpdateSystem>();

            m_TransitLinesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { 
                    ComponentType.ReadOnly<Route>(), 
                    ComponentType.ReadOnly<Game.Routes.Color>(), 
                    ComponentType.ReadOnly<RouteSegment>() 
                },
                None = new[] { 
                    ComponentType.ReadOnly<Deleted>(), 
                    ComponentType.ReadOnly<Game.Tools.Temp>() // Explicit namespace fix
                }
            });
        }

        protected override void OnUpdate()
        {
            if (m_TrafficUISystem == null || !m_TrafficUISystem.IsTransitPanelActive) return;

            var hiddenSet = new NativeHashSet<Entity>(TrafficUISystem.HiddenCustomRoutes.Count, Allocator.TempJob);
            foreach (var e in TrafficUISystem.HiddenCustomRoutes) hiddenSet.Add(e);

            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle deps);

            var transitJob = new RenderTransitLineOverlayJob
            {
                overlayBuffer = buffer,
                EntityType = SystemAPI.GetEntityTypeHandle(),
                ColorType = SystemAPI.GetComponentTypeHandle<Game.Routes.Color>(true),
                SegmentBufferType = SystemAPI.GetBufferTypeHandle<RouteSegment>(true),
                PathElementLookup = SystemAPI.GetBufferLookup<PathElement>(true),
                CurveLookup = SystemAPI.GetComponentLookup<Curve>(true),
                PrefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                TransportLineDataLookup = SystemAPI.GetComponentLookup<TransportLineData>(true),
                HiddenRoutes = hiddenSet,
                WaypointBufferType = SystemAPI.GetBufferTypeHandle<Game.Routes.RouteWaypoint>(true),
                TransformLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                DrawStops = TrafficUISystem.ShowStopsAndStations,
                ConnectedLookup = SystemAPI.GetComponentLookup<Game.Routes.Connected>(true),
                ZoomLevel = m_CameraUpdateSystem.zoom 
            };

            JobHandle transitHandle = transitJob.Schedule(m_TransitLinesQuery, JobHandle.CombineDependencies(Dependency, deps));
            hiddenSet.Dispose(transitHandle);
            Dependency = transitHandle;
            m_OverlayRenderSystem.AddBufferWriter(Dependency);
        }

        // Wrapper methods for TrafficRouteSystem compatibility
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
            public Buffer(OverlayRenderSystem.Buffer buffer) { m_Buffer = buffer; }
            public void DrawCurve(Color color, Bezier4x3 curve, float width, float2 roundness)
            { m_Buffer.DrawCurve(color, curve, width, roundness); }
            public void DrawLine(Color color, Line3.Segment line, float width)
            { m_Buffer.DrawLine(color, line, width); }
        }
    }
}
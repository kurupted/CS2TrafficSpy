using Game.Common;
using Game.Rendering;
using Game.Buildings;
using Game.Objects;
using Game.Prefabs;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using TrafficSpy.Jobs;
using TrafficSpy.Systems; // Needed to see TrafficUISystem & TrafficRenderData

namespace TrafficSpy.Systems
{
    public partial class OriginDestRenderSystem : SystemBase
    {
        private OverlayRenderSystem overlayRenderSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            this.overlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
        }

        protected override void OnUpdate()
        {
            if (this.overlayRenderSystem == null) return;

            // ACCESSING THE STATIC LIST HERE
            var renderList = TrafficUISystem.CurrentRenderList;
            if (renderList == null || renderList.Count == 0) return;

            OverlayRenderSystem.Buffer buffer = this.overlayRenderSystem.GetBuffer(out JobHandle dependencies);

            NativeList<TrafficRenderData> nativeList = new NativeList<TrafficRenderData>(renderList.Count, Allocator.TempJob);
            foreach (var item in renderList) nativeList.Add(item);

            RenderOverlaysJob job = new RenderOverlaysJob
            {
                overlayBuffer = buffer,
                renderList = nativeList,
                transformLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                prefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                objectGeometryDataLookup = SystemAPI.GetComponentLookup<ObjectGeometryData>(true)
            };

            JobHandle jobHandle = job.Schedule(dependencies);

            nativeList.Dispose(jobHandle);

            this.overlayRenderSystem.AddBufferWriter(jobHandle);
        }
    }
}
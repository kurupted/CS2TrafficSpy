using Game.Common;
using Game.Rendering;
using Game.Buildings;
using Game.Objects;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Traffic_Explorer.Jobs;

namespace Traffic_Explorer.Systems
{
    public partial class OriginDestRenderSystem : SystemBase
    {
        private OverlayRenderSystem overlayRenderSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            this.overlayRenderSystem = World.GetExistingSystemManaged<OverlayRenderSystem>();
        }

        protected override void OnUpdate()
        {
            var originList = TrafficUISystem.CurrentOrigins;
            var destList = TrafficUISystem.CurrentDestinations;

            if (originList.Count == 0 && destList.Count == 0) return;

            OverlayRenderSystem.Buffer buffer = this.overlayRenderSystem.GetBuffer(out JobHandle dependencies);

            NativeList<Entity> nativeOrigins = new NativeList<Entity>(originList.Count, Allocator.TempJob);
            NativeList<Entity> nativeDestinations = new NativeList<Entity>(destList.Count, Allocator.TempJob);

            foreach (var e in originList) nativeOrigins.Add(e);
            foreach (var e in destList) nativeDestinations.Add(e);

            RenderOverlaysJob job = new RenderOverlaysJob
            {
                overlayBuffer = buffer,
                origins = nativeOrigins,
                destinations = nativeDestinations,
                // FIXED: Explicitly getting Game.Objects.Transform
                transformLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                renterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true)
            };

            JobHandle jobHandle = job.Schedule(dependencies);

            nativeOrigins.Dispose(jobHandle);
            nativeDestinations.Dispose(jobHandle);

            this.overlayRenderSystem.AddBufferWriter(jobHandle);
        }
    }
}
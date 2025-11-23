using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Rendering;
using TrafficSpy.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

// FIX: Match the namespace of TrafficUISystem
namespace TrafficSpy.Systems
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
            // Now this will find TrafficUISystem because they are in the same namespace
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
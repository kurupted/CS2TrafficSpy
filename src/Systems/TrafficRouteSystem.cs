using Game;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Rendering;
using Game.Vehicles;
using TrafficSpy.Jobs;
using TrafficSpy.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TrafficSpy.Systems
{
    [UpdateAfter(typeof(TrafficUISystem))]
    public partial class TrafficRouteSystem : SystemBase
    {
        private SimpleOverlayRendererSystem overlayRenderSystem;
        private TrafficUISystem trafficUISystem;

        // CHANGED: List now holds struct with Type info
        private NativeList<EntityRouteInput> entityInputList;

        protected override void OnCreate()
        {
            base.OnCreate();
            overlayRenderSystem = World.GetExistingSystemManaged<SimpleOverlayRendererSystem>();
            trafficUISystem = World.GetExistingSystemManaged<TrafficUISystem>();
            entityInputList = new NativeList<EntityRouteInput>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (entityInputList.IsCreated) entityInputList.Dispose();
        }

        protected override void OnUpdate()
        {
            if (!trafficUISystem.ShowRoutes) return;

            var renderList = TrafficUISystem.CurrentRenderList;
            if (renderList == null || renderList.Count == 0) return;

            // 1. Prepare Data
            entityInputList.Clear();
            if (entityInputList.Capacity < renderList.Count) entityInputList.Capacity = renderList.Count;

            foreach (var item in renderList)
            {
                if (EntityManager.Exists(item.entity))
                {
                    // PASS THE TYPE HERE
                    // 2 = Pedestrian, 4 = Vehicle
                    byte type = item.isPedestrian ? (byte)2 : (byte)4;
                    
                    entityInputList.Add(new EntityRouteInput 
                    { 
                        entity = item.entity, 
                        type = type 
                    });
                }
            }

            if (entityInputList.Length == 0) return;

            // 2. Set up the Calculation Job
            int batchSize = 32;
            int batchCount = (entityInputList.Length + batchSize - 1) / batchSize;
            
            NativeArray<NativeHashMap<CurveDef, int>> jobResults = 
                new NativeArray<NativeHashMap<CurveDef, int>>(batchCount, Allocator.TempJob);

            for (int i = 0; i < batchCount; i++)
            {
                jobResults[i] = new NativeHashMap<CurveDef, int>(100, Allocator.TempJob);
            }

            CalculateEntityPathsJob calcJob = new CalculateEntityPathsJob
            {
                input = entityInputList,
                batchSize = batchSize,
                results = jobResults,
                pathOwnerLookup = GetComponentLookup<PathOwner>(true),
                curveLookup = GetComponentLookup<Curve>(true),
                pathElementLookup = GetBufferLookup<PathElement>(true),
                carNavigationLaneSegmentLookup = GetBufferLookup<CarNavigationLane>(true),
                carLaneLookup = GetComponentLookup<CarCurrentLane>(true),
                humanLaneLookup = GetComponentLookup<HumanCurrentLane>(true)
            };

            JobHandle calcHandle = calcJob.ScheduleBatch(entityInputList.Length, batchSize, Dependency);

            RenderRouteOverlayJob renderJob = new RenderRouteOverlayJob
            {
                curveData = jobResults,
                overlayBuffer = overlayRenderSystem.GetBuffer(out JobHandle renderDependency)
            };

            JobHandle finalHandle = renderJob.Schedule(JobHandle.CombineDependencies(calcHandle, renderDependency));

            for (int i = 0; i < batchCount; i++)
            {
                jobResults[i].Dispose(finalHandle);
            }
            jobResults.Dispose(finalHandle);

            overlayRenderSystem.AddBufferWriter(finalHandle);
            Dependency = finalHandle;
        }
    }
}
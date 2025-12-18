using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Objects;
using Game.Vehicles;
using TrafficSpy.Jobs;
using TrafficSpy.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

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

            // Get distance filtering params from UI System
            float3 centerPos = TrafficUISystem.FilterPosition;
            float maxDistSq = TrafficUISystem.FilterDistance * TrafficUISystem.FilterDistance;
            bool checkDistance = TrafficUISystem.FilterDistance < 1000000f; // If not "Unlimited"
            
            ComponentLookup<Transform> transformLookup = GetComponentLookup<Transform>(true);

            foreach (var item in renderList)
            {
                if (EntityManager.Exists(item.entity))
                {
                    // Distance Check
                    if (checkDistance)
                    {
                        if (transformLookup.TryGetComponent(item.entity, out Transform trans))
                        {
                            if (math.distancesq(trans.m_Position, centerPos) > maxDistSq)
                            {
                                continue; // Skip if too far
                            }
                        }
                    }

                    // PASS THE TYPE HERE
                    // 2 = Pedestrian, 4 = Vehicle
                    byte type = 4; // Default Vehicle
                    
                    if (item.isPedestrian) 
                    {
                        type = 2;
                    }
                    else if (item.isVehicle)
                    {
                        type = 4;
                    }
                    else if (item.type == TrafficType.Citizen) 
                    {
                        // Fallback: If it's a citizen but 'isPedestrian' wasn't set (e.g. waiting), treat as ped
                        type = 2;
                    }
                    
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
                overlayBuffer = overlayRenderSystem.GetBuffer(out JobHandle renderDependency),
                maxVehicleTraffic = ModSettings.ModSettings.Instance.MaxVehicleTraffic,
                maxPedestrianTraffic = ModSettings.ModSettings.Instance.MaxPedestrianTraffic
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
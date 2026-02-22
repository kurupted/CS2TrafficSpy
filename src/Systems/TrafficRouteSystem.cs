using Game;
using Game.Common;
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
using UnityEngine;
using Transform = Game.Objects.Transform;

namespace TrafficSpy.Systems
{
    [UpdateAfter(typeof(TrafficUISystem))]
    public partial class TrafficRouteSystem : SystemBase
    {
        private SimpleOverlayRendererSystem overlayRenderSystem;
        private TrafficUISystem trafficUISystem;
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

            entityInputList.Clear();
            if (entityInputList.Capacity < renderList.Count) entityInputList.Capacity = renderList.Count;

            // Range filtering is now handled by TrafficUISystem.CurrentRenderList

            foreach (var item in renderList)
            {
                Entity routeTarget = item.sourceAgent != Entity.Null ? item.sourceAgent : item.entity;
                if (EntityManager.Exists(routeTarget))
                {
                    byte type = 4; // Default Vehicle
                    
                    if (item.isPedestrian) type = 2;
                    else if (item.isVehicle) type = 4;
                    else if (item.type == TrafficType.Citizen) type = 2;

                    entityInputList.Add(new EntityRouteInput 
                    { 
                        entity = routeTarget, 
                        type = type 
                    });
                }
            }

            if (entityInputList.Length == 0) return;

            // 1. Pre-Pass: Count agents per Lane (for Heatmaps)
            NativeHashMap<Entity, int> laneCounts = new NativeHashMap<Entity, int>(1000, Allocator.TempJob);
            
            CountLanesJob countJob = new CountLanesJob
            {
                input = entityInputList,
                laneCounts = laneCounts,
                carLaneLookup = GetComponentLookup<CarCurrentLane>(true),
                humanLaneLookup = GetComponentLookup<HumanCurrentLane>(true)
            };
            
            // Single threaded is fast enough and avoids concurrent write issues easily
            JobHandle countHandle = countJob.Schedule(Dependency);

            // 2. Calculate Geometry
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
                laneCounts = laneCounts, // Pass the counts
                batchSize = batchSize,
                results = jobResults,
                pathOwnerLookup = GetComponentLookup<PathOwner>(true),
                curveLookup = GetComponentLookup<Curve>(true),
                pathElementLookup = GetBufferLookup<PathElement>(true),
                carNavigationLaneSegmentLookup = GetBufferLookup<CarNavigationLane>(true),
                carLaneLookup = GetComponentLookup<CarCurrentLane>(true),
                humanLaneLookup = GetComponentLookup<HumanCurrentLane>(true),
                vehicleLookup = GetComponentLookup<Game.Vehicles.Vehicle>(true),
                trainLaneLookup = GetComponentLookup<Game.Vehicles.TrainCurrentLane>(true),
                watercraftLaneLookup = GetComponentLookup<Game.Vehicles.WatercraftCurrentLane>(true),
                transformLookup = GetComponentLookup<Transform>(true)
            };

            JobHandle calcHandle = calcJob.ScheduleBatch(entityInputList.Length, batchSize, countHandle);

            RenderRouteOverlayJob renderJob = new RenderRouteOverlayJob
            {
                curveData = jobResults,
                overlayBuffer = overlayRenderSystem.GetBuffer(out JobHandle renderDependency),
                maxVehicleTraffic = ModSettings.ModSettings.Instance.MaxVehicleTraffic,
                maxPedestrianTraffic = ModSettings.ModSettings.Instance.MaxPedestrianTraffic,
                alphaMultiplier = TrafficSpy.ModSettings.ModSettings.Instance.RouteOpacity / 100f
            };

            JobHandle finalHandle = renderJob.Schedule(JobHandle.CombineDependencies(calcHandle, renderDependency));

            for (int i = 0; i < batchCount; i++)
            {
                jobResults[i].Dispose(finalHandle);
            }
            jobResults.Dispose(finalHandle);
            laneCounts.Dispose(finalHandle);

            overlayRenderSystem.AddBufferWriter(finalHandle);
            Dependency = finalHandle;
        }
    }
}
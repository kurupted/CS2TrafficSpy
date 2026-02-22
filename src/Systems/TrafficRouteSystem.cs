using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Vehicles;
using TrafficSpy.Jobs;
using TrafficSpy.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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
            if (TrafficUISystem.AnalyzedLanes != null && TrafficUISystem.AnalyzedLanes.Count > 0)
            {
                NativeArray<Entity> selectedLanes = new NativeArray<Entity>(TrafficUISystem.AnalyzedLanes.Count, Allocator.TempJob);
                int laneIndex = 0;
                foreach (var e in TrafficUISystem.AnalyzedLanes) 
                {
                    selectedLanes[laneIndex++] = e;
                }

                DrawCustomOutlineJob outlineJob = new DrawCustomOutlineJob
                {
                    selectedEntities = selectedLanes,
                    curveLookup = GetComponentLookup<Game.Net.Curve>(true),
                    transformLookup = GetComponentLookup<Game.Objects.Transform>(true),
            
                    prefabRefLookup = GetComponentLookup<Game.Prefabs.PrefabRef>(true),
                    objectGeomLookup = GetComponentLookup<Game.Prefabs.ObjectGeometryData>(true),
                    netGeomLookup = GetComponentLookup<Game.Prefabs.NetGeometryData>(true),
                    nodeLookup = GetComponentLookup<Game.Net.Node>(true),
                    edgeLookup = GetComponentLookup<Game.Net.Edge>(true),
                    subLaneBufferLookup = GetBufferLookup<Game.Net.SubLane>(true),
                    
                    overlayBuffer = overlayRenderSystem.GetBuffer(out JobHandle outlineDep)
                };

                JobHandle outlineHandle = outlineJob.Schedule(JobHandle.CombineDependencies(Dependency, outlineDep));
                overlayRenderSystem.AddBufferWriter(outlineHandle);
                Dependency = outlineHandle; 
            }
            
            // If the UI toggle is off, or no agents are routed, we stop here.
            if (!trafficUISystem.ShowRoutes) return;
            var renderList = TrafficUISystem.CurrentRenderList;
            if (renderList == null || renderList.Count == 0) return;
            
            entityInputList.Clear();
            if (entityInputList.Capacity < renderList.Count) entityInputList.Capacity = renderList.Count;

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
                input = entityInputList.AsArray(),
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
                input = entityInputList.AsArray(),
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
    
    
    
    
    
    
[Unity.Burst.BurstCompile]
public struct DrawCustomOutlineJob : IJob
{
    [DeallocateOnJobCompletion] public NativeArray<Entity> selectedEntities;
    [ReadOnly] public ComponentLookup<Game.Net.Curve> curveLookup;
    [ReadOnly] public ComponentLookup<Game.Objects.Transform> transformLookup;
    
    [ReadOnly] public ComponentLookup<Game.Prefabs.PrefabRef> prefabRefLookup;
    [ReadOnly] public ComponentLookup<Game.Prefabs.ObjectGeometryData> objectGeomLookup;
    [ReadOnly] public ComponentLookup<Game.Prefabs.NetGeometryData> netGeomLookup;
    [ReadOnly] public ComponentLookup<Game.Net.Node> nodeLookup;
    [ReadOnly] public ComponentLookup<Game.Net.Edge> edgeLookup;
    [ReadOnly] public BufferLookup<Game.Net.SubLane> subLaneBufferLookup;
    
    public SimpleOverlayRendererSystem.Buffer overlayBuffer;

    public void Execute()
    {
        UnityEngine.Color spyCyan = new UnityEngine.Color(0f, 215f / 255f, 1f, 0.7f);
        float lineWidth = 0.5f;

        // 1. Build a fast lookup set for selected entities
        Unity.Collections.NativeHashSet<Entity> selectedSet = new Unity.Collections.NativeHashSet<Entity>(selectedEntities.Length, Unity.Collections.Allocator.Temp);
        for (int i = 0; i < selectedEntities.Length; i++)
        {
            selectedSet.Add(selectedEntities[i]);
        }

        for (int i = 0; i < selectedEntities.Length; i++)
        {
            Entity e = selectedEntities[i];

            // 2. BUILDINGS / STOPS
            if (transformLookup.TryGetComponent(e, out Game.Objects.Transform transform) &&
                prefabRefLookup.TryGetComponent(e, out Game.Prefabs.PrefabRef prefabRef))
            {
                if (objectGeomLookup.TryGetComponent(prefabRef.m_Prefab, out Game.Prefabs.ObjectGeometryData geom))
                {
                    float halfX = geom.m_Size.x * 0.5f;
                    float halfZ = geom.m_Size.z * 0.5f;

                    Unity.Mathematics.float3 c1 = transform.m_Position + Unity.Mathematics.math.mul(transform.m_Rotation, new Unity.Mathematics.float3(-halfX, 0, -halfZ));
                    Unity.Mathematics.float3 c2 = transform.m_Position + Unity.Mathematics.math.mul(transform.m_Rotation, new Unity.Mathematics.float3(-halfX, 0, halfZ));
                    Unity.Mathematics.float3 c3 = transform.m_Position + Unity.Mathematics.math.mul(transform.m_Rotation, new Unity.Mathematics.float3(halfX, 0, halfZ));
                    Unity.Mathematics.float3 c4 = transform.m_Position + Unity.Mathematics.math.mul(transform.m_Rotation, new Unity.Mathematics.float3(halfX, 0, -halfZ));

                    overlayBuffer.DrawLine(spyCyan, new Colossal.Mathematics.Line3.Segment(c1, c2), lineWidth);
                    overlayBuffer.DrawLine(spyCyan, new Colossal.Mathematics.Line3.Segment(c2, c3), lineWidth);
                    overlayBuffer.DrawLine(spyCyan, new Colossal.Mathematics.Line3.Segment(c3, c4), lineWidth);
                    overlayBuffer.DrawLine(spyCyan, new Colossal.Mathematics.Line3.Segment(c4, c1), lineWidth);
                    continue;
                }
            }

            // 3. INTERSECTIONS (Nodes)
            if (nodeLookup.TryGetComponent(e, out Game.Net.Node node))
            {
                int segments = 24;
                float radius = 10.0f; 
                Unity.Mathematics.float3 center = node.m_Position; 

                for (int j = 0; j < segments; j++)
                {
                    float angle1 = (j / (float)segments) * Unity.Mathematics.math.PI * 2f;
                    float angle2 = ((j + 1) / (float)segments) * Unity.Mathematics.math.PI * 2f;

                    Unity.Mathematics.float3 p1 = center + new Unity.Mathematics.float3(Unity.Mathematics.math.cos(angle1) * radius, 0, Unity.Mathematics.math.sin(angle1) * radius);
                    Unity.Mathematics.float3 p2 = center + new Unity.Mathematics.float3(Unity.Mathematics.math.cos(angle2) * radius, 0, Unity.Mathematics.math.sin(angle2) * radius);

                    overlayBuffer.DrawLine(spyCyan, new Colossal.Mathematics.Line3.Segment(p1, p2), lineWidth);
                }
                continue;
            }

            // 4. ROADS & DIRECTIONS (SMART EDGES)
            if (edgeLookup.HasComponent(e) && curveLookup.TryGetComponent(e, out Game.Net.Curve curve))
            {
                float halfWidth = 4.0f; 
                if (prefabRefLookup.TryGetComponent(e, out Game.Prefabs.PrefabRef netPrefabRef) &&
                    netGeomLookup.TryGetComponent(netPrefabRef.m_Prefab, out Game.Prefabs.NetGeometryData netGeom))
                {
                    halfWidth = netGeom.m_DefaultWidth * 0.5f;
                }

                float minOffset = -halfWidth;
                float maxOffset = halfWidth;

                // SMART OUTLINE: If we only selected SOME sublanes (one direction), tighten the bounding box
                if (subLaneBufferLookup.TryGetBuffer(e, out DynamicBuffer<Game.Net.SubLane> subLanes))
                {
                    float tempMin = float.MaxValue;
                    float tempMax = float.MinValue;
                    bool foundSubLanes = false;

                    Unity.Mathematics.float3 edgePos = Colossal.Mathematics.MathUtils.Position(curve.m_Bezier, 0.5f);
                    Unity.Mathematics.float3 edgeTan = Colossal.Mathematics.MathUtils.Tangent(curve.m_Bezier, 0.5f);
                    Unity.Mathematics.float3 edgeRight = Unity.Mathematics.math.normalize(Unity.Mathematics.math.cross(edgeTan, new Unity.Mathematics.float3(0, 1, 0)));

                    for (int j = 0; j < subLanes.Length; j++)
                    {
                        Entity subLane = subLanes[j].m_SubLane;
                        // Only measure sublanes that the UI says are active/selected
                        if (selectedSet.Contains(subLane) && curveLookup.TryGetComponent(subLane, out Game.Net.Curve laneCurve))
                        {
                            Unity.Mathematics.float3 lanePos = Colossal.Mathematics.MathUtils.Position(laneCurve.m_Bezier, 0.5f);
                            float offset = Unity.Mathematics.math.dot(lanePos - edgePos, edgeRight);
                            
                            if (offset < tempMin) tempMin = offset;
                            if (offset > tempMax) tempMax = offset;
                            foundSubLanes = true;
                        }
                    }

                    if (foundSubLanes)
                    {
                        // Add 1.8m padding (approx 1 lane width / 2) around the outermost selected lanes
                        minOffset = tempMin - 1.8f;
                        maxOffset = tempMax + 1.8f;

                        // Clamp it to the physical road so it doesn't spill over
                        if (minOffset < -halfWidth) minOffset = -halfWidth;
                        if (maxOffset > halfWidth) maxOffset = halfWidth;
                    }
                }

                Unity.Mathematics.float3 dirA = Unity.Mathematics.math.normalize(curve.m_Bezier.b - curve.m_Bezier.a);
                Unity.Mathematics.float3 rightA = Unity.Mathematics.math.cross(dirA, new Unity.Mathematics.float3(0, 1, 0));

                Unity.Mathematics.float3 dirD = Unity.Mathematics.math.normalize(curve.m_Bezier.d - curve.m_Bezier.c);
                Unity.Mathematics.float3 rightD = Unity.Mathematics.math.cross(dirD, new Unity.Mathematics.float3(0, 1, 0));

                Colossal.Mathematics.Bezier4x3 leftCurve = new Colossal.Mathematics.Bezier4x3(
                    curve.m_Bezier.a + (rightA * minOffset), 
                    curve.m_Bezier.b + (rightA * minOffset), 
                    curve.m_Bezier.c + (rightD * minOffset), 
                    curve.m_Bezier.d + (rightD * minOffset));
                
                Colossal.Mathematics.Bezier4x3 rightCurve = new Colossal.Mathematics.Bezier4x3(
                    curve.m_Bezier.a + (rightA * maxOffset), 
                    curve.m_Bezier.b + (rightA * maxOffset), 
                    curve.m_Bezier.c + (rightD * maxOffset), 
                    curve.m_Bezier.d + (rightD * maxOffset));

                overlayBuffer.DrawCurve(spyCyan, leftCurve, lineWidth, new Unity.Mathematics.float2(1, 1));
                overlayBuffer.DrawCurve(spyCyan, rightCurve, lineWidth, new Unity.Mathematics.float2(1, 1));
                overlayBuffer.DrawLine(spyCyan, new Colossal.Mathematics.Line3.Segment(leftCurve.a, rightCurve.a), lineWidth);
                overlayBuffer.DrawLine(spyCyan, new Colossal.Mathematics.Line3.Segment(leftCurve.d, rightCurve.d), lineWidth);
                continue;
            }
            
        }

        selectedSet.Dispose();
    }
}



}
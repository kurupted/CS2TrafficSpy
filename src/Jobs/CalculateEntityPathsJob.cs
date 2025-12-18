using Colossal.Mathematics;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Objects; // Required for Transform
using Game.Pathfind;
using Game.Vehicles;
using TrafficSpy.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MathUtils = TrafficSpy.Utils.MathUtils;

namespace TrafficSpy.Jobs
{
    public struct EntityRouteInput
    {
        public Entity entity;
        public byte type; // 2 = Pedestrian, 4 = Vehicle
    }

    [BurstCompile]
    public struct CalculateEntityPathsJob : IJobParallelForBatch
    {
        [ReadOnly] public NativeList<EntityRouteInput> input;
        [ReadOnly] public NativeHashMap<Entity, int> laneCounts; // Pre-Pass Data
        
        [ReadOnly] public ComponentLookup<PathOwner> pathOwnerLookup;
        [ReadOnly] public ComponentLookup<Curve> curveLookup;
        [ReadOnly] public BufferLookup<PathElement> pathElementLookup;
        [ReadOnly] public BufferLookup<CarNavigationLane> carNavigationLaneSegmentLookup;
        [ReadOnly] public ComponentLookup<CarCurrentLane> carLaneLookup;
        [ReadOnly] public ComponentLookup<HumanCurrentLane> humanLaneLookup;
        [ReadOnly] public ComponentLookup<Transform> transformLookup;

        public int batchSize;

        [NativeDisableParallelForRestriction]
        public NativeArray<NativeHashMap<CurveDef, int>> results;

        public void Execute(int start, int count)
        {
            int batchIndex = start / batchSize;
            
            for (int i = start; i < start + count; ++i)
            {
                if (i < input.Length)
                {
                    WriteEntityRoute(input[i], batchIndex);
                }
            }
        }

        private void WriteEntityRoute(EntityRouteInput item, int batchIndex)
        {
            Entity entity = item.entity;
            byte agentType = item.type;

            if (!pathOwnerLookup.TryGetComponent(entity, out PathOwner pathOwner)) return;
            if (!pathElementLookup.TryGetBuffer(entity, out DynamicBuffer<PathElement> pathElements)) return;

            // 1. Add Future Path Elements
            // Start loop at 'm_ElementIndex + 1' to exclude the current lane
            for (int i = pathOwner.m_ElementIndex + 1; i < pathElements.Length; ++i)
            {
                PathElement element = pathElements[i];
                if (curveLookup.TryGetComponent(element.m_Target, out Curve curve))
                {
                    // Weight 1, will sum up in the results map
                    Write(new CurveDef(curve.m_Bezier, agentType), batchIndex, 1);
                }
            }

            // 2. Add Current Navigation/Lane Elements (This handles the current lane with cutting)
            AddRouteNavigationCurves(entity, batchIndex, agentType);
        }

        private void AddRouteNavigationCurves(Entity entity, int batchIndex, byte agentType)
        {
            // Cut Navigation Lanes using their specific CurvePosition range
            // This prevents "wrong side" lines when turning into/across lanes.
            if (carNavigationLaneSegmentLookup.TryGetBuffer(entity, out DynamicBuffer<CarNavigationLane> navLanes))
            {
                for (int i = 0; i < navLanes.Length; i++)
                {
                    if (curveLookup.TryGetComponent(navLanes[i].m_Lane, out Curve curve))
                    {
                        // Check if the curve is a "crossing" (very short distance)
                        // Simple check: Distance squared between start and end control points
                        float distSq = math.distancesq(curve.m_Bezier.a, curve.m_Bezier.d);
                        
                        // 16.0f = 4 meters squared. 
                        // Lane connections/merges across a road are usually short.
                        // Standard lanes are usually much longer.
                        if (distSq < 16.0f) 
                        {
                            continue; // Skip crossing/merge lines
                        }

                        Bezier4x3 cutNav = MathUtils.Cut(curve.m_Bezier, navLanes[i].m_CurvePosition);
                        Write(new CurveDef(cutNav, agentType), batchIndex, 1);
                    }
                }
            }

            Entity laneEntity = Entity.Null;
            Curve laneCurve = default;
            
            // We only need the 'T' parameter (0.0 to 1.0) to cut the curve
            float curveStartT = 0f; 
            bool hasLane = false;

            // Get Current Lane and Agent Position
            if (carLaneLookup.TryGetComponent(entity, out CarCurrentLane carLane) 
                && curveLookup.TryGetComponent(carLane.m_Lane, out laneCurve))
            {
                laneEntity = carLane.m_Lane;
                curveStartT = carLane.m_CurvePosition.x; 
                hasLane = true;
            }
            else if (humanLaneLookup.TryGetComponent(entity, out HumanCurrentLane humanLane) 
                && curveLookup.TryGetComponent(humanLane.m_Lane, out laneCurve))
            {
                laneEntity = humanLane.m_Lane;
                curveStartT = humanLane.m_CurvePosition.x;
                hasLane = true;
            }

            if (hasLane)
            {
                // LOOKUP HEATMAP WEIGHT
                int weight = 1;
                if (laneCounts.ContainsKey(laneEntity))
                {
                    weight = laneCounts[laneEntity];
                }

                // CUT THE CURVE (Visual Fix)
                // Draw from current position (x) to end (1.0)
                Bezier4x3 cutBezier = MathUtils.Cut(laneCurve.m_Bezier, curveStartT);

                // Write with FORCED WEIGHT (Color Fix)
                Write(new CurveDef(cutBezier, agentType), batchIndex, weight);
            }
        }

        private void Write(CurveDef resultCurve, int batchIndex, int weight)
        {
            NativeHashMap<CurveDef, int> resultCurves = results[batchIndex];
            if (resultCurves.ContainsKey(resultCurve))
            {
                resultCurves[resultCurve] += weight; 
            }
            else
            {
                resultCurves.Add(resultCurve, weight);
            }
        }
    }
}
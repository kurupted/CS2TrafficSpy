using Colossal.Mathematics;
using Game.Common;
using Game.Creatures;
using Game.Net;
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
        
        [ReadOnly] public ComponentLookup<PathOwner> pathOwnerLookup;
        [ReadOnly] public ComponentLookup<Curve> curveLookup;
        [ReadOnly] public BufferLookup<PathElement> pathElementLookup;
        [ReadOnly] public BufferLookup<CarNavigationLane> carNavigationLaneSegmentLookup;
        [ReadOnly] public ComponentLookup<CarCurrentLane> carLaneLookup;
        [ReadOnly] public ComponentLookup<HumanCurrentLane> humanLaneLookup;

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
            for (int i = pathOwner.m_ElementIndex; i < pathElements.Length; ++i)
            {
                PathElement element = pathElements[i];
                if (curveLookup.TryGetComponent(element.m_Target, out Curve curve))
                {
                    Write(new CurveDef(curve.m_Bezier, agentType), batchIndex);
                }
            }

            // 2. Add Current Navigation/Lane Elements
            AddRouteNavigationCurves(entity, batchIndex, agentType);
        }

        private void AddRouteNavigationCurves(Entity entity, int batchIndex, byte agentType)
        {
            // Handle Car Navigation (Immediate turning lanes etc)
            if (carNavigationLaneSegmentLookup.TryGetBuffer(entity, out DynamicBuffer<CarNavigationLane> navLanes))
            {
                for (int i = 0; i < navLanes.Length; i++)
                {
                    if (curveLookup.TryGetComponent(navLanes[i].m_Lane, out Curve curve))
                    {
                        // Use FULL curve for aggregation
                        Write(new CurveDef(curve.m_Bezier, agentType), batchIndex);
                    }
                }
            }

            // Handle Current Car Lane
            if (carLaneLookup.TryGetComponent(entity, out CarCurrentLane carLane) 
                && curveLookup.TryGetComponent(carLane.m_Lane, out Curve carCurve))
            {
                // Use FULL curve for aggregation (fixes MaxTraffic setting)
                Write(new CurveDef(carCurve.m_Bezier, agentType), batchIndex);
            }

            // Handle Current Pedestrian Lane
            if (humanLaneLookup.TryGetComponent(entity, out HumanCurrentLane humanLane) 
                && curveLookup.TryGetComponent(humanLane.m_Lane, out Curve humanCurve))
            {
                // Use FULL curve for aggregation
                Write(new CurveDef(humanCurve.m_Bezier, agentType), batchIndex);
            }
        }

        private void Write(CurveDef resultCurve, int batchIndex)
        {
            NativeHashMap<CurveDef, int> resultCurves = results[batchIndex];
            if (resultCurves.ContainsKey(resultCurve))
            {
                resultCurves[resultCurve] += 1;
            }
            else
            {
                resultCurves.Add(resultCurve, 1);
            }
        }
    }
}
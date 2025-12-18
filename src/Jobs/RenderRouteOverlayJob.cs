using Colossal.Mathematics;
using Game.Rendering;
using TrafficSpy.Systems;
using TrafficSpy.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public struct RenderRouteOverlayJob : IJob
    {
        public SimpleOverlayRendererSystem.Buffer overlayBuffer;
        
        public float maxVehicleTraffic;
        public float maxPedestrianTraffic;

        [ReadOnly]
        public NativeArray<NativeHashMap<CurveDef, int>> curveData;

        public void Execute()
        {
            NativeHashMap<CurveDef, int> aggregatedCurves = new NativeHashMap<CurveDef, int>(1000, Allocator.Temp);

            for (int i = 0; i < curveData.Length; i++)
            {
                var batchMap = curveData[i];
                foreach (var kvp in batchMap)
                {
                    if (aggregatedCurves.ContainsKey(kvp.Key))
                    {
                        aggregatedCurves[kvp.Key] += kvp.Value;
                    }
                    else
                    {
                        aggregatedCurves.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            foreach (var kvp in aggregatedCurves)
            {
                DrawWeightedCurve(kvp.Key, kvp.Value);
            }

            aggregatedCurves.Dispose();
        }

        private void DrawWeightedCurve(CurveDef curveDef, int weight)
        {
            float baseWidth = 1.0f; 
            float maxAdditionalWidth = 2.5f; 
            float widthMultiplier = 0.2f;

            float width = baseWidth + math.min(weight * widthMultiplier, maxAdditionalWidth);

            float t = 0f;
            
            // Normalize weight based on type
            if (curveDef.type == 2) // Pedestrian
            {
                 t = math.clamp(weight / maxPedestrianTraffic, 0f, 1f);
                 width *= 0.75f; // Peds slightly thinner
            }
            else // Vehicle
            {
                 t = math.clamp(weight / maxVehicleTraffic, 0f, 1f);
            }

            // Shared Heatmap Colors
            Color low = new Color(0f, 1f, 0f, 0.6f);     // Green
            Color mid = new Color(1f, 0.9f, 0f, 0.8f);   // Yellow
            Color high = new Color(1f, 0.2f, 0f, 0.95f);  // Red

            Color color;
            if (t < 0.5f)
            {
                color = Color.Lerp(low, mid, t * 2.0f);
            }
            else
            {
                color = Color.Lerp(mid, high, (t - 0.5f) * 2.0f);
            }

            overlayBuffer.DrawCurve(color, curveDef.curve, width, new float2(1, 1));
        }
    }
}
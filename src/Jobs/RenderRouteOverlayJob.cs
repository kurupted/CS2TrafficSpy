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

            Color color;

            if (curveDef.type == 2) // Pedestrian
            {
                // Changed to Purple/White so it doesn't look like "Green Traffic"
                color = new Color(0.8f, 0.5f, 1.0f, 0.6f); 
                width *= 0.75f;  // Ped paths slightly thinner
            }
            else // Vehicle
            {
                // Heatmap Logic. We normalize the weight against a "Max Capacity" constant.
                float maxTraffic = 50.0f; 
                float t = math.clamp(weight / maxTraffic, 0f, 1f);

                // Standard Traffic Colors: Green -> Yellow -> Red
                Color low = new Color(0f, 1f, 0f, 0.5f);     // Green (Minimal traffic)
                Color mid = new Color(1f, 0.9f, 0f, 0.7f);   // Yellow
                Color high = new Color(1f, 0.2f, 0f, 0.9f);  // Red (Heavy traffic)

                if (t < 0.5f)
                {
                    // Interpolate Green -> Yellow
                    color = Color.Lerp(low, mid, t * 2.0f);
                }
                else
                {
                    // Interpolate Yellow -> Red
                    color = Color.Lerp(mid, high, (t - 0.5f) * 2.0f);
                }
            }

            overlayBuffer.DrawCurve(color, curveDef.curve, width, new float2(1, 1));
        }
    }
}
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
                        // Note: For "Cut" curves, this will likely only see 1 entry with high weight
                        // For "Future" curves, this will see many entries with weight 1, summing up.
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
            float maxAdditionalWidth = 2.2f; 
            float widthMultiplier = 0.2f;

            float width = baseWidth + math.min(weight * widthMultiplier, maxAdditionalWidth);
            float t = 0f;
            
            if (curveDef.type == 2) // Pedestrian
            {
                 t = math.clamp(weight / maxPedestrianTraffic, 0f, 1f);
                 width *= 0.75f; 
            }
            else // Vehicle
            {
                 t = math.clamp(weight / maxVehicleTraffic, 0f, 1f);
            }

            Color cCyan = new Color(0f, 1f, 1f, 0.5f);
            Color cYellow = new Color(1f, 0.9f, 0f, 0.8f);
            Color cRed = new Color(1f, 0.2f, 0f, 0.95f);

            Color color;
            
            if (t < 0.5f)
            {
                // Lerp Cyan -> Yellow
                float localT = t * 2.0f;
                color = Color.Lerp(cCyan, cYellow, localT);
            }
            else
            {
                // Lerp Yellow -> Red
                float localT = (t - 0.5f) * 2.0f;
                color = Color.Lerp(cYellow, cRed, localT);
            }

            overlayBuffer.DrawCurve(color, curveDef.curve, width, new float2(1, 1));
        }
    }
}
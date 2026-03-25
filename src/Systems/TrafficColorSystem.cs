using Colossal.Collections;
using Game;
using Game.Common;
using Game.Objects;
using Game.Rendering;
using Game.Tools;
using HarmonyLib;
using System.Reflection;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TrafficSpy.Systems
{
    public partial class TrafficColorSystem : GameSystemBase
    {
        private static TrafficColorSystem _instance;
        private TrafficUISystem _trafficUISystem;
        
        private const string HarmonyID = "TrafficSpy.TrafficColorSystem";

        [BurstCompile]
        private struct UpdateColorsJobDefault : IJobChunk
        {
            public ComponentTypeHandle<Color> ComponentTypeHandleColor;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Color> colors = chunk.GetNativeArray(ref ComponentTypeHandleColor);
                for (int i = 0; i < colors.Length; i++)
                {
                    Color color = colors[i];
                    color.m_Index = 0;
                    color.m_Value = 0;
                    colors[i] = color;
                }
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _instance = this;
            _trafficUISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();

            // Intercept the vanilla color system
            MethodInfo originalMethod = typeof(ObjectColorSystem).GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo prefixMethod = typeof(TrafficColorSystem).GetMethod(nameof(OnUpdatePrefix), BindingFlags.Static | BindingFlags.NonPublic);
            
            if (originalMethod != null && prefixMethod != null)
            {
                new Harmony(HarmonyID).Patch(originalMethod, new HarmonyMethod(prefixMethod));
            }
        }

        protected override void OnUpdate() { }

        private static bool OnUpdatePrefix()
        {
            return _instance.OnUpdateImpl();
        }

        private bool OnUpdateImpl()
        {
            // If the transit panel is CLOSED, return true to let vanilla handle colors normally
            bool isTransitViewActive = _trafficUISystem != null && _trafficUISystem.IsTransitPanelActive;
            if (!isTransitViewActive) return true;

            // If the transit panel is OPEN, wipe all colors from the map
            EntityQuery queryDefault = SystemAPI.QueryBuilder()
                .WithAllRW<Color>()
                .WithAll<Object>()
                .WithNone<Hidden, Deleted>()
                .Build();

            UpdateColorsJobDefault jobDefault = new UpdateColorsJobDefault()
            {
                ComponentTypeHandleColor = SystemAPI.GetComponentTypeHandle<Color>(false)
            };

            JobHandle defaultHandle = JobChunkExtensions.ScheduleParallel(jobDefault, queryDefault, this.Dependency);
            this.Dependency = defaultHandle;

            // Return false to BLOCK the vanilla ObjectColorSystem from running
            return false; 
        }
    }
}
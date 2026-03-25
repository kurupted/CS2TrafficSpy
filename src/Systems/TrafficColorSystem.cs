using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Rendering;
using Game.Routes;
using Game.Tools;
using TrafficSpy.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TrafficSpy.Systems
{
    // We update after ObjectColorSystem so we can override the game's default coloring
    [UpdateAfter(typeof(Game.Rendering.ObjectColorSystem))]
    public partial class TrafficColorSystem : GameSystemBase
    {
        private EntityQuery m_TargetQuery;
        private TrafficUISystem m_UISystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_UISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();
            
            // Look for any object that is a stop or a station that has a color component
            m_TargetQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<Game.Objects.Color>() },
                Any = new[] { ComponentType.ReadOnly<TransportStop>(), ComponentType.ReadOnly<TransportStation>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }

        protected override void OnUpdate()
        {
            // Only apply the gray-out effect if the custom transit panel is open
            if (!m_UISystem.IsTransitPanelActive) return;

            var job = new GrayTransitStructuresJob
            {
                ColorType = GetComponentTypeHandle<Game.Objects.Color>(false)
            };
            
            Dependency = job.ScheduleParallel(m_TargetQuery, Dependency);
        }

        [BurstCompile]
        struct GrayTransitStructuresJob : IJobChunk
        {
            public ComponentTypeHandle<Game.Objects.Color> ColorType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ColorType);
                
                for (int i = 0; i < colors.Length; i++)
                {
                    Game.Objects.Color color = colors[i];
                    
                    // Setting m_Index to 0 uses the game's default palette
                    color.m_Index = 0; 
                    
                    // Value 128 is roughly 50% brightness (Gray)
                    color.m_Value = 128; 
                    
                    colors[i] = color;
                }
            }
        }
    }
}
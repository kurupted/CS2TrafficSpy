using Game;
using Game.Buildings;
using Game.Citizens; // Fixes ResidentialProperty symbol
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Rendering;
using Game.Routes;
using Game.Tools; // Fixes Temp component symbol
using TrafficSpy.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections; // Fixes Allocator/NativeArray symbols
using Unity.Entities;
using Unity.Jobs;

namespace TrafficSpy.Systems
{
    [UpdateAfter(typeof(ObjectColorSystem))]
    public partial class TrafficColorSystem : GameSystemBase
    {
        private EntityQuery m_TransitStructuresQuery;
        private EntityQuery m_BackgroundQuery;
        private TrafficUISystem m_UISystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_UISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();

            // Query for stops and stations
            m_TransitStructuresQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<Game.Objects.Color>() },
                Any = new[] { ComponentType.ReadOnly<TransportStop>(), ComponentType.ReadOnly<TransportStation>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            // Query for city background (Buildings/Roads) excluding transit structures
            m_BackgroundQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<Game.Objects.Color>() },
                Any = new[] { ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Edge>() },
                None = new[]
                {
                    ComponentType.ReadOnly<TransportStop>(),
                    ComponentType.ReadOnly<TransportStation>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (!m_UISystem.IsTransitPanelActive) return;

            // Job for Transit structures (Stops/Stations) - Bright White
            var transitJob = new ColorJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                ColorType = SystemAPI.GetComponentTypeHandle<Game.Objects.Color>(false),
                Mode = 0, 
                UseZoneColors = false
            };

            // Job for Background city (Buildings/Roads) - Dark or Zone colors
            var bgJob = new ColorJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                ColorType = SystemAPI.GetComponentTypeHandle<Game.Objects.Color>(false),
                Mode = 1,
                UseZoneColors = m_UISystem.UseZoneColors,
                ResidentialLookup = SystemAPI.GetComponentLookup<ResidentialProperty>(true),
                CommercialLookup = SystemAPI.GetComponentLookup<CommercialProperty>(true),
                IndustrialLookup = SystemAPI.GetComponentLookup<IndustrialProperty>(true)
            };

            JobHandle transitHandle = transitJob.ScheduleParallel(m_TransitStructuresQuery, Dependency);
            JobHandle bgHandle = bgJob.ScheduleParallel(m_BackgroundQuery, Dependency);
            Dependency = JobHandle.CombineDependencies(transitHandle, bgHandle);
        }

        [BurstCompile]
        struct ColorJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public ComponentTypeHandle<Game.Objects.Color> ColorType;
            public int Mode; // 0 = Station/Stop, 1 = Background/City
            public bool UseZoneColors;

            [ReadOnly] public ComponentLookup<ResidentialProperty> ResidentialLookup;
            [ReadOnly] public ComponentLookup<CommercialProperty> CommercialLookup;
            [ReadOnly] public ComponentLookup<IndustrialProperty> IndustrialLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityType);
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ColorType);

                for (int i = 0; i < colors.Length; i++)
                {
                    Entity e = entities[i];
                    if (Mode == 0)
                    {
                        colors[i] = new Game.Objects.Color { m_Index = 0, m_Value = 255 };
                    }
                    else
                    {
                        byte colorValue = 20; // Very dark background
                        byte colorIndex = 0;

                        if (UseZoneColors)
                        {
                            if (ResidentialLookup.HasComponent(e)) { colorIndex = 1; colorValue = 50; }
                            else if (CommercialLookup.HasComponent(e)) { colorIndex = 2; colorValue = 50; }
                            else if (IndustrialLookup.HasComponent(e)) { colorIndex = 3; colorValue = 50; }
                        }
                        colors[i] = new Game.Objects.Color { m_Index = colorIndex, m_Value = colorValue };
                    }
                }
            }
        }
    }
}
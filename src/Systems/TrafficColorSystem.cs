using Game;
using Game.Buildings;
using Game.Common;
using Game.Net;
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

namespace TrafficSpy.Systems
{
    [UpdateAfter(typeof(ObjectColorSystem))]
    public partial class TrafficColorSystem : GameSystemBase
    {
        private EntityQuery m_TransitStructuresQuery;
        private EntityQuery m_BackgroundQuery;
        private TrafficUISystem m_UISystem;

        // src/Systems/TrafficColorSystem.cs

        protected override void OnCreate()
        {
            base.OnCreate();
            m_UISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();

            m_TransitStructuresQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<Game.Objects.Color>() },
                // Use full namespaces to avoid ambiguity
                Any = new[] { 
                    ComponentType.ReadOnly<Game.Routes.TransportStop>(), 
                    ComponentType.ReadOnly<Game.Buildings.TransportStation>(),
                    ComponentType.ReadOnly<Game.Buildings.TransportDepot>()
                },
                None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });

            m_BackgroundQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<Game.Objects.Color>() },
                Any = new[] { ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Game.Net.Edge>() },
                None = new[]
                {
                    ComponentType.ReadOnly<Game.Routes.TransportStop>(),
                    ComponentType.ReadOnly<Game.Buildings.TransportStation>(),
                    ComponentType.ReadOnly<Game.Buildings.TransportDepot>(),
                    ComponentType.ReadOnly<Game.Common.Deleted>(),
                    ComponentType.ReadOnly<Game.Tools.Temp>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (!m_UISystem.IsTransitPanelActive) return;

            // Transit Job: Assigns correct index based on entity type
            var transitJob = new TransitHighlightJob
            {
                ColorType = SystemAPI.GetComponentTypeHandle<Game.Objects.Color>(false),
                EntityType = SystemAPI.GetEntityTypeHandle(),
                StationLookup = SystemAPI.GetComponentLookup<Game.Buildings.TransportStation>(true),
                DepotLookup = SystemAPI.GetComponentLookup<Game.Buildings.TransportDepot>(true),
                StopLookup = SystemAPI.GetComponentLookup<Game.Routes.TransportStop>(true)
            };

            // Background Job: Dims everything else
            var bgJob = new BackgroundDimJob
            {
                ColorType = SystemAPI.GetComponentTypeHandle<Game.Objects.Color>(false)
            };

            JobHandle transitHandle = transitJob.ScheduleParallel(m_TransitStructuresQuery, Dependency);
            JobHandle bgHandle = bgJob.ScheduleParallel(m_BackgroundQuery, Dependency);

            Dependency = JobHandle.CombineDependencies(transitHandle, bgHandle);
        }

        struct TransitHighlightJob : IJobChunk
        {
            public ComponentTypeHandle<Game.Objects.Color> ColorType;
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentLookup<Game.Buildings.TransportStation> StationLookup;
            [ReadOnly] public ComponentLookup<Game.Buildings.TransportDepot> DepotLookup;
            [ReadOnly] public ComponentLookup<Game.Routes.TransportStop> StopLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ColorType);
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityType);

                for (int i = 0; i < colors.Length; i++)
                {
                    Entity e = entities[i];
            
                    // MAGIC FIX: Use 1-based indices to match the Infomode toggles
                    if (StationLookup.HasComponent(e) || StopLookup.HasComponent(e)) {
                        colors[i] = new Game.Objects.Color { m_Index = 1, m_Value = 255 }; // Stations
                    }
                    else if (DepotLookup.HasComponent(e)) {
                        colors[i] = new Game.Objects.Color { m_Index = 2, m_Value = 255 }; // Depots
                    }
                }
            }
        }

        [BurstCompile]
        struct BackgroundDimJob : IJobChunk
        {
            public ComponentTypeHandle<Game.Objects.Color> ColorType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ColorType);
                for (int i = 0; i < colors.Length; i++)
                {
                    // Dark blueprint background
                    colors[i] = new Game.Objects.Color { m_Index = 0, m_Value = 25 };
                }
            }
        }
    }
}
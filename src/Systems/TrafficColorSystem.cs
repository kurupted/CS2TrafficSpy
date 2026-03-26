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
                StationType = SystemAPI.GetComponentTypeHandle<TransportStation>(true),
                StopType = SystemAPI.GetComponentTypeHandle<TransportStop>(true),
                DepotType = SystemAPI.GetComponentTypeHandle<TransportDepot>(true)
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

        [BurstCompile]
        struct TransitHighlightJob : IJobChunk
        {
            public ComponentTypeHandle<Game.Objects.Color> ColorType;
            [ReadOnly] public ComponentTypeHandle<TransportStation> StationType;
            [ReadOnly] public ComponentTypeHandle<TransportStop> StopType;
            [ReadOnly] public ComponentTypeHandle<TransportDepot> DepotType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ColorType);
                
                // Determine what this chunk contains
                bool isStationOrStop = chunk.Has(ref StationType) || chunk.Has(ref StopType);
                bool isDepot = chunk.Has(ref DepotType);

                // Map to the correct Infomode index defined in SetupCustomInfoview
                byte targetIndex = 0;
                if (isStationOrStop) targetIndex = 1; // Maps to "TrafficSpyStations"
                else if (isDepot) targetIndex = 2;    // Maps to "TrafficSpyDepots"

                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Game.Objects.Color { m_Index = targetIndex, m_Value = 255 };
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
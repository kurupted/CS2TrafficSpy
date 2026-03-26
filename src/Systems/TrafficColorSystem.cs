using Colossal.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TrafficSpy.Systems
{
    // Correct native system to update after
    [UpdateAfter(typeof(ObjectColorSystem))] 
    public partial class TrafficColorSystem : GameSystemBase
    {
        private TrafficUISystem m_TrafficUISystem;
        private EntityQuery m_TransitBuildingQuery;
        private EntityQuery m_ActiveInfomodeQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TrafficUISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();

            // Query for Stations and Depots with the Color component
            m_TransitBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {
                    ComponentType.ReadWrite<Game.Objects.Color>()
                },
                Any = new ComponentType[] {
                    ComponentType.ReadOnly<Game.Buildings.TransportStation>(),
                    ComponentType.ReadOnly<Game.Buildings.TransportDepot>()
                },
                None = new ComponentType[] {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            // The exact query BuildingUse uses to find active infomodes
            m_ActiveInfomodeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<InfoviewBuildingStatusData>(),
                    ComponentType.ReadOnly<InfomodeActive>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (!m_TrafficUISystem.IsTransitPanelActive) return;

            byte stationIndex = 0;
            byte depotIndex = 0;
            bool foundStation = false;
            bool foundDepot = false;

            // 1. Find the dynamic indices mapped to 101 and 102
            var statusDatas = m_ActiveInfomodeQuery.ToComponentDataArray<InfoviewBuildingStatusData>(Allocator.Temp);
            var activeDatas = m_ActiveInfomodeQuery.ToComponentDataArray<InfomodeActive>(Allocator.Temp);

            for (int i = 0; i < statusDatas.Length; i++)
            {
                // Cast the enum to an int to safely compare it with 101 and 102
                int currentType = (int)statusDatas[i].m_Type;

                if (currentType == 101) 
                {
                    stationIndex = (byte)activeDatas[i].m_Index;
                    foundStation = true;
                }
                else if (currentType == 102)
                {
                    depotIndex = (byte)activeDatas[i].m_Index;
                    foundDepot = true;
                }
            }

            statusDatas.Dispose();
            activeDatas.Dispose();

            if (!foundStation && !foundDepot) return;

            // 2. Schedule the Job to apply the indices
            var colorJob = new ColorTransitBuildingsJob
            {
                ColorTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Objects.Color>(false),
                StationTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Buildings.TransportStation>(true),
                DepotTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Buildings.TransportDepot>(true),
                StationIndex = stationIndex,
                DepotIndex = depotIndex,
                HasStationMode = foundStation,
                HasDepotMode = foundDepot
            };

            Dependency = colorJob.ScheduleParallel(m_TransitBuildingQuery, Dependency);
        }

        [BurstCompile]
        private struct ColorTransitBuildingsJob : IJobChunk
        {
            public ComponentTypeHandle<Game.Objects.Color> ColorTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.TransportStation> StationTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.TransportDepot> DepotTypeHandle;

            public byte StationIndex;
            public byte DepotIndex;
            public bool HasStationMode;
            public bool HasDepotMode;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ColorTypeHandle);
                
                bool isDepot = chunk.Has(ref DepotTypeHandle);
                bool isStation = chunk.Has(ref StationTypeHandle);

                byte targetIndex;
                if (isDepot && HasDepotMode) targetIndex = DepotIndex;
                else if (isStation && HasStationMode) targetIndex = StationIndex;
                else return; 

                for (int i = 0; i < colors.Length; i++)
                {
                    var colorComponent = colors[i];
                    
                    // Apply the correct palette index
                    colorComponent.m_Index = targetIndex; 
                    
                    // 255 pushes it to the m_High color defined in your Infomode prefab
                    colorComponent.m_Value = 255; 
                    
                    colors[i] = colorComponent;
                }
            }
        }
    }
}
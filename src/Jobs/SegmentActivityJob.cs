using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Vehicles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Colossal; // Fixed: NativeCounter is here, not Colossal.Collections

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public struct SegmentActivityJob : IJob
    {
        [ReadOnly]
        public Entity selectedSegment;

        [ReadOnly]
        public BufferLookup<Game.Net.SubLane> subLaneLookup;
        [ReadOnly]
        public BufferLookup<Game.Net.LaneObject> laneObjectLookup;

        [ReadOnly]
        public BufferLookup<LayoutElement> layoutElementLookup;
        [ReadOnly]
        public BufferLookup<Passenger> passengerLookup;
        [ReadOnly]
        public ComponentLookup<Controller> controllerLookup;
        [ReadOnly]
        public ComponentLookup<CurrentVehicle> currentVehicleLookup;

        [ReadOnly]
        public ComponentLookup<TravelPurpose> travelPurposeLookup;
        [ReadOnly]
        public ComponentLookup<Target> targetLookup;
        [ReadOnly]
        public ComponentLookup<HouseholdMember> householdMemberLookup;
        [ReadOnly]
        public ComponentLookup<Worker> workerLookup;
        [ReadOnly]
        public ComponentLookup<Game.Citizens.Student> studentLookup;

        public NativeCounter workers;
        public NativeCounter students;
        public NativeCounter shoppers;
        public NativeCounter goingHome;
        public NativeCounter healthcare;
        public NativeCounter other;

        public NativeList<Entity> origins;
        public NativeList<Entity> destinations;

        public void Execute()
        {
            if (!subLaneLookup.HasBuffer(selectedSegment))
            {
                return;
            }

            DynamicBuffer<Game.Net.SubLane> lanes = subLaneLookup[selectedSegment];

            for (int i = 0; i < lanes.Length; i++)
            {
                Entity laneEntity = lanes[i].m_SubLane;

                if (laneObjectLookup.HasBuffer(laneEntity))
                {
                    DynamicBuffer<Game.Net.LaneObject> laneObjects = laneObjectLookup[laneEntity];
                    for (int j = 0; j < laneObjects.Length; j++)
                    {
                        ProcessRootEntity(laneObjects[j].m_LaneObject);
                    }
                }
            }
        }

        private void ProcessRootEntity(Entity entity)
        {
            if (controllerLookup.HasComponent(entity))
            {
                if (layoutElementLookup.HasBuffer(entity))
                {
                    DynamicBuffer<LayoutElement> elements = layoutElementLookup[entity];
                    for (int k = 0; k < elements.Length; k++)
                    {
                        ProcessVehicle(elements[k].m_Vehicle);
                    }
                }
            }
            else if (passengerLookup.HasBuffer(entity))
            {
                ProcessVehicle(entity);
            }
            else if (travelPurposeLookup.HasComponent(entity))
            {
                AnalyzeCitizen(entity);
            }
        }

        private void ProcessVehicle(Entity vehicleEntity)
        {
            if (!passengerLookup.HasBuffer(vehicleEntity)) return;

            DynamicBuffer<Passenger> passengers = passengerLookup[vehicleEntity];
            for (int i = 0; i < passengers.Length; i++)
            {
                AnalyzeCitizen(passengers[i].m_Passenger);
            }
        }

        private void AnalyzeCitizen(Entity citizen)
        {
            // 1. CATEGORIZE ACTIVITY
            // Fixed: Removed 'Eat' and 'GoingToHospital' which are invalid.
            // valid purposes sourced from references: GoingToWork, GoingToSchool, GoingHome, VisitAttractions.
            if (travelPurposeLookup.TryGetComponent(citizen, out TravelPurpose purpose))
            {
                switch (purpose.m_Purpose)
                {
                    case Purpose.GoingToWork:
                    case Purpose.Working:
                        workers.Increment();
                        break;
                    case Purpose.GoingToSchool:
                    case Purpose.Studying:
                        students.Increment();
                        break;
                    case Purpose.GoingHome:
                        goingHome.Increment();
                        break;
                    case Purpose.Shopping: // If Shopping causes error, remove this case. It usually exists but VisitAttractions covers leisure.
                    case Purpose.VisitAttractions:
                    case Purpose.Leisure:
                        shoppers.Increment();
                        break;
                    case Purpose.InHospital:
                        // case Purpose.GoingToHealthcare: // Some versions use this, if error, map to healthcare manually or remove
                        healthcare.Increment();
                        break;
                    default:
                        other.Increment();
                        break;
                }
            }

            // 2. IDENTIFY DESTINATION
            if (targetLookup.TryGetComponent(citizen, out Target dest))
            {
                if (dest.m_Target != Entity.Null) destinations.Add(dest.m_Target);
            }
            else if (currentVehicleLookup.TryGetComponent(citizen, out CurrentVehicle vehicleRef))
            {
                if (targetLookup.TryGetComponent(vehicleRef.m_Vehicle, out Target vehicleDest))
                {
                    if (vehicleDest.m_Target != Entity.Null) destinations.Add(vehicleDest.m_Target);
                }
            }

            // 3. IDENTIFY ORIGIN
            Entity originEntity = Entity.Null;

            if (purpose.m_Purpose == Purpose.GoingHome)
            {
                if (workerLookup.TryGetComponent(citizen, out Worker workerData))
                    originEntity = workerData.m_Workplace;
                else if (studentLookup.TryGetComponent(citizen, out Game.Citizens.Student studentData))
                    originEntity = studentData.m_School;
            }
            else if (purpose.m_Purpose == Purpose.GoingToWork || purpose.m_Purpose == Purpose.GoingToSchool)
            {
                if (householdMemberLookup.TryGetComponent(citizen, out HouseholdMember memberData))
                    originEntity = memberData.m_Household;
            }

            if (originEntity != Entity.Null)
            {
                origins.Add(originEntity);
            }
        }
    }
}
using Colossal;
using Colossal.Collections;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Vehicles;
using TrafficSpy.Systems; // Needed for TrafficRenderData
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public struct SegmentActivityJob : IJob
    {
        [ReadOnly] public Entity selectedSegment;

        [ReadOnly] public BufferLookup<Game.Net.SubLane> subLaneLookup;
        [ReadOnly] public BufferLookup<Game.Net.LaneObject> laneObjectLookup;
        [ReadOnly] public BufferLookup<LayoutElement> layoutElementLookup;
        [ReadOnly] public BufferLookup<Passenger> passengerLookup;
        [ReadOnly] public ComponentLookup<Controller> controllerLookup;
        [ReadOnly] public ComponentLookup<CurrentVehicle> currentVehicleLookup;
        [ReadOnly] public ComponentLookup<TravelPurpose> travelPurposeLookup;
        [ReadOnly] public ComponentLookup<Target> targetLookup;
        [ReadOnly] public ComponentLookup<HouseholdMember> householdMemberLookup;
        [ReadOnly] public ComponentLookup<Worker> workerLookup;
        [ReadOnly] public ComponentLookup<Game.Citizens.Student> studentLookup;
        [ReadOnly] public ComponentLookup<Game.Creatures.Resident> creatureResidentLookup;
        [ReadOnly] public ComponentLookup<PropertyRenter> propertyRenterLookup;

        public NativeCounter workers;
        public NativeCounter students;
        public NativeCounter shoppers;
        public NativeCounter goingHome;
        public NativeCounter healthcare;
        public NativeCounter other;
        public NativeCounter noPurpose;

        public NativeList<TrafficRenderData> results;

        public void Execute()
        {
            if (!subLaneLookup.HasBuffer(selectedSegment)) return;

            DynamicBuffer<Game.Net.SubLane> lanes = subLaneLookup[selectedSegment];
            for (int i = 0; i < lanes.Length; i++)
            {
                Entity laneEntity = lanes[i].m_SubLane;
                if (laneObjectLookup.HasBuffer(laneEntity))
                {
                    DynamicBuffer<Game.Net.LaneObject> laneObjects = laneObjectLookup[laneEntity];
                    for (int j = 0; j < laneObjects.Length; j++)
                        ProcessRootEntity(laneObjects[j].m_LaneObject);
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
                        ProcessVehicle(elements[k].m_Vehicle);
                }
            }
            else if (passengerLookup.HasBuffer(entity))
            {
                ProcessVehicle(entity);
            }
            else
            {
                AnalyzeEntity(entity);
            }
        }

        private void ProcessVehicle(Entity vehicleEntity)
        {
            if (!passengerLookup.HasBuffer(vehicleEntity)) return;

            DynamicBuffer<Passenger> passengers = passengerLookup[vehicleEntity];
            for (int i = 0; i < passengers.Length; i++)
            {
                AnalyzeEntity(passengers[i].m_Passenger);
            }
        }

        private Entity ResolvePhysicalEntity(Entity target)
        {
            if (propertyRenterLookup.TryGetComponent(target, out PropertyRenter renter))
            {
                return renter.m_Property;
            }
            return target;
        }

        private void AnalyzeEntity(Entity entity)
        {
            Entity citizenEntity = entity;
            if (creatureResidentLookup.TryGetComponent(entity, out Game.Creatures.Resident resident))
            {
                citizenEntity = resident.m_Citizen;
            }

            Purpose currentPurpose = Purpose.None;

            if (travelPurposeLookup.TryGetComponent(citizenEntity, out TravelPurpose purpose))
            {
                currentPurpose = purpose.m_Purpose;
                switch (currentPurpose)
                {
                    case Purpose.GoingToWork:
                    case Purpose.Working: workers.Increment(); break;
                    case Purpose.GoingToSchool:
                    case Purpose.Studying: students.Increment(); break;
                    case Purpose.GoingHome: goingHome.Increment(); break;
                    case Purpose.Shopping:
                    case Purpose.VisitAttractions:
                    case Purpose.Leisure: shoppers.Increment(); break;
                    case Purpose.Hospital:
                    case Purpose.InHospital: healthcare.Increment(); break;
                    default: other.Increment(); break;
                }

                Entity originEntity = Entity.Null;
                if (currentPurpose == Purpose.GoingHome)
                {
                    if (workerLookup.TryGetComponent(citizenEntity, out Worker workerData))
                        originEntity = workerData.m_Workplace;
                    else if (studentLookup.TryGetComponent(citizenEntity, out Game.Citizens.Student studentData))
                        originEntity = studentData.m_School;
                }
                else if (currentPurpose == Purpose.GoingToWork || currentPurpose == Purpose.GoingToSchool)
                {
                    if (householdMemberLookup.TryGetComponent(citizenEntity, out HouseholdMember memberData))
                        originEntity = memberData.m_Household;
                }

                if (originEntity != Entity.Null)
                {
                    Entity physicalOrigin = ResolvePhysicalEntity(originEntity);
                    results.Add(new TrafficRenderData { entity = physicalOrigin, purpose = currentPurpose, isOrigin = true });
                }
            }
            else
            {
                noPurpose.Increment();
            }

            Entity rawDest = Entity.Null;
            if (targetLookup.TryGetComponent(entity, out Target dest))
            {
                rawDest = dest.m_Target;
            }
            else if (currentVehicleLookup.TryGetComponent(entity, out CurrentVehicle vehicleRef))
            {
                if (targetLookup.TryGetComponent(vehicleRef.m_Vehicle, out Target vehicleDest))
                {
                    rawDest = vehicleDest.m_Target;
                }
            }

            if (rawDest != Entity.Null)
            {
                Entity physicalDest = ResolvePhysicalEntity(rawDest);
                results.Add(new TrafficRenderData { entity = physicalDest, purpose = currentPurpose, isOrigin = false });
            }
        }
    }
}
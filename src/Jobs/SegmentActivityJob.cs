using Colossal;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Vehicles;
using Game.Buildings;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Colossal.Collections;
using TrafficSpy.Systems;

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
        [ReadOnly] public ComponentLookup<Owner> ownerLookup;
        [ReadOnly] public ComponentLookup<Building> buildingLookup;
        [ReadOnly] public ComponentLookup<DeliveryTruck> deliveryTruckLookup;
        [ReadOnly] public ComponentLookup<CargoTransport> cargoTransportLookup;
        [ReadOnly] public ComponentLookup<PublicTransport> publicTransportLookup;

        // Counters matching the new groups
        public NativeCounter cntNone;
        public NativeCounter cntShopping;
        public NativeCounter cntLeisure;
        public NativeCounter cntGoingHome;
        public NativeCounter cntGoingToWork;
        public NativeCounter cntMovingAway;
        public NativeCounter cntSchool;
        public NativeCounter cntDelivery;
        public NativeCounter cntTourism;
        public NativeCounter cntOther;
        public NativeCounter cntServices;

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
            if (target == Entity.Null) return Entity.Null;
            Entity current = target;
            if (propertyRenterLookup.TryGetComponent(current, out PropertyRenter renter))
                current = renter.m_Property;
            if (ownerLookup.TryGetComponent(current, out Owner owner))
                if (buildingLookup.HasComponent(owner.m_Owner))
                    current = owner.m_Owner;
            return current;
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
                    case Purpose.None: cntNone.Increment(); break;
                    case Purpose.Shopping: cntShopping.Increment(); break;
                    case Purpose.Leisure:
                    case Purpose.Sleeping:
                    case Purpose.WaitingHome:
                    case Purpose.Relaxing:
                        cntLeisure.Increment(); break;
                    case Purpose.GoingHome: cntGoingHome.Increment(); break;
                    case Purpose.GoingToWork:
                    case Purpose.Working:
                        cntGoingToWork.Increment(); break;
                    case Purpose.MovingAway: cntMovingAway.Increment(); break;
                    case Purpose.GoingToSchool:
                    case Purpose.Studying:
                        cntSchool.Increment(); break;
                    case Purpose.Delivery:
                    case Purpose.Exporting:
                    case Purpose.UpkeepDelivery:
                    case Purpose.StorageTransfer:
                    case Purpose.Collect:
                    case Purpose.CompanyShopping:
                        cntDelivery.Increment(); break;
                    case Purpose.Sightseeing:
                    case Purpose.Traveling:
                    case Purpose.VisitAttractions:
                        cntTourism.Increment(); break;
                    case Purpose.ReturnGarbage:
                    case Purpose.Deathcare:
                    case Purpose.InDeathcare:
                    case Purpose.ReturnUnsortedMail:
                    case Purpose.ReturnLocalMail:
                    case Purpose.ReturnOutgoingMail:
                    case Purpose.SendMail:
                    case Purpose.Hospital:
                    case Purpose.InHospital:
                        cntServices.Increment(); break;
                    case Purpose.Escape:
                    case Purpose.PathFailed:
                    case Purpose.Disappear:
                    case Purpose.Safety:
                    case Purpose.EmergencyShelter:
                    case Purpose.InEmergencyShelter:
                    case Purpose.Crime:
                    case Purpose.GoingToJail:
                    case Purpose.GoingToPrison:
                    case Purpose.InJail:
                    case Purpose.InPrison:
                    default:
                        cntOther.Increment(); break;
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
                    if (physicalOrigin != Entity.Null)
                    {
                        results.Add(new TrafficRenderData { entity = physicalOrigin, purpose = currentPurpose, type = TrafficType.Citizen, isOrigin = true });
                    }
                }
            }
            else
            {
                cntNone.Increment();
            }

            Entity rawDest = Entity.Null;
            if (targetLookup.TryGetComponent(entity, out Target dest))
                rawDest = dest.m_Target;
            else if (currentVehicleLookup.TryGetComponent(entity, out CurrentVehicle vehicleRef))
                if (targetLookup.TryGetComponent(vehicleRef.m_Vehicle, out Target vehicleDest))
                    rawDest = vehicleDest.m_Target;

            if (rawDest != Entity.Null)
            {
                Entity physicalDest = ResolvePhysicalEntity(rawDest);
                if (physicalDest != Entity.Null && physicalDest != selectedSegment)
                {
                    results.Add(new TrafficRenderData { entity = physicalDest, purpose = currentPurpose, type = TrafficType.Citizen, isOrigin = false });
                }
            }
        }
    }
}
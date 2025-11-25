using Colossal;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Vehicles;
using Game.Buildings;
using Game.Companies;
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
        [ReadOnly] public ComponentLookup<Owner> ownerLookup;
        [ReadOnly] public ComponentLookup<HouseholdMember> householdMemberLookup;
        [ReadOnly] public ComponentLookup<Worker> workerLookup;
        [ReadOnly] public ComponentLookup<Game.Citizens.Student> studentLookup;
        [ReadOnly] public ComponentLookup<Game.Creatures.Resident> creatureResidentLookup;
        [ReadOnly] public ComponentLookup<PropertyRenter> propertyRenterLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.DeliveryTruck> deliveryTruckLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.CargoTransport> cargoTransportLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.PublicTransport> publicTransportLookup;

        public NativeCounter workers;
        public NativeCounter students;
        public NativeCounter shoppers;
        public NativeCounter goingHome;
        public NativeCounter healthcare;
        public NativeCounter cargo;
        public NativeCounter services;
        public NativeCounter publicTransport;
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
                        ProcessLaneObject(laneObjects[j].m_LaneObject);
                }
            }
        }

        private void ProcessLaneObject(Entity entity)
        {
            // 1. CARGO
            if (deliveryTruckLookup.HasComponent(entity) || cargoTransportLookup.HasComponent(entity))
            {
                cargo.Increment();
                AnalyzeVehicleData(entity, TrafficType.Cargo);
                return;
            }

            // 2. PUBLIC TRANSPORT
            if (publicTransportLookup.HasComponent(entity))
            {
                publicTransport.Increment();
                AnalyzeVehicleData(entity, TrafficType.PublicTransport);
                return;
            }

            // 3. SERVICE VEHICLES
            if (controllerLookup.HasComponent(entity) && targetLookup.HasComponent(entity))
            {
                if (CheckPassengers(entity)) return; // It's a private car with passengers

                services.Increment();
                AnalyzeVehicleData(entity, TrafficType.Service);
                return;
            }

            // 4. PRIVATE CARS
            if (CheckPassengers(entity)) return;

            // 5. PEDESTRIANS
            if (!controllerLookup.HasComponent(entity))
            {
                AnalyzeCitizen(entity);
            }
        }

        private bool CheckPassengers(Entity rootEntity)
        {
            bool found = false;
            if (passengerLookup.HasBuffer(rootEntity))
            {
                found |= ProcessPassengerBuffer(rootEntity);
            }

            if (layoutElementLookup.HasBuffer(rootEntity))
            {
                DynamicBuffer<LayoutElement> elements = layoutElementLookup[rootEntity];
                for (int k = 0; k < elements.Length; k++)
                {
                    Entity vehiclePart = elements[k].m_Vehicle;
                    if (passengerLookup.HasBuffer(vehiclePart))
                    {
                        found |= ProcessPassengerBuffer(vehiclePart);
                    }
                }
            }
            return found;
        }

        private bool ProcessPassengerBuffer(Entity vehicle)
        {
            DynamicBuffer<Passenger> passengers = passengerLookup[vehicle];
            if (passengers.Length > 0)
            {
                for (int i = 0; i < passengers.Length; i++)
                {
                    AnalyzeCitizen(passengers[i].m_Passenger);
                }
                return true;
            }
            return false;
        }

        private void AnalyzeVehicleData(Entity vehicleEntity, TrafficType type)
        {
            // ORIGIN (Owner)
            if (ownerLookup.TryGetComponent(vehicleEntity, out Owner owner))
            {
                Entity origin = ResolvePhysicalEntity(owner.m_Owner);
                if (origin != Entity.Null)
                {
                    results.Add(new TrafficRenderData
                    {
                        entity = origin,
                        purpose = Purpose.None,
                        type = type,
                        isOrigin = true
                    });
                }
            }

            // DESTINATION (Target)
            if (targetLookup.TryGetComponent(vehicleEntity, out Target target))
            {
                Entity dest = ResolvePhysicalEntity(target.m_Target);
                if (dest != Entity.Null)
                {
                    results.Add(new TrafficRenderData
                    {
                        entity = dest,
                        purpose = Purpose.None,
                        type = type,
                        isOrigin = false
                    });
                }
            }
        }

        private void AnalyzeCitizen(Entity entity)
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
                    results.Add(new TrafficRenderData
                    {
                        entity = physicalOrigin,
                        purpose = currentPurpose,
                        type = TrafficType.Citizen,
                        isOrigin = true
                    });
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
                results.Add(new TrafficRenderData
                {
                    entity = physicalDest,
                    purpose = currentPurpose,
                    type = TrafficType.Citizen,
                    isOrigin = false
                });
            }
        }

        private Entity ResolvePhysicalEntity(Entity target)
        {
            // If target is a Renter (Company/Household), get the Property (Building)
            if (propertyRenterLookup.TryGetComponent(target, out PropertyRenter renter))
            {
                return renter.m_Property;
            }

            // If target is a Vehicle or Citizen, we might want to find their current location, 
            // but usually for TrafficSpy we care about static buildings. 
            // If the target IS a building, it won't have PropertyRenter, so we return target itself.
            return target;
        }
    }
}
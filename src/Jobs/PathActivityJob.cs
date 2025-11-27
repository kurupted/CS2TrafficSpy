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
using TrafficSpy.Systems;

namespace TrafficSpy.Jobs
{
    [BurstCompile]
    public partial struct PathActivityJob : IJobEntity
    {
        [ReadOnly] public NativeHashSet<Entity> targets; // the road segment

        // Citizen Lookups
        [ReadOnly] public ComponentLookup<TravelPurpose> travelPurposeLookup;
        [ReadOnly] public ComponentLookup<Target> targetLookup;
        [ReadOnly] public ComponentLookup<HouseholdMember> householdMemberLookup;
        [ReadOnly] public ComponentLookup<Worker> workerLookup;
        [ReadOnly] public ComponentLookup<Game.Citizens.Student> studentLookup;
        [ReadOnly] public ComponentLookup<Game.Creatures.Resident> creatureResidentLookup;
        [ReadOnly] public ComponentLookup<PropertyRenter> propertyRenterLookup;
        [ReadOnly] public ComponentLookup<Owner> ownerLookup;
        [ReadOnly] public ComponentLookup<Building> buildingLookup;
        [ReadOnly] public ComponentLookup<CurrentVehicle> currentVehicleLookup;

        // Vehicle Lookups
        [ReadOnly] public ComponentLookup<DeliveryTruck> deliveryTruckLookup;
        [ReadOnly] public ComponentLookup<CargoTransport> cargoTransportLookup;
        [ReadOnly] public ComponentLookup<PublicTransport> publicTransportLookup;

        // Service Vehicle Lookups
        [ReadOnly] public ComponentLookup<Game.Vehicles.Hearse> hearseLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.GarbageTruck> garbageTruckLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.PoliceCar> policeCarLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.FireEngine> fireEngineLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.Ambulance> ambulanceLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.PostVan> postVanLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.MaintenanceVehicle> maintenanceVehicleLookup;

        // Counters
        public NativeCounter.Concurrent cntNone;
        public NativeCounter.Concurrent cntShopping;
        public NativeCounter.Concurrent cntLeisure;
        public NativeCounter.Concurrent cntGoingHome;
        public NativeCounter.Concurrent cntGoingToWork;
        public NativeCounter.Concurrent cntMovingAway;
        public NativeCounter.Concurrent cntSchool;
        public NativeCounter.Concurrent cntTransporting;
        public NativeCounter.Concurrent cntReturning;
        public NativeCounter.Concurrent cntTourism;
        public NativeCounter.Concurrent cntOther;
        public NativeCounter.Concurrent cntServices;

        public NativeQueue<TrafficRenderData>.ParallelWriter results;

        public void Execute(Entity entity, DynamicBuffer<PathElement> path)
        {
            bool passesThrough = false;
            for (int i = 0; i < path.Length; i++)
            {
                if (targets.Contains(path[i].m_Target))
                {
                    passesThrough = true;
                    break;
                }
            }

            if (!passesThrough) return;

            // Check Vehicle types first
            if (AnalyzeVehicle(entity)) return;

            // Fallback to Citizen/Generic
            AnalyzeCitizen(entity);
        }

        private bool AnalyzeVehicle(Entity entity)
        {
            // 1. Service Vehicles (Check these first to catch returning hearses etc)
            if (hearseLookup.HasComponent(entity) ||
                garbageTruckLookup.HasComponent(entity) ||
                policeCarLookup.HasComponent(entity) ||
                fireEngineLookup.HasComponent(entity) ||
                ambulanceLookup.HasComponent(entity) ||
                postVanLookup.HasComponent(entity) ||
                maintenanceVehicleLookup.HasComponent(entity))
            {
                cntServices.Increment();
                // FIXED: Directly enqueue destination for vehicles
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Service);
                return true;
            }

            // 2. Public Transport
            if (publicTransportLookup.HasComponent(entity))
            {
                cntOther.Increment();
                // FIXED: Directly enqueue destination for vehicles
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.PublicTransport);
                return true;
            }

            // 3. Delivery / Cargo
            if (deliveryTruckLookup.TryGetComponent(entity, out DeliveryTruck truck))
            {
                if ((truck.m_State & DeliveryTruckFlags.Returning) != 0)
                {
                    cntReturning.Increment();
                }
                else
                {
                    cntTransporting.Increment();
                }
                // FIXED: Always enqueue destination regardless of returning status
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Cargo);
                return true;
            }

            if (cargoTransportLookup.TryGetComponent(entity, out CargoTransport cargo))
            {
                if ((cargo.m_State & CargoTransportFlags.Returning) != 0)
                {
                    cntReturning.Increment();
                }
                else
                {
                    cntTransporting.Increment();
                }
                // FIXED: Always enqueue destination regardless of returning status
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Cargo);
                return true;
            }

            return false;
        }

        private void AnalyzeCitizen(Entity entity)
        {
            Entity citizenEntity = entity;
            // Try to resolve driver/resident if possible
            if (creatureResidentLookup.TryGetComponent(entity, out Game.Creatures.Resident resident))
            {
                citizenEntity = resident.m_Citizen;
            }

            Purpose currentPurpose = Purpose.None;
            bool hasPurpose = false;

            if (travelPurposeLookup.TryGetComponent(citizenEntity, out TravelPurpose purpose))
            {
                hasPurpose = true;
                currentPurpose = purpose.m_Purpose;

                switch (currentPurpose)
                {
                    case Purpose.None:
                        cntNone.Increment();
                        break;
                    case Purpose.Shopping:
                        cntShopping.Increment();
                        break;
                    case Purpose.Leisure:
                    case Purpose.Sleeping:
                    case Purpose.WaitingHome:
                    case Purpose.Relaxing:
                        cntLeisure.Increment();
                        break;
                    case Purpose.GoingHome:
                        cntGoingHome.Increment();
                        break;
                    case Purpose.GoingToWork:
                    case Purpose.Working:
                        cntGoingToWork.Increment();
                        break;
                    case Purpose.MovingAway:
                        cntMovingAway.Increment();
                        break;
                    case Purpose.GoingToSchool:
                    case Purpose.Studying:
                        cntSchool.Increment();
                        break;
                    case Purpose.Sightseeing:
                    case Purpose.Traveling:
                    case Purpose.VisitAttractions:
                        cntTourism.Increment();
                        break;

                    // Cargo purposes (if citizen has them, unlikely but possible)
                    case Purpose.Delivery:
                    case Purpose.Exporting:
                    case Purpose.UpkeepDelivery:
                    case Purpose.StorageTransfer:
                    case Purpose.Collect:
                    case Purpose.CompanyShopping:
                        cntTransporting.Increment();
                        break;

                    case Purpose.ReturnGarbage:
                    case Purpose.Deathcare:
                    case Purpose.InDeathcare:
                    case Purpose.ReturnUnsortedMail:
                    case Purpose.ReturnLocalMail:
                    case Purpose.ReturnOutgoingMail:
                    case Purpose.SendMail:
                    case Purpose.Hospital:
                    case Purpose.InHospital:
                        cntServices.Increment();
                        break;

                    default:
                        cntOther.Increment();
                        break;
                }

                // Origin Logic
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
                        results.Enqueue(new TrafficRenderData { entity = physicalOrigin, purpose = currentPurpose, type = TrafficType.Citizen, isOrigin = true });
                    }
                }
            }
            else
            {
                // No purpose (Private cars, dummy traffic)
                cntNone.Increment();
            }

            // Always try to visualize destination, even if no purpose found
            EnqueueDestination(entity, currentPurpose);
        }

        // FIXED: New method specifically for vehicle destinations
        private void EnqueueVehicleDestination(Entity vehicleEntity, Purpose purpose, TrafficType type)
        {
            Entity rawDest = Entity.Null;

            // Vehicles have Target component directly on them
            if (targetLookup.TryGetComponent(vehicleEntity, out Target dest))
            {
                rawDest = dest.m_Target;

                if (rawDest != Entity.Null)
                {
                    Entity physicalDest = ResolvePhysicalEntity(rawDest);
                    if (physicalDest != Entity.Null && !targets.Contains(physicalDest))
                    {
                        results.Enqueue(new TrafficRenderData { entity = physicalDest, purpose = purpose, type = type, isOrigin = false });
                    }
                }
            }
        }

        private void EnqueueDestination(Entity entity, Purpose purpose)
        {
            Entity rawDest = Entity.Null;

            // 1. Check direct target (Citizens, Pedestrians)
            if (targetLookup.TryGetComponent(entity, out Target dest))
            {
                rawDest = dest.m_Target;
            }
            // 2. Check Vehicle target (Drivers)
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
                if (physicalDest != Entity.Null && !targets.Contains(physicalDest))
                {
                    results.Enqueue(new TrafficRenderData { entity = physicalDest, purpose = purpose, type = TrafficType.Citizen, isOrigin = false });
                }
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
    }
}
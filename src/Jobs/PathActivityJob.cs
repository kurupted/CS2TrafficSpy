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
        [ReadOnly] public NativeHashSet<Entity> targets;

        // Citizen Lookups
        [ReadOnly] public ComponentLookup<TravelPurpose> travelPurposeLookup;
        [ReadOnly] public ComponentLookup<Target> targetLookup;
        [ReadOnly] public ComponentLookup<Household> householdLookup;
        [ReadOnly] public ComponentLookup<HouseholdMember> householdMemberLookup;
        [ReadOnly] public ComponentLookup<Worker> workerLookup;
        [ReadOnly] public ComponentLookup<Game.Citizens.Student> studentLookup;
        [ReadOnly] public ComponentLookup<Game.Creatures.Resident> creatureResidentLookup;
        [ReadOnly] public ComponentLookup<PropertyRenter> propertyRenterLookup;
        [ReadOnly] public ComponentLookup<Owner> ownerLookup;
        [ReadOnly] public ComponentLookup<Building> buildingLookup;
        [ReadOnly] public ComponentLookup<CurrentVehicle> currentVehicleLookup;
        [ReadOnly] public ComponentLookup<CurrentTransport> currentTransportLookup;

        // Vehicle Lookups
        [ReadOnly] public ComponentLookup<PersonalCar> personalCarLookup;
        [ReadOnly] public ComponentLookup<DeliveryTruck> deliveryTruckLookup;
        [ReadOnly] public ComponentLookup<CargoTransport> cargoTransportLookup;
        [ReadOnly] public ComponentLookup<PublicTransport> publicTransportLookup;
        
        // Taxi Lookups
        [ReadOnly] public ComponentLookup<Game.Vehicles.Taxi> taxiLookup;
        [ReadOnly] public BufferLookup<Passenger> passengerLookup;

        // Service Vehicle Lookups
        [ReadOnly] public ComponentLookup<Game.Vehicles.Hearse> hearseLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.GarbageTruck> garbageTruckLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.PoliceCar> policeCarLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.FireEngine> fireEngineLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.Ambulance> ambulanceLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.PostVan> postVanLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.MaintenanceVehicle> maintenanceVehicleLookup;

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

            if (AnalyzeVehicle(entity)) return;

            AnalyzeCitizen(entity);
        }

        private bool AnalyzeVehicle(Entity entity)
        {
            // 1. Service Vehicles
            if (hearseLookup.HasComponent(entity) ||
                garbageTruckLookup.HasComponent(entity) ||
                policeCarLookup.HasComponent(entity) ||
                fireEngineLookup.HasComponent(entity) ||
                ambulanceLookup.HasComponent(entity) ||
                postVanLookup.HasComponent(entity) ||
                maintenanceVehicleLookup.HasComponent(entity))
            {
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Service);
                return true;
            }

            // Taxis (Check before PublicTransport to be specific)
            if (taxiLookup.HasComponent(entity))
            {
                Purpose passengerPurpose = Purpose.None;
                TrafficType type = TrafficType.Service; // Default to Service (Yellow/Other)
                bool isMovingIn = false;

                // Try to find a passenger to get the real purpose
                if (passengerLookup.TryGetBuffer(entity, out DynamicBuffer<Passenger> passengers) && passengers.Length > 0)
                {
                    for (int i = 0; i < passengers.Length; i++)
                    {
                        Entity passenger = passengers[i].m_Passenger;
                        if (travelPurposeLookup.TryGetComponent(passenger, out TravelPurpose purpose))
                        {
                            passengerPurpose = purpose.m_Purpose;
                            type = TrafficType.Citizen; // Upgrade to Citizen so it counts towards Shopping/Home/etc stats
                            
                            if (passengerPurpose == Purpose.GoingHome)
                            {
                                if (householdMemberLookup.TryGetComponent(passenger, out HouseholdMember householdMember) &&
                                    householdLookup.TryGetComponent(householdMember.m_Household, out Game.Citizens.Household household) &&
                                    (household.m_Flags & HouseholdFlags.MovedIn) == 0)
                                {
                                    isMovingIn = true;
                                }
                            }
                            break; // Use the first valid passenger's purpose
                        }
                    }
                }

                EnqueueVehicleDestination(entity, passengerPurpose, type, isMovingIn);
                return true;
            }

            // 2. Public Transport
            if (publicTransportLookup.HasComponent(entity))
            {
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.PublicTransport);
                return true;
            }

            // 3. Delivery / Cargo
            if (deliveryTruckLookup.TryGetComponent(entity, out DeliveryTruck truck))
            {
                if ((truck.m_State & DeliveryTruckFlags.Returning) != 0)
                {
                    EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Cargo);
                }
                else
                {
                    EnqueueVehicleDestination(entity, Purpose.Delivery, TrafficType.Cargo);
                }
                return true;
            }

            // 4. Cargo
            if (cargoTransportLookup.TryGetComponent(entity, out CargoTransport cargo))
            {
                if ((cargo.m_State & CargoTransportFlags.Returning) != 0)
                {
                    EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Cargo);
                }
                else
                {
                    EnqueueVehicleDestination(entity, Purpose.Delivery, TrafficType.Cargo);
                }
                return true;
            }

            // 5. Personal Cars
            if (personalCarLookup.TryGetComponent(entity, out PersonalCar car))
            {
                Purpose driverPurpose = Purpose.None;
                bool isMovingIn = false;

                if (car.m_Keeper != Entity.Null && travelPurposeLookup.TryGetComponent(car.m_Keeper, out TravelPurpose purpose))
                {
                    driverPurpose = purpose.m_Purpose;
                    if (driverPurpose == Purpose.GoingHome)
                    {
                        if (householdMemberLookup.TryGetComponent(car.m_Keeper, out HouseholdMember householdMember) &&
                            householdLookup.TryGetComponent(householdMember.m_Household, out Game.Citizens.Household household) &&
                            (household.m_Flags & HouseholdFlags.MovedIn) == 0)
                        {
                            isMovingIn = true;
                        }
                    }
                }
                EnqueueVehicleDestination(entity, driverPurpose, TrafficType.Citizen, isMovingIn);
                return true;
            }

            return false;
        }

        private void AnalyzeCitizen(Entity entity)
        {
            // 1. Check if they are driving (CurrentVehicle) OR riding Public Transport (CurrentTransport)
            // If they have either, they are not "Pedestrians" on the road surface, so we skip them.
            if (currentVehicleLookup.HasComponent(entity) || currentTransportLookup.HasComponent(entity)) return;

            Entity citizenEntity = entity;
            if (creatureResidentLookup.TryGetComponent(entity, out Game.Creatures.Resident resident))
            {
                citizenEntity = resident.m_Citizen;
            }

            Purpose currentPurpose = Purpose.None;
            bool isMovingIn = false;

            if (travelPurposeLookup.TryGetComponent(citizenEntity, out TravelPurpose purpose))
            {
                currentPurpose = purpose.m_Purpose;
                if (currentPurpose == Purpose.GoingHome)
                {
                    if (householdMemberLookup.TryGetComponent(entity, out HouseholdMember householdMember) &&
                        householdLookup.TryGetComponent(householdMember.m_Household, out Game.Citizens.Household household) &&
                        (household.m_Flags & HouseholdFlags.MovedIn) == 0)
                    {
                        isMovingIn = true;
                    }
                }
            }

            EnqueueDestination(entity, currentPurpose, isMovingIn);
        }

        private void EnqueueVehicleDestination(Entity vehicleEntity, Purpose purpose, TrafficType type, bool isMovingIn = false)
        {
            // Vehicle Agent
            // isVehicle = true, isDestination = false
            results.Enqueue(new TrafficRenderData
            {
                entity = vehicleEntity,
                purpose = purpose,
                type = type,
                isOrigin = false,
                isVehicle = true,
                isPedestrian = false,
                isDestination = false,
                isMovingIn = isMovingIn
            });

            // Vehicle Destination (Building)
            if (targetLookup.TryGetComponent(vehicleEntity, out Target dest) && dest.m_Target != Entity.Null)
            {
                Entity physicalDest = ResolvePhysicalEntity(dest.m_Target);
                if (physicalDest != Entity.Null && !targets.Contains(physicalDest))
                {
                    // isVehicle = false, isDestination = true
                    results.Enqueue(new TrafficRenderData
                    {
                        entity = physicalDest,
                        purpose = purpose,
                        type = type,
                        isOrigin = false,
                        isVehicle = false,
                        isPedestrian = false,
                        isDestination = true,
                        isMovingIn = isMovingIn
                    });
                }
            }
        }

        private void EnqueueDestination(Entity entity, Purpose purpose, bool isMovingIn = false)
        {
            Entity renderEntity = entity;
            TrafficType type = TrafficType.Citizen;

            // Note: Since we return early for CurrentVehicle/CurrentTransport in AnalyzeCitizen, 
            // we can assume anyone reaching here is a pedestrian.
            bool isPedestrian = true;
            bool isVehicle = false;

            // Citizen Agent
            // isDestination = false
            results.Enqueue(new TrafficRenderData
            {
                entity = renderEntity,
                purpose = purpose,
                type = type,
                isOrigin = false,
                isVehicle = isVehicle,
                isPedestrian = isPedestrian,
                isDestination = false,
                isMovingIn = isMovingIn
            });

            Entity rawDest = Entity.Null;
            if (targetLookup.TryGetComponent(entity, out Target dest))
            {
                rawDest = dest.m_Target;
            }

            if (rawDest != Entity.Null)
            {
                Entity physicalDest = ResolvePhysicalEntity(rawDest);
                if (physicalDest != Entity.Null && !targets.Contains(physicalDest))
                {
                    // Destination Building
                    // isVehicle = false, isDestination = true
                    // Keep isPedestrian = true (if they were walking) to allow filtering "Pedestrian Destinations" out.
                    results.Enqueue(new TrafficRenderData
                    {
                        entity = physicalDest,
                        purpose = purpose,
                        type = type,
                        isOrigin = false,
                        isVehicle = false,
                        isPedestrian = isPedestrian,
                        isDestination = true,
                        isMovingIn = isMovingIn
                    });
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
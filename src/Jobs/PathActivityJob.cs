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

        // Vehicle Lookups
        [ReadOnly] public ComponentLookup<PersonalCar> personalCarLookup;
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
        public NativeCounter.Concurrent cntMovingIn;
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
            // ... (Keep logic) ...
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
                cntServices.Increment();
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Service);
                return true;
            }

            // 2. Public Transport
            if (publicTransportLookup.HasComponent(entity))
            {
                cntOther.Increment();
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.PublicTransport);
                return true;
            }

            // 3. Delivery / Cargo (TrafficType.Cargo)
            // Use Purpose.Delivery for transporting, Purpose.None for returning
            if (deliveryTruckLookup.TryGetComponent(entity, out DeliveryTruck truck))
            {
                if ((truck.m_State & DeliveryTruckFlags.Returning) != 0)
                {
                    cntReturning.Increment();
                    EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Cargo); // Returning
                }
                else
                {
                    cntTransporting.Increment();
                    EnqueueVehicleDestination(entity, Purpose.Delivery, TrafficType.Cargo); // Transporting
                }
                return true;
            }

            // 4. Cargo
            if (cargoTransportLookup.TryGetComponent(entity, out CargoTransport cargo))
            {
                if ((cargo.m_State & CargoTransportFlags.Returning) != 0)
                {
                    cntReturning.Increment();
                    EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Cargo); // Returning
                }
                else
                {
                    cntTransporting.Increment();
                    EnqueueVehicleDestination(entity, Purpose.Delivery, TrafficType.Cargo); // Transporting
                }
                return true;
            }

            // 5. Personal Cars
            if (personalCarLookup.TryGetComponent(entity, out PersonalCar car))
            {
                Purpose driverPurpose = Purpose.None;

                if (car.m_Keeper != Entity.Null && travelPurposeLookup.TryGetComponent(car.m_Keeper, out TravelPurpose purpose))
                {
                    driverPurpose = purpose.m_Purpose;
                    IncrementCounter(driverPurpose, car.m_Keeper); // Helper to increment based on purpose
                }
                else
                {
                    cntNone.Increment();
                }
                EnqueueVehicleDestination(entity, driverPurpose, TrafficType.Citizen);
                return true;
            }

            return false;
        }

        private void AnalyzeCitizen(Entity entity)
        {
            // Exclude Citizens inside vehicles ---
            // If a citizen is in a vehicle, the Vehicle entity logic above should handle it.
            // If we process the citizen too, we get double counts and confusing highlights.
            if (currentVehicleLookup.HasComponent(entity)) return;

            Entity citizenEntity = entity;
            if (creatureResidentLookup.TryGetComponent(entity, out Game.Creatures.Resident resident))
            {
                citizenEntity = resident.m_Citizen;
            }

            Purpose currentPurpose = Purpose.None;
            if (travelPurposeLookup.TryGetComponent(citizenEntity, out TravelPurpose purpose))
            {
                currentPurpose = purpose.m_Purpose;
                IncrementCounter(currentPurpose, citizenEntity);
            }
            else
            {
                cntNone.Increment();
            }

            // This citizen is walking/waiting (not in vehicle), so include them.
            EnqueueDestination(entity, currentPurpose);
        }

        private void IncrementCounter(Purpose p, Entity entity)
        {
            switch (p)
            {
                case Purpose.None: cntNone.Increment(); break;
                case Purpose.Shopping: cntShopping.Increment(); break;
                case Purpose.Leisure:
                case Purpose.Sleeping:
                case Purpose.WaitingHome:
                case Purpose.Relaxing: cntLeisure.Increment(); break;
                case Purpose.GoingHome:
                    if (householdMemberLookup.TryGetComponent(entity, out HouseholdMember householdMember) &&
                        householdLookup.TryGetComponent(householdMember.m_Household, out Game.Citizens.Household household) &&
                        (household.m_Flags & HouseholdFlags.MovedIn) == 0)
                    {
                        cntMovingIn.Increment();
                    }
                    else
                    {
                        cntGoingHome.Increment();
                    }
                    break;
                case Purpose.GoingToWork:
                case Purpose.Working: cntGoingToWork.Increment(); break;
                case Purpose.MovingAway: cntMovingAway.Increment(); break;
                case Purpose.GoingToSchool:
                case Purpose.Studying: cntSchool.Increment(); break;
                case Purpose.Sightseeing:
                case Purpose.Traveling:
                case Purpose.VisitAttractions: cntTourism.Increment(); break;
                case Purpose.Delivery:
                case Purpose.Exporting:
                case Purpose.UpkeepDelivery:
                case Purpose.StorageTransfer:
                case Purpose.Collect:
                case Purpose.CompanyShopping: cntTransporting.Increment(); break;
                case Purpose.ReturnGarbage:
                case Purpose.Deathcare:
                case Purpose.InDeathcare:
                case Purpose.ReturnUnsortedMail:
                case Purpose.ReturnLocalMail:
                case Purpose.ReturnOutgoingMail:
                case Purpose.SendMail:
                case Purpose.Hospital:
                case Purpose.InHospital: cntServices.Increment(); break;
                default: cntOther.Increment(); break;
            }
        }

        private void EnqueueVehicleDestination(Entity vehicleEntity, Purpose purpose, TrafficType type)
        {
            // Enqueue Vehicle (isVehicle = true)
            results.Enqueue(new TrafficRenderData { entity = vehicleEntity, purpose = purpose, type = type, isOrigin = false, isVehicle = true });

            // Logic to find destination building
            if (targetLookup.TryGetComponent(vehicleEntity, out Target dest) && dest.m_Target != Entity.Null)
            {
                Entity physicalDest = ResolvePhysicalEntity(dest.m_Target);
                if (physicalDest != Entity.Null && !targets.Contains(physicalDest))
                {
                        // Enqueue Destination (isVehicle = false)
                    results.Enqueue(new TrafficRenderData { entity = physicalDest, purpose = purpose, type = type, isOrigin = false, isVehicle = false });
                }
            }
        }

        private void EnqueueDestination(Entity entity, Purpose purpose)
        {
            Entity renderEntity = entity;
            TrafficType type = TrafficType.Citizen;

            // Resolve the actual vehicle entity if the citizen is driving
            bool isDriving = currentVehicleLookup.TryGetComponent(entity, out CurrentVehicle vehicleRef);
            if (isDriving)
            {
                renderEntity = vehicleRef.m_Vehicle;
            }

            // Enqueue the moving agent (pedestrian or vehicle) for highlighting
            results.Enqueue(new TrafficRenderData { entity = renderEntity, purpose = purpose, type = type, isOrigin = false, isVehicle = true });

            Entity rawDest = Entity.Null;
            if (targetLookup.TryGetComponent(entity, out Target dest))
            {
                rawDest = dest.m_Target;
            }
            else if (isDriving)
            {
                // If a driver, check the vehicle's target
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
                    // Enqueue the Destination (isVehicle = false)
                    results.Enqueue(new TrafficRenderData { entity = physicalDest, purpose = purpose, type = type, isOrigin = false, isVehicle = false });
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
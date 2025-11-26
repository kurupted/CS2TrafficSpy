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

        // Updated Counters
        public NativeCounter.Concurrent cntNone;
        public NativeCounter.Concurrent cntShopping;
        public NativeCounter.Concurrent cntLeisure;
        public NativeCounter.Concurrent cntGoingHome;
        public NativeCounter.Concurrent cntGoingToWork;
        public NativeCounter.Concurrent cntMovingAway;
        public NativeCounter.Concurrent cntSchool;
        public NativeCounter.Concurrent cntDelivery;
        public NativeCounter.Concurrent cntTourism;
        public NativeCounter.Concurrent cntOther;
        public NativeCounter.Concurrent cntServices;

        public NativeQueue<TrafficRenderData>.ParallelWriter results;

        public void Execute(Entity entity, DynamicBuffer<PathElement> path)
        {
            // 1. Check if this entity's path intersects with our target road/lanes
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

            // 2. If it intersects, analyze the entity
            AnalyzeEntity(entity);
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

                // Updated Switch Statement based on your groups
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

                    case Purpose.Delivery:
                    case Purpose.Exporting:
                    case Purpose.UpkeepDelivery:
                    case Purpose.StorageTransfer:
                    case Purpose.Collect:
                    case Purpose.CompanyShopping:
                        cntDelivery.Increment();
                        break;

                    case Purpose.Sightseeing:
                    case Purpose.Traveling:
                    case Purpose.VisitAttractions:
                        cntTourism.Increment();
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
                    default: // Catch-all for anything else
                        cntOther.Increment();
                        break;
                }

                // Origin/Destination Logic (Kept same, just ensures visuals still work)
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
                // No purpose component usually means "None" or generic vehicle behavior
                cntNone.Increment();
            }

            // Destination Logic
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
                if (physicalDest != Entity.Null && !targets.Contains(physicalDest))
                {
                    results.Enqueue(new TrafficRenderData { entity = physicalDest, purpose = currentPurpose, type = TrafficType.Citizen, isOrigin = false });
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
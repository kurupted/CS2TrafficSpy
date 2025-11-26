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

        // Concurrent counters for thread safety
        public NativeCounter.Concurrent workers;
        public NativeCounter.Concurrent students;
        public NativeCounter.Concurrent shoppers;
        public NativeCounter.Concurrent goingHome;
        public NativeCounter.Concurrent healthcare;
        public NativeCounter.Concurrent other;
        public NativeCounter.Concurrent noPurpose;
        public NativeCounter.Concurrent cargo;
        public NativeCounter.Concurrent services;
        public NativeCounter.Concurrent publicTransport;

        // FIXED: Use NativeQueue for thread-safe dynamic insertion
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

                // Resolve Origin
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
                noPurpose.Increment();
            }

            // Resolve Destination
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
            {
                current = renter.m_Property;
            }

            if (ownerLookup.TryGetComponent(current, out Owner owner))
            {
                if (buildingLookup.HasComponent(owner.m_Owner))
                {
                    current = owner.m_Owner;
                }
            }

            return current;
        }
    }
}
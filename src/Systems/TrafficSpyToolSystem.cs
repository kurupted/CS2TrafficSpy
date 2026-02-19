using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Game.Routes;
using UnityEngine;
using Unity.Collections;
using PedestrianLane = Game.Net.PedestrianLane;
using TrackLane = Game.Net.TrackLane;

namespace TrafficSpy.Systems
{
    public partial class TrafficSpyToolSystem : ToolBaseSystem
    {
        public override string toolID => "TrafficSpyTool";

        private ToolSystem _toolSystem;
        private TrafficUISystem _uiSystem;
        private Entity _hoveredEntity = Entity.Null;
        private EntityQuery m_HighlightedQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            _uiSystem = World.GetOrCreateSystemManaged<TrafficUISystem>();
            m_HighlightedQuery = GetEntityQuery(ComponentType.ReadWrite<Highlighted>());
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            
            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.StaticObjects;
            
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.PublicTransportRoad | Layer.Pathway | 
                                               Layer.TrainTrack | Layer.TramTrack | Layer.SubwayTrack;
                                               
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (_toolSystem.activeTool != this) return inputDeps;

            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;

            Entity hitEntity = Entity.Null;
            if ((m_ToolRaycastSystem.raycastFlags & RaycastFlags.UIDisable) == 0)
            {
                if (GetRaycastResult(out Entity entity, out _))
                {
                    Entity potentialTarget = entity;

                    // 1. OWNER CHECK (Handle clicking props/sub-objects)
                    // If we click a bench or a tree attached to a road/station, 
                    // check its owner to see if the OWNER is a valid target.
                    if (!IsValidSpyTarget(potentialTarget) && EntityManager.HasComponent<Owner>(potentialTarget))
                    {
                        potentialTarget = EntityManager.GetComponentData<Owner>(potentialTarget).m_Owner;
                    }

                    // 2. FINAL VALIDATION
                    if (IsValidSpyTarget(potentialTarget))
                    {
                        hitEntity = potentialTarget;
                    }
                }
            }

            UpdateHoverHighlight(hitEntity);

            if (hitEntity != Entity.Null && applyAction.WasReleasedThisFrame())
            {
                _toolSystem.selected = hitEntity;
                Disable(); 
            }

            if (cancelAction.WasPressedThisFrame())
            {
                _uiSystem.SetSpyMode(false);
                Disable();
            }

            return inputDeps;
        }

        private bool IsValidSpyTarget(Entity entity)
        {
            if (entity == Entity.Null) return false;

            // A. Networks (Roads & Rails & Paths)
            if (EntityManager.HasComponent<Road>(entity) || 
                EntityManager.HasComponent<TrainTrack>(entity) ||
                EntityManager.HasComponent<TramTrack>(entity) ||
                EntityManager.HasComponent<SubwayTrack>(entity) ||
                EntityManager.HasComponent<PedestrianLane>(entity) ||
                EntityManager.HasComponent<TrackLane>(entity) ||
                EntityManager.HasComponent<Waterway>(entity))
            {
                return true;
            }

            // B. Transit Stations 
            if (EntityManager.HasComponent<Game.Buildings.TransportStation>(entity))
            {
                return true;
            }
            
            // C. Direct Stops (If a stop icon is directly clickable, reject bicycles)
            if (EntityManager.HasComponent<Game.Routes.TransportStop>(entity))
            {
                if (EntityManager.HasComponent<Game.Routes.BusStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.TrainStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.TramStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.SubwayStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.ShipStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.AirplaneStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.FerryStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.TaxiStand>(entity))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateHoverHighlight(Entity newHover)
        {
            if (newHover == _hoveredEntity) return;

            if (_hoveredEntity != Entity.Null)
            {
                if (_toolSystem.selected != _hoveredEntity)
                {
                    EntityManager.RemoveComponent<Highlighted>(_hoveredEntity);
                    EntityManager.AddComponent<BatchesUpdated>(_hoveredEntity);
                }
            }

            if (newHover != Entity.Null)
            {
                EntityManager.AddComponent<Highlighted>(newHover);
                EntityManager.AddComponent<BatchesUpdated>(newHover);
            }

            _hoveredEntity = newHover;
        }

        public void Enable()
        {
            _toolSystem.activeTool = this;
            
            // Clear any lingering highlights left by the default tool before we start
            using var entities = m_HighlightedQuery.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                EntityManager.RemoveComponent<Highlighted>(e);
                EntityManager.AddComponent<BatchesUpdated>(e);
            }
        }

        public void Disable()
        {
            if (_toolSystem.activeTool == this)
            {
                _toolSystem.activeTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            }
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            // Cleanup highlights
            if (_hoveredEntity != Entity.Null)
            {
                 EntityManager.RemoveComponent<Highlighted>(_hoveredEntity);
                 EntityManager.AddComponent<BatchesUpdated>(_hoveredEntity);
                 _hoveredEntity = Entity.Null;
            }
        }

        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;
    }
}
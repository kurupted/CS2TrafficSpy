using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Game.Routes;
using UnityEngine;
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

        protected override void OnCreate()
        {
            base.OnCreate();
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            _uiSystem = World.GetOrCreateSystemManaged<TrafficUISystem>();
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

        // Helper to filter for only Roads, Tracks, and Transit Stations
        private bool IsValidSpyTarget(Entity entity)
        {
            if (entity == Entity.Null) return false;

            // A. Networks (Roads & Rails)
            if (EntityManager.HasComponent<Road>(entity) || 
                EntityManager.HasComponent<TrainTrack>(entity) ||
                EntityManager.HasComponent<TramTrack>(entity) ||
                EntityManager.HasComponent<SubwayTrack>(entity))
            {
                return true;
            }

            // B. Transit Stations (Plopped Buildings Only)
            // This filters out Zoned Buildings (Res/Com/Ind) because they lack this component.
            if (EntityManager.HasComponent<Game.Buildings.TransportStation>(entity))
            {
                return true;
            }
            
            // C. Direct Stops (If a stop icon is directly clickable)
            if (EntityManager.HasComponent<Game.Routes.TransportStop>(entity))
            {
                return true;
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
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

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
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.PublicTransportRoad | Layer.Pathway;
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
                    hitEntity = entity;
                }
            }

            UpdateHoverHighlight(hitEntity);

            // 1. SELECT (Left Click)
            if (hitEntity != Entity.Null && applyAction.WasReleasedThisFrame())
            {
                _toolSystem.selected = hitEntity;
                
                // Switch to Default Tool to show Info Panel.
                // We do NOT turn off Spy Mode here. The UI System keeps it alive.
                Disable(); 
            }

            // 2. CANCEL (Right Click / Esc)
            if (cancelAction.WasPressedThisFrame())
            {
                // User explicitly wants to quit the tool completely.
                _uiSystem.SetSpyMode(false);
                Disable();
            }

            return inputDeps;
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
            // CRITICAL CHANGE: Do NOT call SyncToolState(false) here. 
            // We let the UI System manage the "Mode".
        }

        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;
    }
}
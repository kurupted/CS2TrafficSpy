using Colossal;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Input;
using Game.Net;
using Game.Objects;
using Game.Routes;
using Game.Prefabs;
using Game.Pathfind;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Game.Vehicles;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TrafficSpy.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Diagnostics;
using Entity = Unity.Entities.Entity;

namespace TrafficSpy.Systems
{
    public enum TrafficType
    {
        Citizen,
        Cargo,
        PublicTransport,
        Service
    }

    public struct TrafficRenderData
    {
        public Entity entity;
        public Entity sourceAgent; // The agent responsible for this entry (for distance checks)
        public Entity destinationEntity; 
        public Entity waitingAtStop;
        public Game.Citizens.Purpose purpose;
        public TrafficType type;
        public bool isOrigin;
        public bool isVehicle;
        public bool isPedestrian;
        public bool isDestination;
        public bool isMovingIn;
        public bool isTourist;
    }

    [UpdateAfter(typeof(ToolSystem))]
    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem toolSystem;
        private DefaultToolSystem defaultToolSystem;
        private TrafficSpyToolSystem trafficSpyToolSystem;
        private Game.Rendering.CameraUpdateSystem cameraUpdateSystem;
        private bool _isSpyModeActive = false;

        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;
        private ValueBinding<bool> highlightAgentsBinding;
        private ValueBinding<int> displayModeBinding; // 0 = Vehicles, 1 = Pedestrians
        private ValueBinding<bool> showRoutesBinding;
        private ValueBinding<int> directionModeBinding;
        private ValueBinding<int> rangeModeBinding; // 0=Short, 1=Med, 2=Long, 3=Unlimited
        private ValueBinding<string> associatedStopsBinding;
        private ValueBinding<bool> walkingOnlyBinding;
        private ValueBinding<bool> isTransitStopSelectedBinding;
        private ValueBinding<bool> hasParentBinding;
        private ValueBinding<bool> isRoadSelectedBinding;

        private ValueBinding<string> trafficJamDataBinding;
        private ValueBinding<bool> monitorPanelActiveBinding;
        private bool monitorPanelActive = false;
        private float monitorTimer = 5.0f;
        private EntityQuery m_BlockerVehicleQuery;
        private EntityQuery m_NotificationIconQuery;
        private EntityQuery m_TrafficConfigurationQuery;
        private Game.UI.NameSystem nameSystem;

        private bool highlightAgents = false;
        private int displayMode = 0; // Default to Vehicles
        
        public static List<Entity> AnalyzedLanes = new List<Entity>();
        public bool HighlightAgents => highlightAgents;
        public bool ShowRoutes { get; private set; } = true; // Default to True
        private int directionMode = 0; // 0 = Both, 1 = Side A (Fwd), 2 = Side B (Bwd) 
        private int rangeMode = 1; // Default to Medium
        public int RangeMode => rangeMode;

        // Statics for the Route System to read
        public static float3 FilterPosition = float3.zero;
        public static float FilterDistance = 3000f; // Default Medium

        private bool isToolActive = false;
        private bool usePathBasedAnalysis = true;
        private bool walkingOnly = true;

        private bool wasToggleKeyDown = false;

        private List<TrafficRenderData> allAnalysisResults = new List<TrafficRenderData>();
        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        public static bool IsDirty = false;

        private string currentFilter = "";
        private Entity lastSelectedEntity = Entity.Null;
        private Entity lastParentEntity = Entity.Null; 
        
        private EntityQuery pathOwnerQuery;
        private EntityQuery waitingPassengersQuery;
        
        // for 'gray world'
        /*private EntityQuery infoviewQuery;
        private Entity fakeInfoviewEntity = Entity.Null;
        private ValueBinding<bool> grayWorldBinding;
        private bool grayWorldEnabled = false;*/
        
        private struct StopOption
        {
            public Entity Entity;
            public string Name;
        }
        private List<StopOption> m_AssociatedStops = new List<StopOption>();
        private EntityQuery m_AllStopsQuery;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            m_InfoUISystem.AddMiddleSection(this);

            this.toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            this.defaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            this.trafficSpyToolSystem = World.GetOrCreateSystemManaged<TrafficSpyToolSystem>();
            this.cameraUpdateSystem = World.GetOrCreateSystemManaged<Game.Rendering.CameraUpdateSystem>();
            
            // Listen for tool changes
            this.toolSystem.EventToolChanged += OnToolChanged;
            
            this.pathOwnerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.waitingPassengersQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] 
                {
                    ComponentType.ReadOnly<Game.Creatures.Resident>(),
                    ComponentType.ReadOnly<Creature>(),
                    ComponentType.ReadOnly<Target>(), 
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.m_AllStopsQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Routes.TransportStop>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.m_BlockerVehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Vehicles.Vehicle>(),
                    ComponentType.ReadOnly<Game.Vehicles.Blocker>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.m_NotificationIconQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Notifications.Icon>(),
                    ComponentType.ReadOnly<Game.Prefabs.PrefabRef>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.m_TrafficConfigurationQuery = GetEntityQuery(ComponentType.ReadOnly<TrafficConfigurationData>());
            this.nameSystem = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();

            this.activityDataBinding = new ValueBinding<string>("TrafficSpy", "activityData", "{}");
            this.toolActiveBinding = new ValueBinding<bool>("TrafficSpy", "toolActive", false);
            this.highlightAgentsBinding = new ValueBinding<bool>("TrafficSpy", "highlightAgents", false);
            this.displayModeBinding = new ValueBinding<int>("TrafficSpy", "displayMode", 0);
            this.showRoutesBinding = new ValueBinding<bool>("TrafficSpy", "showRoutes", true);
            this.directionModeBinding = new ValueBinding<int>("TrafficSpy", "directionMode", 0);
            this.rangeModeBinding = new ValueBinding<int>("TrafficSpy", "rangeMode", 1);
            this.associatedStopsBinding = new ValueBinding<string>("TrafficSpy", "associatedStops", "[]");
            this.walkingOnlyBinding = new ValueBinding<bool>("TrafficSpy", "walkingOnly", true);
            this.isTransitStopSelectedBinding = new ValueBinding<bool>("TrafficSpy", "isTransitStopSelected", false);
            this.hasParentBinding = new ValueBinding<bool>("TrafficSpy", "hasParent", false);
            this.isRoadSelectedBinding = new ValueBinding<bool>("TrafficSpy", "isRoadSelected", false);

            this.trafficJamDataBinding = new ValueBinding<string>("TrafficSpy", "trafficJamData", "{}");
            this.monitorPanelActiveBinding = new ValueBinding<bool>("TrafficSpy", "monitorPanelActive", false);

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);
            AddBinding(this.highlightAgentsBinding);
            AddBinding(this.displayModeBinding);
            AddBinding(this.showRoutesBinding);
            AddBinding(this.directionModeBinding);
            AddBinding(this.rangeModeBinding);
            AddBinding(this.associatedStopsBinding);
            AddBinding(this.walkingOnlyBinding);
            AddBinding(this.isTransitStopSelectedBinding);
            AddBinding(this.hasParentBinding);
            AddBinding(this.isRoadSelectedBinding);

            AddBinding(this.trafficJamDataBinding);
            AddBinding(this.monitorPanelActiveBinding);

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setMonitorPanelActive", (bool active) => {
                this.monitorPanelActive = active;
                this.monitorPanelActiveBinding.Update(active);
                if (active)
                {
                    UpdateTrafficJamData();
                }
            }));

            AddBinding(new TriggerBinding<Entity>("TrafficSpy", "focusEntity", (entity) => {
                if (EntityManager.Exists(entity))
                {
                    if (this.cameraUpdateSystem != null && this.cameraUpdateSystem.activeCameraController != null)
                    {
                        if (EntityManager.HasComponent<Game.Objects.Transform>(entity))
                        {
                            var transform = EntityManager.GetComponentData<Game.Objects.Transform>(entity);
                            this.cameraUpdateSystem.activeCameraController.pivot = transform.m_Position;
                        }
                        else if (EntityManager.HasComponent<Game.Notifications.Icon>(entity))
                        {
                            var icon = EntityManager.GetComponentData<Game.Notifications.Icon>(entity);
                            this.cameraUpdateSystem.activeCameraController.pivot = icon.m_Location;
                        }
                        else if (EntityManager.HasComponent<Game.Net.Curve>(entity))
                        {
                            var curve = EntityManager.GetComponentData<Game.Net.Curve>(entity);
                            this.cameraUpdateSystem.activeCameraController.pivot = Colossal.Mathematics.MathUtils.Position(curve.m_Bezier, 0.5f);
                        }
                        else if (EntityManager.HasComponent<Game.Net.Node>(entity))
                        {
                            var node = EntityManager.GetComponentData<Game.Net.Node>(entity);
                            this.cameraUpdateSystem.activeCameraController.pivot = node.m_Position;
                        }
                    }
                }
            }));

            AddBinding(new TriggerBinding<Entity>("TrafficSpy", "selectStop", (entity) => {
                this.toolSystem.selected = entity; 
            }));
            
            AddBinding(new TriggerBinding("TrafficSpy", "selectParent", () => {
                if (lastParentEntity != Entity.Null && EntityManager.Exists(lastParentEntity)) {
                    this.toolSystem.selected = lastParentEntity;
                }
            }));

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "sethighlightAgents", (bool active) => {
                this.highlightAgents = active;
                this.highlightAgentsBinding.Update(active);
                ApplyFilter();
            }));

            AddBinding(new TriggerBinding<int>("TrafficSpy", "setDisplayMode", (int mode) => {
                this.displayMode = mode;
                this.displayModeBinding.Update(mode);
                CalculateStats();
                ApplyFilter();
            }));
            
            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setShowRoutes", (bool active) => {
                this.ShowRoutes = active;
                this.showRoutesBinding.Update(active);
                // We set IsDirty to true so the RouteSystem knows to check immediately
                IsDirty = true; 
                ApplyFilter();
            }));

            // Handler for setting direction mode
            AddBinding(new TriggerBinding<int>("TrafficSpy", "setDirectionMode", (int mode) => {
                this.directionMode = mode;
                this.directionModeBinding.Update(mode);
                if (lastSelectedEntity != Entity.Null)
                {
                    RunAnalysis(lastSelectedEntity);
                }
            }));

            // Handler for setting range mode
            AddBinding(new TriggerBinding<int>("TrafficSpy", "setRangeMode", (int mode) => {
                this.rangeMode = mode;
                this.rangeModeBinding.Update(mode);
                UpdateRangeDistance();
                if (lastSelectedEntity != Entity.Null)
                {
                    RunAnalysis(lastSelectedEntity);
                }
            }));

            AddBinding(new TriggerBinding<string>("TrafficSpy", "setTrafficFilter", (string filter) => {
                if (filter == "RESET" || string.IsNullOrEmpty(filter)) this.currentFilter = "";
                else if (this.currentFilter == filter) this.currentFilter = "";
                else this.currentFilter = filter;
                ApplyFilter();
            }));

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setWalkingOnly", (bool active) => {
                this.walkingOnly = active;
                this.walkingOnlyBinding.Update(active);
                if (lastSelectedEntity != Entity.Null)
                {
                    RunAnalysis(lastSelectedEntity);
                }
            }));
            
            /*this.grayWorldBinding = new ValueBinding<bool>("TrafficSpy", "grayWorld", false);
            AddBinding(this.grayWorldBinding);
            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setGrayWorld", (bool active) => {
                this.grayWorldEnabled = active;
                this.grayWorldBinding.Update(active);
                if (this.isToolActive)
                {
                    ToggleGrayWorld(active);
                }
            }));*/

            AddBinding(new TriggerBinding<bool>("TrafficSpy", "setToolActive", SetToolActive));
        }

        protected override string group => "TrafficSpy.Systems.TrafficUISystem";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }

        protected bool ShouldBeVisible(Entity entity)
        {
            if (EntityManager.HasComponent<Game.Routes.TransportStop>(entity)) return true;
            if (EntityManager.HasComponent<Game.Buildings.TransportStation>(entity)) return true;

            return EntityManager.Exists(entity)
                   && EntityManager.HasBuffer<Game.Net.SubLane>(entity)
                   && !EntityManager.HasComponent<Building>(entity) 
                   && (EntityManager.HasComponent<Game.Net.Edge>(entity) || EntityManager.HasComponent<Game.Net.Node>(entity));
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (this.toolSystem != null)
            {
                this.toolSystem.EventToolChanged -= OnToolChanged;
            }
        }

        protected override void OnUpdate()
        {
            if (!Enabled) Enabled = true;

            // 1. CHECK KEYBOARD INPUT
            if (Mod.m_ToggleAction != null)
            {
                bool isPressed = Mod.m_ToggleAction.IsPressed();
                // Only trigger if pressed NOW but wasn't pressed LAST frame
                if (isPressed && !wasToggleKeyDown) SetToolActive(!isToolActive);
                wasToggleKeyDown = isPressed;
            }
            
            base.OnUpdate();

            monitorTimer += UnityEngine.Time.deltaTime;
            if (monitorTimer >= 5.0f)
            {
                monitorTimer = 0f;
                UpdateTrafficJamData();
            }
            
            // Auto-Reactivate Tool
            if (_isSpyModeActive)
            {
                // If we are currently in Default Tool (viewing info or idle)
                if (toolSystem.activeTool == defaultToolSystem && toolSystem.selected == Entity.Null)
                {
                    // And nothing is selected (User just pressed Esc to close Info Panel)
                    // Reactivate the Spy Tool immediately
                    trafficSpyToolSystem.Enable();
                }
            }

            Entity selected = this.toolSystem.selected;

            if (ShouldBeVisible(selected))
            {
                this.visible = true;
                this.isRoadSelectedBinding.Update(true);
            }
            else
            {
                this.visible = false;
                this.isRoadSelectedBinding.Update(false);
                ClearData();
                return;
            }

            if (selected != lastSelectedEntity)
            {
                bool isDirectStop = EntityManager.HasComponent<Game.Routes.TransportStop>(selected);
                
                // Track Parent Entity for the "Back" button
                if (!isDirectStop) lastParentEntity = selected;
                this.hasParentBinding.Update(isDirectStop && lastParentEntity != Entity.Null && EntityManager.Exists(lastParentEntity));

                lastSelectedEntity = selected;
                currentFilter = "";

                // Reset direction to All Sides (0)
                this.directionMode = 0;
                this.directionModeBinding.Update(0);
                
                bool isStopOrStation = isDirectStop || EntityManager.HasComponent<Game.Buildings.TransportStation>(selected);
                this.isTransitStopSelectedBinding.Update(isStopOrStation);

                FindAssociatedStops(selected);
                RunAnalysis(selected);
            }
        }
        
        private void OnToolChanged(ToolBaseSystem newTool)
        {
            if (_isSpyModeActive)
            {
                if (newTool != trafficSpyToolSystem && newTool != defaultToolSystem)
                {
                    SetSpyMode(false);
                }
            }
        }

        public void SetSpyMode(bool active)
        {
            if (_isSpyModeActive != active)
            {
                _isSpyModeActive = active;
                this.isToolActive = active;
                this.toolActiveBinding.Update(active);

                if (active) trafficSpyToolSystem.Enable();
                else
                {
                    // If disabling, ensure we switch back to default tool if we were spying
                    if (toolSystem.activeTool == trafficSpyToolSystem) trafficSpyToolSystem.Disable();
                    ClearData();
                }
            }
        }
        
        private void SetToolActive(bool active) => SetSpyMode(active);
        public void SyncToolState(bool active) => SetSpyMode(active);
        
        private void ClearData()
        {
            lastSelectedEntity = Entity.Null;
            lastParentEntity = Entity.Null;
            currentFilter = "";
            FilterPosition = float3.zero;
            if (this.activityDataBinding.value != "{}")
            {
                this.activityDataBinding.Update("{}");
                allAnalysisResults.Clear();
                CurrentRenderList.Clear();
                AnalyzedLanes.Clear(); // Clear lanes
                IsDirty = true;
                this.associatedStopsBinding.Update("[]"); 
                this.hasParentBinding.Update(false);
            }
        }

        private void UpdateRangeDistance()
        {
            switch (rangeMode)
            {
                case 0: FilterDistance = float.MaxValue; break; // Lane Data Only (distance doesn't matter, handled by job skipping PathElements)
                case 1: FilterDistance = 1000f; break; // 1km
                case 2: FilterDistance = 2000f; break; // 2km
                case 3: FilterDistance = float.MaxValue; break; // Unlimited
                default: FilterDistance = 2000f; break;
            }
        }
        
        private bool IsItemVisible(TrafficRenderData item, ComponentLookup<Transform> transformLookup)
        {
            bool isPed = item.isPedestrian;

            bool isStopOrStation = lastSelectedEntity != Entity.Null && 
                                  (EntityManager.HasComponent<Game.Routes.TransportStop>(lastSelectedEntity) || 
                                   EntityManager.HasComponent<Game.Buildings.TransportStation>(lastSelectedEntity));

            // Strictly enforce pedestrian/passenger mode when viewing a stop or station
            if (isStopOrStation)
            {
                if (!isPed) return false; 
            }
            else
            {
                // Display Mode Check
                if (this.displayMode == 0 && !item.isVehicle) return false; 
                if (this.displayMode == 1 && !isPed) return false; 
                if (this.displayMode == 1 && this.walkingOnly && item.waitingAtStop != Entity.Null) return false;
            }

            if (item.sourceAgent == Entity.Null) return true;

            // Range Check
            if (!isStopOrStation && FilterDistance < 1000000f)
            {
                Entity entityToCheck = item.sourceAgent != Entity.Null ? item.sourceAgent : item.entity;
                if (transformLookup.TryGetComponent(entityToCheck, out Transform trans))
                {
                    if (math.distancesq(trans.m_Position, FilterPosition) > (FilterDistance * FilterDistance))
                        return false;
                }
                else 
                {
                    // unspawned / in a building have no transform
                    // Return false here so they get excluded if range isn't unlimited.
                    return false;
                }
            }
            return true;
        }

        private void ApplyFilter()
        {
            CurrentRenderList.Clear();
            if (allAnalysisResults == null) return;
            ComponentLookup<Transform> transformLookup = SystemAPI.GetComponentLookup<Transform>(true);

            foreach (var item in allAnalysisResults)
            {
                // Use centralized visibility check
                if (!IsItemVisible(item, transformLookup)) continue;

                bool matchesFilter = false;
                if (string.IsNullOrEmpty(currentFilter)) matchesFilter = true;
                else matchesFilter = MatchesFilter(item, currentFilter);

                if (!matchesFilter) continue;

                if (item.isDestination) CurrentRenderList.Add(item);
                else if (!string.IsNullOrEmpty(currentFilter) || this.highlightAgents || this.ShowRoutes) CurrentRenderList.Add(item);
            }
            IsDirty = true;
        }

        private bool MatchesFilter(TrafficRenderData item, string filter)
        {
            switch (filter)
            {
                case "none": return item.purpose == Purpose.None && item.type != TrafficType.Service && item.type != TrafficType.Cargo && item.type != TrafficType.PublicTransport;
                case "shopping": return item.purpose == Purpose.Shopping;
                case "leisure": return item.purpose == Purpose.Leisure || item.purpose == Purpose.Relaxing || item.purpose == Purpose.Sleeping || item.purpose == Purpose.WaitingHome;
                case "goingHome": return item.purpose == Purpose.GoingHome && !item.isTourist;
                case "goingToWork": return item.purpose == Purpose.GoingToWork || item.purpose == Purpose.Working;
                case "movingIn": return item.isMovingIn;
                case "movingAway": return item.purpose == Purpose.MovingAway && !item.isTourist;
                case "school": return item.purpose == Purpose.GoingToSchool || item.purpose == Purpose.Studying;
                case "transporting": return (item.type == TrafficType.Cargo && item.purpose == Purpose.Delivery) || (item.type == TrafficType.Citizen && (item.purpose == Purpose.Delivery || item.purpose == Purpose.Exporting || item.purpose == Purpose.UpkeepDelivery || item.purpose == Purpose.StorageTransfer || item.purpose == Purpose.Collect || item.purpose == Purpose.CompanyShopping));
                case "returning": return item.type == TrafficType.Cargo && item.purpose == Purpose.None;
                case "tourism": 
                    // Includes standard tourism purposes OR Tourists going home (leaving city)
                    return item.purpose == Purpose.Sightseeing || item.purpose == Purpose.Traveling || item.purpose == Purpose.VisitAttractions || ((item.purpose == Purpose.GoingHome || item.purpose == Purpose.MovingAway) && item.isTourist);  
                case "services": return item.type == TrafficType.Service || item.purpose == Purpose.Hospital || item.purpose == Purpose.InHospital || item.purpose == Purpose.Deathcare || item.purpose == Purpose.ReturnGarbage || item.purpose == Purpose.InDeathcare || item.purpose == Purpose.ReturnUnsortedMail || item.purpose == Purpose.ReturnLocalMail || item.purpose == Purpose.ReturnOutgoingMail || item.purpose == Purpose.SendMail;
                case "other":
                    if (item.type == TrafficType.Cargo || item.type == TrafficType.Service) return false;
                    if (item.type == TrafficType.PublicTransport) return true;
                    return item.type == TrafficType.Citizen && item.purpose != Purpose.None && item.purpose != Purpose.Shopping && !(item.purpose == Purpose.Leisure || item.purpose == Purpose.Relaxing || item.purpose == Purpose.Sleeping || item.purpose == Purpose.WaitingHome) && item.purpose != Purpose.GoingHome && !(item.purpose == Purpose.GoingToWork || item.purpose == Purpose.Working) && item.purpose != Purpose.MovingAway && !(item.purpose == Purpose.GoingToSchool || item.purpose == Purpose.Studying) && !(item.purpose == Purpose.Sightseeing || item.purpose == Purpose.Traveling || item.purpose == Purpose.VisitAttractions) && !(item.purpose == Purpose.Delivery || item.purpose == Purpose.Exporting || item.purpose == Purpose.UpkeepDelivery || item.purpose == Purpose.StorageTransfer || item.purpose == Purpose.Collect || item.purpose == Purpose.CompanyShopping) && !(item.purpose == Purpose.Hospital || item.purpose == Purpose.InHospital || item.purpose == Purpose.Deathcare || item.purpose == Purpose.ReturnGarbage || item.purpose == Purpose.InDeathcare || item.purpose == Purpose.ReturnUnsortedMail || item.purpose == Purpose.ReturnLocalMail || item.purpose == Purpose.ReturnOutgoingMail || item.purpose == Purpose.SendMail);
                default: return false;
            }
        }
        
        private NativeHashSet<Entity> GetTargetEntities(Entity segment, Allocator allocator, int directionMode)
        {
            NativeHashSet<Entity> targets = new NativeHashSet<Entity>(16, allocator);
            
            if (directionMode == 0 || EntityManager.HasComponent<Game.Net.Node>(segment))
            {
                targets.Add(segment);

                // 1. MACRO-PATHFINDING (Highways & Long Distances)
                // CS2 groups highways into "Aggregate" entities. Cars far away may target this aggregate instead of the specific micro-segment
                if (EntityManager.HasComponent<Game.Net.Aggregated>(segment))
                {
                    Game.Net.Aggregated aggregated = EntityManager.GetComponentData<Game.Net.Aggregated>(segment);
                    if (aggregated.m_Aggregate != Entity.Null)
                    {
                        targets.Add(aggregated.m_Aggregate);
                    }
                }
            }
            
            // Get the main road segment's geometry
            if (EntityManager.TryGetBuffer(segment, true, out DynamicBuffer<Game.Net.SubLane> lanes))
            {
                // EDGE CASE (Standard Road Segment)
                if (EntityManager.HasComponent<Curve>(segment))
                {
                    Curve segmentCurve = EntityManager.GetComponentData<Curve>(segment);
                    // Calculate the "Center" and "Right Vector" of the road at the midpoint (t=0.5)
                    // Position(0.5)
                    float3 segmentPos = Colossal.Mathematics.MathUtils.Position(segmentCurve.m_Bezier, 0.5f);
                    // Tangent(0.5) gives the forward direction
                    float3 segmentTan = Colossal.Mathematics.MathUtils.Tangent(segmentCurve.m_Bezier, 0.5f);
                    // Cross product with Up (0,1,0) gives the Right Vector
                    float3 segmentRight = math.cross(segmentTan, new float3(0, 1, 0));

                    for (int i = 0; i < lanes.Length; i++)
                    {
                        Entity subLaneEntity = lanes[i].m_SubLane;
                        if (directionMode != 0)
                        {
                            // We check the geometry of the sub-lane
                            if (EntityManager.HasComponent<Curve>(subLaneEntity))
                            {
                                Curve laneCurve = EntityManager.GetComponentData<Curve>(subLaneEntity);
                                float3 lanePos = Colossal.Mathematics.MathUtils.Position(laneCurve.m_Bezier, 0.5f);
                                
                                // Calculate vector from Road Center -> Lane Center
                                float3 diff = lanePos - segmentPos;
                                
                                // Dot product determines side:
                                // > 0 means the lane is on the Right side of the road center
                                // < 0 means the lane is on the Left side of the road center
                                float dot = math.dot(diff, segmentRight);
                                
                                // Threshold 0.1f ignores tiny floating point errors for exact center lanes

                                // Mode 1 (Side A): We want ONE side (e.g. Left). So skip if dot is Positive (Right).
                                if (directionMode == 1 && dot > 0.1f) continue;
                                
                                // Mode 2 (Side B): We want OTHER side (e.g. Right). So skip if dot is Negative (Left).
                                if (directionMode == 2 && dot < -0.1f) continue;
                            }
                        }
                        targets.Add(subLaneEntity);
                    }
                }
                // NODE CASE (Intersection / Roundabout)
                else if (EntityManager.HasComponent<Game.Net.Node>(segment))
                {
                    // Nodes don't have a simple forward/backward direction.
                    // Ignore directionMode and just add all internal connection lanes.
                    for (int i = 0; i < lanes.Length; i++)
                    {
                        targets.Add(lanes[i].m_SubLane);
                    }
                }
            }
            return targets;
        }

        
        private bool IsValidTransitStop(Entity entity)
        {
            return EntityManager.HasComponent<Game.Routes.BusStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.TrainStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.TramStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.SubwayStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.ShipStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.AirplaneStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.FerryStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.TaxiStand>(entity);
        }

        private string GetStopPrefix(Entity stopEntity)
        {
            if (EntityManager.HasComponent<Game.Routes.BusStop>(stopEntity)) return "Bus Stop";
            if (EntityManager.HasComponent<Game.Routes.SubwayStop>(stopEntity)) return "Subway";
            if (EntityManager.HasComponent<Game.Routes.TrainStop>(stopEntity)) return "Platform";
            if (EntityManager.HasComponent<Game.Routes.TramStop>(stopEntity)) return "Tram";
            if (EntityManager.HasComponent<Game.Routes.ShipStop>(stopEntity)) return "Ship";
            if (EntityManager.HasComponent<Game.Routes.FerryStop>(stopEntity)) return "Ferry";
            if (EntityManager.HasComponent<Game.Routes.AirplaneStop>(stopEntity)) return "Gate";
            if (EntityManager.HasComponent<Game.Routes.TaxiStand>(stopEntity)) return "Taxi";
            return "Stop";
        }
        
        private void FindAssociatedStops(Entity selected)
        {
            m_AssociatedStops.Clear();
            if (selected == Entity.Null) {
                this.associatedStopsBinding.Update("[]");
                return;
            }

            bool isDirectStop = EntityManager.HasComponent<Game.Routes.TransportStop>(selected);

            if (isDirectStop && IsValidTransitStop(selected))
            {
                m_AssociatedStops.Add(new StopOption { Entity = selected, Name = "" });
            }

            var stopEntities = m_AllStopsQuery.ToEntityArray(Allocator.Temp);
            
            NativeList<Entity> targetParts = new NativeList<Entity>(Allocator.Temp);
            targetParts.Add(selected);
            
            if (EntityManager.HasComponent<Game.Net.Edge>(selected))
            {
                Game.Net.Edge edge = EntityManager.GetComponentData<Game.Net.Edge>(selected);
                targetParts.Add(edge.m_Start);
                targetParts.Add(edge.m_End);
            }
            if (EntityManager.TryGetBuffer(selected, true, out DynamicBuffer<Game.Objects.SubObject> subObjs))
            {
                foreach(var sub in subObjs) targetParts.Add(sub.m_SubObject);
            }

            for (int i = 0; i < stopEntities.Length; i++)
            {
                Entity stopEntity = stopEntities[i];
                
                // Exclude bicycle parking and strictly enforce whitelist
                if (!IsValidTransitStop(stopEntity)) continue;

                Entity currentChecker = stopEntity;
                if (EntityManager.HasComponent<Game.Routes.Connected>(stopEntity))
                    currentChecker = EntityManager.GetComponentData<Game.Routes.Connected>(stopEntity).m_Connected;

                bool matchFound = false;
                
                for (int depth = 0; depth < 4; depth++)
                {
                    Entity nextChecker = Entity.Null;

                    if (EntityManager.HasComponent<Owner>(currentChecker))
                        nextChecker = EntityManager.GetComponentData<Owner>(currentChecker).m_Owner;
                    else if (EntityManager.HasComponent<Attached>(currentChecker))
                        nextChecker = EntityManager.GetComponentData<Attached>(currentChecker).m_Parent;

                    if (nextChecker != Entity.Null)
                    {
                        if (targetParts.Contains(nextChecker))
                        {
                            matchFound = true;
                            break;
                        }
                        currentChecker = nextChecker;
                    }
                    else break;
                }

                if (matchFound)
                {
                    bool exists = false;
                    foreach(var s in m_AssociatedStops) if(s.Entity == stopEntity) exists = true;
                    if (!exists) 
                    {
                        m_AssociatedStops.Add(new StopOption { Entity = stopEntity, Name = "" });
                    }
                }
            }
            targetParts.Dispose();
            
            // hide the UI stop list if the user has explicitly selected a single stop
            if (isDirectStop)
            {
                this.associatedStopsBinding.Update("[]");
            }
            else
            {
                // Sort by Entity Index so stops remain consistently in the exact same order visually
                m_AssociatedStops.Sort((a, b) => a.Entity.Index.CompareTo(b.Entity.Index));
                
                var sb = new System.Text.StringBuilder("[");
                for(int i = 0; i < m_AssociatedStops.Count; i++)
                {
                    var stop = m_AssociatedStops[i];
                    stop.Name = $"{GetStopPrefix(stop.Entity)} {i + 1}";
                    m_AssociatedStops[i] = stop; // save generated name
                    
                    sb.Append($"{{\"index\":{stop.Entity.Index}, \"version\":{stop.Entity.Version}, \"name\":\"{stop.Name}\"}},");
                }
                if (m_AssociatedStops.Count > 0) sb.Length--; 
                sb.Append("]");
                
                this.associatedStopsBinding.Update(sb.ToString());
            }
        }

        private void FindStopsRecursive(Entity entity, ref NativeHashSet<Entity> results)
        {
            if (EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<ConnectedRoute> routes))
            {
                foreach(var route in routes) results.Add(route.m_Waypoint);
            }
            if (EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<Game.Objects.SubObject> subObjects))
            {
                foreach(var sub in subObjects) FindStopsRecursive(sub.m_SubObject, ref results);
            }
        }

        private void CalculateStats()
        {
            int cntNone = 0, cntShopping = 0, cntLeisure = 0, cntGoingHome = 0, cntGoingToWork = 0, cntMovingIn = 0, cntMovingAway = 0, cntSchool = 0, cntTransporting = 0, cntReturning = 0, cntTourism = 0, cntOther = 0, cntServices = 0;

            ComponentLookup<Transform> transformLookup = SystemAPI.GetComponentLookup<Transform>(true);

            foreach (var item in allAnalysisResults)
            {
                // Use centralized visibility check (respects Range and DisplayMode)
                if (!IsItemVisible(item, transformLookup)) continue;
                if (item.isDestination) continue;

                if (item.type == TrafficType.Service) { cntServices++; continue; }
                if (item.type == TrafficType.PublicTransport) { cntOther++; continue; }
                if (item.type == TrafficType.Cargo) {
                    if (item.purpose == Purpose.Delivery) cntTransporting++;
                    else cntReturning++;
                    continue;
                }

                switch (item.purpose) {
                    case Purpose.None: cntNone++; break;
                    case Purpose.Shopping: cntShopping++; break;
                    case Purpose.Leisure:
                    case Purpose.Sleeping:
                    case Purpose.WaitingHome:
                    case Purpose.Relaxing: cntLeisure++; break;
                    case Purpose.GoingHome:
                        if (item.isMovingIn) cntMovingIn++;
                        else if (item.isTourist) cntTourism++; // Tourists leaving counted as Tourism
                        else cntGoingHome++;
                        break;
                    case Purpose.GoingToWork:
                    case Purpose.Working: cntGoingToWork++; break;
                    case Purpose.MovingAway:
                        if (item.isTourist) cntTourism++; 
                        else cntMovingAway++; 
                        break;
                    case Purpose.GoingToSchool:
                    case Purpose.Studying: cntSchool++; break;
                    case Purpose.Sightseeing:
                    case Purpose.Traveling:
                    case Purpose.VisitAttractions: cntTourism++; break;
                    case Purpose.Delivery:
                    case Purpose.Exporting:
                    case Purpose.UpkeepDelivery:
                    case Purpose.StorageTransfer:
                    case Purpose.Collect:
                    case Purpose.CompanyShopping: cntTransporting++; break;
                    case Purpose.ReturnGarbage:
                    case Purpose.Deathcare:
                    case Purpose.InDeathcare:
                    case Purpose.ReturnUnsortedMail:
                    case Purpose.ReturnLocalMail:
                    case Purpose.ReturnOutgoingMail:
                    case Purpose.SendMail:
                    case Purpose.Hospital:
                    case Purpose.InHospital: cntServices++; break;
                    default: cntOther++; break;
                }
            }

            string json = $@"{{""none"": {cntNone}, ""shopping"": {cntShopping}, ""leisure"": {cntLeisure}, ""goingHome"": {cntGoingHome}, ""goingToWork"": {cntGoingToWork}, ""movingIn"": {cntMovingIn}, ""movingAway"": {cntMovingAway}, ""school"": {cntSchool}, ""transporting"": {cntTransporting}, ""returning"": {cntReturning}, ""tourism"": {cntTourism}, ""other"": {cntOther}, ""services"": {cntServices}}}";
            this.activityDataBinding.Update(json);
        }

        private void RunAnalysis(Entity selectedSegment)
        {
            allAnalysisResults.Clear();
            AnalyzedLanes.Clear(); // Clear lanes
            
            // Calculate Center Position for Range Filtering
            if (EntityManager.HasComponent<Game.Net.Node>(selectedSegment)) {
                Game.Net.Node node = EntityManager.GetComponentData<Game.Net.Node>(selectedSegment);
                FilterPosition = node.m_Position;
            } else if (EntityManager.HasComponent<Curve>(selectedSegment)) {
                Curve curve = EntityManager.GetComponentData<Curve>(selectedSegment);
                FilterPosition = Colossal.Mathematics.MathUtils.Position(curve.m_Bezier, 0.5f);
            } else if (SystemAPI.GetComponentLookup<Transform>(true).TryGetComponent(selectedSegment, out Transform tr)) {
                FilterPosition = tr.m_Position;
            } else {
                FilterPosition = float3.zero;
            }
            UpdateRangeDistance();

            NativeQueue<TrafficRenderData> resultsQueue = new NativeQueue<TrafficRenderData>(Allocator.TempJob);

            NativeList<Entity> stopsToAnalyze = new NativeList<Entity>(Allocator.TempJob);
            
            bool isDirectStop = EntityManager.HasComponent<Game.Routes.TransportStop>(selectedSegment);
            bool isStation = EntityManager.HasComponent<Game.Buildings.TransportStation>(selectedSegment);

            // highlight the road segment or its specific lanes
            if (isDirectStop)
            {
                stopsToAnalyze.Add(selectedSegment);
                AnalyzedLanes.Add(selectedSegment); 
            }
            else if (isStation)
            {
                AnalyzedLanes.Add(selectedSegment); 
            }
            
            foreach(var option in m_AssociatedStops)
            {
                bool alreadyAdded = false;
                for(int k=0; k<stopsToAnalyze.Length; k++) if(stopsToAnalyze[k] == option.Entity) alreadyAdded = true;
                if(!alreadyAdded) stopsToAnalyze.Add(option.Entity);
            }

            bool shouldAnalyzeStops = stopsToAnalyze.Length > 0 && (isDirectStop || isStation || !this.walkingOnly);

            JobHandle waitingJobHandle = default;
            NativeList<int> debugList = new NativeList<int>(Allocator.TempJob);

            if (shouldAnalyzeStops)
            {
                Mod.log.Info($"TrafficSpy: Analyzing Queue for {stopsToAnalyze.Length} stops.");
                
                WaitingPassengerJob waitJob = new WaitingPassengerJob
                {
                    searchTargets = stopsToAnalyze.AsArray(),
                    debugList = debugList,
                    
                    queueBufferHandle = SystemAPI.GetBufferTypeHandle<Game.Creatures.Queue>(true), 
                    residentHandle = SystemAPI.GetComponentTypeHandle<Game.Creatures.Resident>(true),
                    
                    creatureHandle = SystemAPI.GetComponentTypeHandle<Creature>(true),
                    humanLaneHandle = SystemAPI.GetComponentTypeHandle<HumanCurrentLane>(true),
                    
                    entityHandle = SystemAPI.GetEntityTypeHandle(),
                    targetHandle = SystemAPI.GetComponentTypeHandle<Target>(true),
                    
                    connectedLookup = SystemAPI.GetComponentLookup<Game.Routes.Connected>(true),
                    travelPurposeLookup = SystemAPI.GetComponentLookup<TravelPurpose>(true),
                    householdMemberLookup = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                    householdLookup = SystemAPI.GetComponentLookup<Household>(true),
                    workerLookup = SystemAPI.GetComponentLookup<Worker>(true), 
                    studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                    propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    pathOwnerHandle = SystemAPI.GetComponentTypeHandle<PathOwner>(true),
                    pathBufferHandle = SystemAPI.GetBufferTypeHandle<PathElement>(true),
                    results = resultsQueue.AsParallelWriter()
                };
                waitingJobHandle = waitJob.Schedule(waitingPassengersQuery, default); 
            }

            JobHandle pathJobHandle = default;
            NativeHashSet<Entity> targets = default; 
            
            if (usePathBasedAnalysis && !isDirectStop && !isStation)
            {
                Mod.log.Info("TrafficSpy: Starting Path-Based Analysis");
                
                // Pass directionMode to GetTargetEntities
                targets = GetTargetEntities(selectedSegment, Allocator.TempJob, this.directionMode);
                if (this.directionMode != 0) {
                    NativeArray<Entity> targetArray = targets.ToNativeArray(Allocator.Temp);
                    foreach (var entity in targetArray) {
                        if (entity != selectedSegment) AnalyzedLanes.Add(entity);
                    }
                    targetArray.Dispose();
                } else {
                    AnalyzedLanes.Add(selectedSegment);
                }

                // for those on the selected segment
                PathActivityJob pathJob = new PathActivityJob
                {
                    targets = targets,
                    checkPathElements = (this.rangeMode != 0),
                    travelPurposeLookup = SystemAPI.GetComponentLookup<TravelPurpose>(true),
                    targetLookup = SystemAPI.GetComponentLookup<Target>(true),
                    householdLookup = SystemAPI.GetComponentLookup<Household>(true),
                    householdMemberLookup = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                    workerLookup = SystemAPI.GetComponentLookup<Worker>(true),
                    studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                    creatureResidentLookup = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(true),
                    touristHouseholdLookup = SystemAPI.GetComponentLookup<TouristHousehold>(true), 
                    propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    ownerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                    buildingLookup = SystemAPI.GetComponentLookup<Building>(true),
                    currentVehicleLookup = SystemAPI.GetComponentLookup<CurrentVehicle>(true),
                    currentTransportLookup = SystemAPI.GetComponentLookup<CurrentTransport>(true),
                    taxiLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Taxi>(true),
                    passengerLookup = SystemAPI.GetBufferLookup<Passenger>(true),
                    deliveryTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true),
                    cargoTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.CargoTransport>(true),
                    publicTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PublicTransport>(true),
                    personalCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(true),
                    hearseLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Hearse>(true),
                    garbageTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.GarbageTruck>(true),
                    policeCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PoliceCar>(true),
                    fireEngineLookup = SystemAPI.GetComponentLookup<Game.Vehicles.FireEngine>(true),
                    ambulanceLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                    postVanLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PostVan>(true),
                    maintenanceVehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.MaintenanceVehicle>(true),
                    carLaneLookup = SystemAPI.GetComponentLookup<CarCurrentLane>(true),
                    humanLaneLookup = SystemAPI.GetComponentLookup<HumanCurrentLane>(true),
                    vehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Vehicle>(true),
                    trainLaneLookup = SystemAPI.GetComponentLookup<Game.Vehicles.TrainCurrentLane>(true),
                    watercraftLaneLookup = SystemAPI.GetComponentLookup<Game.Vehicles.WatercraftCurrentLane>(true),
                    carNavigationLaneLookup = SystemAPI.GetBufferLookup<CarNavigationLane>(true),
                    results = resultsQueue.AsParallelWriter()
                };
                pathJobHandle = pathJob.ScheduleParallel(pathOwnerQuery, default);
            }

            if (shouldAnalyzeStops) 
            {
                waitingJobHandle.Complete();
            }
            debugList.Dispose();

            if (targets.IsCreated) pathJobHandle.Complete();

            while (resultsQueue.TryDequeue(out TrafficRenderData item)) allAnalysisResults.Add(item);
            
            resultsQueue.Dispose();
            stopsToAnalyze.Dispose();
            if (targets.IsCreated) targets.Dispose();
            
            CalculateStats();
            ApplyFilter();
        }

        private struct TrafficJamNotificationItem
        {
            public Entity IconEntity;
            public Entity TargetEntity;
            public string Name;
        }

        private string CleanLocationName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Traffic Bottleneck";

            string cleaned = raw.Trim();

            // 1. If it contains brackets e.g. "Assets.NAME[Two-Lane Road]" or "Assets.ASSET_NAME[Small Road_01]"
            var bracketMatch = Regex.Match(cleaned, @"\[(.*?)\]");
            if (bracketMatch.Success)
            {
                cleaned = bracketMatch.Groups[1].Value;
            }

            // 2. Remove common localization prefixes if any remained
            cleaned = Regex.Replace(cleaned, @"^(Assets|SelectedInfoPanel|Common|Notification|SubNet|Net)\.", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"^(NAME|ASSET_NAME|STREET_NAME|ROAD_NAME|SECTION_NAME)[:_ ]*", "", RegexOptions.IgnoreCase);

            // 3. Replace underscores with spaces
            cleaned = cleaned.Replace('_', ' ').Trim();

            // 4. Strip trailing numbers
            while (cleaned.Length > 0 && char.IsDigit(cleaned[cleaned.Length - 1]))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 1).TrimEnd();
            }

            // 5. Expand CamelCase (excluding after spaces and hyphens)
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cleaned.Length; i++)
            {
                if (i > 0 && char.IsUpper(cleaned[i]) && !char.IsUpper(cleaned[i - 1]) && cleaned[i - 1] != ' ' && cleaned[i - 1] != '-')
                {
                    sb.Append(' ');
                }
                sb.Append(cleaned[i]);
            }

            cleaned = Regex.Replace(sb.ToString().Trim(), @"\s+", " ");
            return string.IsNullOrEmpty(cleaned) ? "Traffic Bottleneck" : cleaned;
        }

        private string GetLocationName(Entity targetEntity, ComponentLookup<Game.Net.Aggregated> aggregatedLookup, ComponentLookup<Game.Prefabs.PrefabRef> prefabRefLookup)
        {
            if (targetEntity != Entity.Null && EntityManager.Exists(targetEntity))
            {
                if (this.nameSystem != null)
                {
                    if (aggregatedLookup.TryGetComponent(targetEntity, out var agg) && agg.m_Aggregate != Entity.Null)
                    {
                        string roadName = this.nameSystem.GetRenderedLabelName(agg.m_Aggregate);
                        if (!string.IsNullOrEmpty(roadName)) return CleanLocationName(roadName);
                    }
                    string entityName = this.nameSystem.GetRenderedLabelName(targetEntity);
                    if (!string.IsNullOrEmpty(entityName)) return CleanLocationName(entityName);
                }

                if (prefabRefLookup.TryGetComponent(targetEntity, out var pRef))
                {
                    var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
                    if (prefabSystem.TryGetPrefab<Game.Prefabs.PrefabBase>(pRef.m_Prefab, out var prefab))
                    {
                        if (!string.IsNullOrEmpty(prefab.name)) return CleanLocationName(prefab.name);
                    }
                }
            }
            return "Traffic Bottleneck";
        }

        private void UpdateTrafficJamData()
        {
            var prefabRefLookup = SystemAPI.GetComponentLookup<Game.Prefabs.PrefabRef>(true);
            var ownerLookup = SystemAPI.GetComponentLookup<Game.Common.Owner>(true);
            var targetLookup = SystemAPI.GetComponentLookup<Game.Common.Target>(true);
            var aggregatedLookup = SystemAPI.GetComponentLookup<Game.Net.Aggregated>(true);

            // 1. COLLECT ACTIVE TRAFFIC JAM NOTIFICATIONS
            List<TrafficJamNotificationItem> jamNotifications = new List<TrafficJamNotificationItem>();
            if (!m_NotificationIconQuery.IsEmptyIgnoreFilter)
            {
                Entity bottleneckPrefab = Entity.Null;
                if (!m_TrafficConfigurationQuery.IsEmptyIgnoreFilter)
                {
                    var trafficConfig = m_TrafficConfigurationQuery.GetSingleton<TrafficConfigurationData>();
                    bottleneckPrefab = trafficConfig.m_BottleneckNotification;
                }

                var notificationIcons = m_NotificationIconQuery.ToEntityArray(Allocator.Temp);
                HashSet<Entity> seenTargets = new HashSet<Entity>();

                for (int i = 0; i < notificationIcons.Length; i++)
                {
                    Entity iconEnt = notificationIcons[i];
                    if (!prefabRefLookup.TryGetComponent(iconEnt, out Game.Prefabs.PrefabRef pRef)) continue;

                    bool isBottleneck = false;
                    if (bottleneckPrefab != Entity.Null && pRef.m_Prefab == bottleneckPrefab)
                    {
                        isBottleneck = true;
                    }
                    else
                    {
                        var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
                        if (prefabSystem.TryGetPrefab<Game.Prefabs.PrefabBase>(pRef.m_Prefab, out var pBase))
                        {
                            if (pBase.name != null && (pBase.name.Contains("Bottleneck") || pBase.name.Contains("TrafficJam") || pBase.name.Contains("Traffic Jam")))
                            {
                                isBottleneck = true;
                            }
                        }
                    }

                    if (!isBottleneck) continue;

                    Entity targetEntity = Entity.Null;
                    if (ownerLookup.TryGetComponent(iconEnt, out Game.Common.Owner owner)) targetEntity = owner.m_Owner;
                    else if (targetLookup.TryGetComponent(iconEnt, out Game.Common.Target target)) targetEntity = target.m_Target;

                    if (targetEntity != Entity.Null && seenTargets.Contains(targetEntity)) continue;
                    if (targetEntity != Entity.Null) seenTargets.Add(targetEntity);

                    string locationName = GetLocationName(targetEntity, aggregatedLookup, prefabRefLookup);
                    jamNotifications.Add(new TrafficJamNotificationItem
                    {
                        IconEntity = iconEnt,
                        TargetEntity = targetEntity,
                        Name = locationName
                    });
                }
                notificationIcons.Dispose();
            }

            // 2. COLLECT LEAD BLOCKERS
            List<KeyValuePair<Entity, int>> sortedBlockers = new List<KeyValuePair<Entity, int>>();
            Dictionary<Entity, Game.Vehicles.BlockerType> blockerTypes = new Dictionary<Entity, Game.Vehicles.BlockerType>();

            if (!m_BlockerVehicleQuery.IsEmptyIgnoreFilter)
            {
                var blockerEntities = m_BlockerVehicleQuery.ToEntityArray(Allocator.Temp);
                var blockerDataLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Blocker>(true);
                var vehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Vehicle>(true);
                Dictionary<Entity, int> blockerCounts = new Dictionary<Entity, int>();

                for (int i = 0; i < blockerEntities.Length; i++)
                {
                    Entity vehicle = GetTowingVehicle(blockerEntities[i]);

                    if (!blockerDataLookup.TryGetComponent(vehicle, out Game.Vehicles.Blocker blocker)) continue;

                    Entity leadBlocker = GetTowingVehicle(blocker.m_Blocker);
                    if (leadBlocker == Entity.Null) continue;

                    Entity current = leadBlocker;
                    int depth = 0;
                    while (depth < 5 && vehicleLookup.HasComponent(current) && blockerDataLookup.TryGetComponent(current, out Game.Vehicles.Blocker nextBlocker))
                    {
                        if (nextBlocker.m_Blocker != Entity.Null && vehicleLookup.HasComponent(nextBlocker.m_Blocker))
                        {
                            current = GetTowingVehicle(nextBlocker.m_Blocker);
                            depth++;
                        }
                        else break;
                    }

                    if (vehicleLookup.HasComponent(current))
                    {
                        current = GetTowingVehicle(current);
                        if (blockerCounts.ContainsKey(current)) blockerCounts[current]++;
                        else
                        {
                            blockerCounts[current] = 1;
                            blockerTypes[current] = blocker.m_Type;
                        }
                    }
                    else if (vehicleLookup.HasComponent(leadBlocker))
                    {
                        leadBlocker = GetTowingVehicle(leadBlocker);
                        if (blockerCounts.ContainsKey(leadBlocker)) blockerCounts[leadBlocker]++;
                        else
                        {
                            blockerCounts[leadBlocker] = 1;
                            blockerTypes[leadBlocker] = blocker.m_Type;
                        }
                    }
                }
                blockerEntities.Dispose();

                sortedBlockers.AddRange(blockerCounts);
                sortedBlockers.Sort((a, b) =>
                {
                    int cmp = b.Value.CompareTo(a.Value);
                    if (cmp != 0) return cmp;
                    return a.Key.Index.CompareTo(b.Key.Index);
                });
            }

            // 3. SERIALIZE JSON PAYLOAD
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("{\"notifications\":[");
            for (int i = 0; i < jamNotifications.Count; i++)
            {
                var notif = jamNotifications[i];
                Entity entityToFocus = notif.TargetEntity != Entity.Null ? notif.TargetEntity : notif.IconEntity;
                sb.Append($"{{\"index\":{entityToFocus.Index},\"version\":{entityToFocus.Version},\"iconIndex\":{notif.IconEntity.Index},\"iconVersion\":{notif.IconEntity.Version},\"name\":\"{EscapeJson(notif.Name)}\"}},");
            }
            if (jamNotifications.Count > 0 && sb[sb.Length - 1] == ',') sb.Length--;

            sb.Append("],\"blockers\":[");
            int count = 0;
            for (int i = 0; i < sortedBlockers.Count && count < 15; i++)
            {
                Entity blockerVeh = sortedBlockers[i].Key;
                int waiting = sortedBlockers[i].Value;
                if (waiting < 1) continue;

                string vehName = GetVehicleDisplayName(blockerVeh, prefabRefLookup);
                string vehType = GetVehicleCategory(blockerVeh);

                Game.Vehicles.BlockerType bType = blockerTypes.TryGetValue(blockerVeh, out var bt) ? bt : Game.Vehicles.BlockerType.Signal;

                string reason = "signal";
                bool isTransitVeh = EntityManager.HasComponent<Game.Vehicles.PublicTransport>(blockerVeh) || EntityManager.HasComponent<Game.Vehicles.Taxi>(blockerVeh);
                bool isSignalBlock = (bType == Game.Vehicles.BlockerType.Signal || bType == Game.Vehicles.BlockerType.Limit || bType == Game.Vehicles.BlockerType.Continuing);

                if (isTransitVeh && !isSignalBlock)
                {
                    reason = "boarding";
                }
                else if (isSignalBlock)
                {
                    reason = "signal";
                }
                else
                {
                    reason = "stopped";
                }

                sb.Append($"{{\"index\":{blockerVeh.Index},\"version\":{blockerVeh.Version},\"name\":\"{EscapeJson(vehName)}\",\"type\":\"{vehType}\",\"waitingCount\":{waiting},\"reason\":\"{reason}\"}},");
                count++;
            }
            if (count > 0 && sb[sb.Length - 1] == ',') sb.Length--;
            sb.Append("]}");

            trafficJamDataBinding.Update(sb.ToString());
        }

        private Entity GetTowingVehicle(Entity vehicle)
        {
            if (vehicle == Entity.Null) return Entity.Null;
            if (EntityManager.HasComponent<Game.Common.Owner>(vehicle))
            {
                Entity owner = EntityManager.GetComponentData<Game.Common.Owner>(vehicle).m_Owner;
                if (owner != Entity.Null && EntityManager.HasComponent<Game.Vehicles.Vehicle>(owner))
                {
                    return owner;
                }
            }
            return vehicle;
        }

        private bool IsTrailerVehicle(Entity vehicle, ComponentLookup<Game.Prefabs.PrefabRef> prefabRefLookup)
        {
            if (prefabRefLookup.TryGetComponent(vehicle, out Game.Prefabs.PrefabRef pRef))
            {
                var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
                var prefab = prefabSystem.GetPrefab<Game.Prefabs.PrefabBase>(pRef);
                if (prefab != null && !string.IsNullOrEmpty(prefab.name))
                {
                    if (prefab.name.ToLower().Contains("trailer")) return true;
                }
            }
            return false;
        }

        private string GetVehicleRichName(Entity vehicle, ComponentLookup<Game.Prefabs.PrefabRef> prefabRefLookup)
        {
            return GetVehicleDisplayName(vehicle, prefabRefLookup);
        }

        private string GetVehicleDisplayName(Entity vehicle, ComponentLookup<Game.Prefabs.PrefabRef> prefabRefLookup)
        {
            if (prefabRefLookup.TryGetComponent(vehicle, out Game.Prefabs.PrefabRef pRef))
            {
                var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
                var prefab = prefabSystem.GetPrefab<Game.Prefabs.PrefabBase>(pRef);
                if (prefab != null && !string.IsNullOrEmpty(prefab.name))
                {
                    string cleaned = CleanPrefabName(prefab.name);
                    if (EntityManager.HasComponent<Game.Vehicles.RoadMaintenanceVehicle>(vehicle) && !cleaned.ToLower().Contains("maintenance"))
                    {
                        return "Road Maintenance " + cleaned;
                    }
                    if (EntityManager.HasComponent<Game.Vehicles.ParkMaintenanceVehicle>(vehicle) && !cleaned.ToLower().Contains("maintenance"))
                    {
                        return "Park Maintenance " + cleaned;
                    }
                    return cleaned;
                }
            }

            if (EntityManager.HasComponent<Game.Vehicles.RoadMaintenanceVehicle>(vehicle)) return "Road Maintenance Vehicle";
            if (EntityManager.HasComponent<Game.Vehicles.ParkMaintenanceVehicle>(vehicle)) return "Park Maintenance Vehicle";
            if (EntityManager.HasComponent<Game.Vehicles.MaintenanceVehicle>(vehicle)) return "Road Maintenance Vehicle";
            if (EntityManager.HasComponent<Game.Vehicles.PoliceCar>(vehicle)) return "Police Car";
            if (EntityManager.HasComponent<Game.Vehicles.FireEngine>(vehicle)) return "Fire Engine";
            if (EntityManager.HasComponent<Game.Vehicles.Ambulance>(vehicle)) return "Ambulance";
            if (EntityManager.HasComponent<Game.Vehicles.GarbageTruck>(vehicle)) return "Garbage Truck";
            if (EntityManager.HasComponent<Game.Vehicles.PostVan>(vehicle)) return "Post Van";
            if (EntityManager.HasComponent<Game.Vehicles.Hearse>(vehicle)) return "Hearse";
            if (EntityManager.HasComponent<Game.Vehicles.Taxi>(vehicle)) return "Taxi";
            if (EntityManager.HasComponent<Game.Vehicles.PublicTransport>(vehicle)) return "Bus";
            if (EntityManager.HasComponent<Game.Vehicles.DeliveryTruck>(vehicle)) return "Delivery Van";
            if (EntityManager.HasComponent<Game.Vehicles.PersonalCar>(vehicle)) return "City Car";
            return "Vehicle";
        }

        private string CleanPrefabName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string name = rawName.Replace('_', ' ').Trim();
            while (name.Length > 0 && char.IsDigit(name[name.Length - 1]))
            {
                name = name.Substring(0, name.Length - 1).TrimEnd();
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]) && name[i - 1] != ' ')
                {
                    sb.Append(' ');
                }
                sb.Append(name[i]);
            }
            return Regex.Replace(sb.ToString().Trim(), @"\s+", " ");
        }

        private string GetVehicleCategory(Entity vehicle)
        {
            if (EntityManager.HasComponent<Game.Vehicles.RoadMaintenanceVehicle>(vehicle) || 
                EntityManager.HasComponent<Game.Vehicles.ParkMaintenanceVehicle>(vehicle) ||
                EntityManager.HasComponent<Game.Vehicles.MaintenanceVehicle>(vehicle)) return "maintenance";
            if (EntityManager.HasComponent<Game.Vehicles.Taxi>(vehicle)) return "taxi";
            if (EntityManager.HasComponent<Game.Vehicles.PublicTransport>(vehicle)) return "bus";
            if (EntityManager.HasComponent<Game.Vehicles.DeliveryTruck>(vehicle)) return "van";
            if (EntityManager.HasComponent<Game.Vehicles.PersonalCar>(vehicle)) return "car";
            return "car";
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
        }
    }

    [Unity.Burst.BurstCompile]
    public struct WaitingPassengerJob : IJobChunk
    {
        [ReadOnly] public NativeArray<Entity> searchTargets; 
        public NativeList<int> debugList; 
        
        [ReadOnly] public BufferTypeHandle<Game.Creatures.Queue> queueBufferHandle; 
        [ReadOnly] public ComponentTypeHandle<Game.Creatures.Resident> residentHandle;
        [ReadOnly] public ComponentTypeHandle<Target> targetHandle;
        [ReadOnly] public EntityTypeHandle entityHandle; 
        
        [ReadOnly] public ComponentTypeHandle<Creature> creatureHandle;
        [ReadOnly] public ComponentTypeHandle<HumanCurrentLane> humanLaneHandle;
        
        [ReadOnly] public ComponentLookup<Game.Routes.Connected> connectedLookup; 
        [ReadOnly] public ComponentLookup<TravelPurpose> travelPurposeLookup;
        [ReadOnly] public ComponentLookup<HouseholdMember> householdMemberLookup;
        [ReadOnly] public ComponentLookup<Household> householdLookup;
        [ReadOnly] public ComponentLookup<Worker> workerLookup;
        [ReadOnly] public ComponentLookup<Game.Citizens.Student> studentLookup;
        
        [ReadOnly] public ComponentLookup<PropertyRenter> propertyRenterLookup;
        [ReadOnly] public ComponentTypeHandle<PathOwner> pathOwnerHandle;
        [ReadOnly] public BufferTypeHandle<PathElement> pathBufferHandle;
        
        public NativeQueue<TrafficRenderData>.ParallelWriter results;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            NativeArray<Game.Creatures.Resident> residents = chunk.GetNativeArray(ref residentHandle);
            NativeArray<Entity> entities = chunk.GetNativeArray(entityHandle); 
            
            NativeArray<Creature> creatures = chunk.GetNativeArray(ref creatureHandle);
            bool hasHumanLanes = chunk.Has(ref humanLaneHandle);
            NativeArray<HumanCurrentLane> humanLanes = hasHumanLanes ? chunk.GetNativeArray(ref humanLaneHandle) : default;
            
            bool hasQueue = chunk.Has(ref queueBufferHandle);
            BufferAccessor<Game.Creatures.Queue> queues = hasQueue ? chunk.GetBufferAccessor(ref queueBufferHandle) : default;

            bool hasPaths = chunk.Has(ref pathBufferHandle);
            BufferAccessor<PathElement> pathBuffers = hasPaths ? chunk.GetBufferAccessor(ref pathBufferHandle) : default;
            
            NativeArray<Target> targetComponents = chunk.GetNativeArray(ref targetHandle);

            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out int i))
            {
                Entity matchedStop = Entity.Null;

                // 1. Check Buffer-Based Queue (Game.Creatures.Queue)
                if (hasQueue)
                {
                    DynamicBuffer<Game.Creatures.Queue> myQueue = queues[i];
                    for (int q = 0; q < myQueue.Length; q++)
                    {
                        Entity intermediateEntity = myQueue[q].m_TargetEntity; 
                        
                        if (connectedLookup.TryGetComponent(intermediateEntity, out var connection))
                        {
                            Entity actualStop = connection.m_Connected;
                            for(int k=0; k<searchTargets.Length; k++) 
                            {
                                if (searchTargets[k] == actualStop || searchTargets[k] == intermediateEntity) 
                                {
                                    matchedStop = searchTargets[k];
                                    break;
                                }
                            }
                        }
                        if (matchedStop != Entity.Null) break;
                    }
                }

                // 2. Check Component-Based Queue (Game.Creatures.Creature.m_QueueEntity)
                if (matchedStop == Entity.Null)
                {
                    Entity queueEntity = creatures[i].m_QueueEntity;
                    if (queueEntity != Entity.Null)
                    {
                        Entity actualStop = queueEntity;
                        if (connectedLookup.TryGetComponent(queueEntity, out var connection))
                        {
                            actualStop = connection.m_Connected;
                        }

                        for(int k=0; k<searchTargets.Length; k++) 
                        {
                            if (searchTargets[k] == actualStop || searchTargets[k] == queueEntity) 
                            {
                                matchedStop = searchTargets[k];
                                break;
                            }
                        }
                    }
                }

                // 3. Fallback: If they have a queue entity (meaning they are waiting for transit), 
                // but it was a TransportLine instead of a stop, check if they are physically standing on the platform.
                if (matchedStop == Entity.Null && creatures[i].m_QueueEntity != Entity.Null && hasHumanLanes)
                {
                    Entity physicalLane = humanLanes[i].m_Lane;
                    for(int k=0; k<searchTargets.Length; k++) 
                    {
                        if (searchTargets[k] == physicalLane) 
                        {
                            matchedStop = searchTargets[k];
                            break;
                        }
                    }
                }

                // If ALL THREE failed, skip them
                if (matchedStop == Entity.Null) continue;

                Entity citizen = residents[i].m_Citizen;
                Purpose purpose = Purpose.None;
                bool isMovingIn = false;
                bool isTourist = false;
                Entity finalDestination = Entity.Null;

                if (travelPurposeLookup.TryGetComponent(citizen, out var tp)) purpose = tp.m_Purpose;

                if (purpose == Purpose.GoingToWork && workerLookup.TryGetComponent(citizen, out var worker))
                {
                    finalDestination = worker.m_Workplace;
                }
                else if ((purpose == Purpose.GoingToSchool || purpose == Purpose.Studying) && studentLookup.TryGetComponent(citizen, out var student))
                {
                    finalDestination = student.m_School;
                }
                else if (purpose == Purpose.GoingHome && householdMemberLookup.TryGetComponent(citizen, out var hm))
                {
                    if (propertyRenterLookup.TryGetComponent(hm.m_Household, out var renter))
                    {
                        finalDestination = renter.m_Property;
                        if (householdLookup.TryGetComponent(hm.m_Household, out var hh))
                        {
                            if ((hh.m_Flags & HouseholdFlags.MovedIn) == 0) isMovingIn = true;
                        }
                    }
                }

                if (finalDestination == Entity.Null)
                {
                    finalDestination = targetComponents[i].m_Target;
                }

                if (finalDestination == Entity.Null && hasPaths && i < pathBuffers.Length)
                {
                    DynamicBuffer<PathElement> path = pathBuffers[i];
                    if (path.Length > 0)
                    {
                        finalDestination = path[path.Length - 1].m_Target;
                    }
                }

                results.Enqueue(new TrafficRenderData
                {
                    entity = citizen,
                    sourceAgent = entities[i], 
                    destinationEntity = finalDestination, 
                    waitingAtStop = matchedStop, 
                    purpose = purpose,
                    type = TrafficType.Citizen,
                    isPedestrian = true,
                    isMovingIn = isMovingIn,
                    isTourist = isTourist
                });
            }
        }
    }
}
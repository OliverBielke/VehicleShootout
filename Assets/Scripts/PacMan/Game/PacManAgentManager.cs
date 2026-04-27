using System.Collections.Generic;
using System.Linq;
using PacMan.Agent;
using PacMan.Game;
using PacMan.Interface.PacMan;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace PacMan.Local
{
    public class PacManAgentManager : MonoBehaviour
    {
        private const float MaxHealthValue = 100f;
        private const float HealthRegenDelaySeconds = 5f;
        private const float HealthRegenPerSecond = 10f;
        private const float LaserBeamRadius = 0.05f;
        private const float LaserBeamMinHalfLength = 0.01f;
        private const float LaserBeamVerticalOffset = 0.15f;

        public bool isScared;
        public double scaredUntil;
        public List<GameObject> foodCarried;
        public bool isGhost;
        public int serverIndex = -1;
        private int _lastRespawnStep;
        private float _currentHealth = MaxHealthValue;
        private double _lastDamageTime = double.NegativeInfinity;
        private PacManAgentManager _currentFiringTarget;
        private Transform _laserBeamTransform;
        private Renderer _laserBeamRenderer;
        private HealthBar _healthBar;

        private PacManAI _pacManAI;
        private bool _aiInitialized;

        protected internal PacManMovementController Movement;
        protected internal PacManGameManager PacManGameManager;
        protected GameObject GhostObject;
        protected GameObject PacManObject;

        public Vector3 globalStartPosition;

        private float _nextObservationTime;
        private readonly float _observationUpdateInterval = 1;
        private readonly float _observationSpread = 10;

        private PacManObservations _latestKnownObservation = new()
        {
            Observations = System.Array.Empty<PacManObservation>(),
            AgentServerIndex = -1
        };

        private bool _ready;
        public PacManAction action;

        public void Initialize(PacManGameManager pacManGameManager, bool deferAIInitialization = false)
        {
            PacManGameManager = pacManGameManager;

            Movement = GetComponent<PacManMovementController>();

            GhostObject = transform.Find("visuals/ghost").gameObject;
            PacManObject = transform.Find("visuals/pacman").gameObject;
            foodCarried = new List<GameObject>();
            _aiInitialized = false;
            _lastRespawnStep = PacManGameManager != null ? PacManGameManager.CurrentSimulationStep : 0;
            ResetMatchScopedState();
            EnsureCombatVisualReferences();
            ResetHealth();
            ClearFiringTarget();

            ConfigureForCurrentMode();

            if (!deferAIInitialization)
            {
                InitializeAIIfNeeded();
            }

            _ready = true;
        }

        private void ResetMatchScopedState()
        {
            _nextObservationTime = 0f;
            _latestKnownObservation = new PacManObservations
            {
                Index = 0,
                ObservationFixedTime = 0f,
                AgentServerIndex = serverIndex,
                Observations = System.Array.Empty<PacManObservation>()
            };
        }

        protected virtual void ConfigureForCurrentMode()
        {
        }

        public void InitializeAIIfNeeded()
        {
            if (_aiInitialized)
            {
                return;
            }

            InitializeAI();
            _aiInitialized = true;
        }

        public virtual void InitializeAI()
        {
            _pacManAI = GetComponent<PacManAI>();
            _pacManAI?.Initialize(PacManGameManager.mapManager);
        }

        public void FixedUpdate()
        {
            if (!_ready) return;

            if (PacManGameManager != null && PacManGameManager.UsesManualSimulation)
            {
                return;
            }

            CompleteSimulationStep();
        }

        public void UpdateObservations()
        {
            UpdateAgentState();
            var simulationTime = GetSimulationTime();
            if (_nextObservationTime <= simulationTime)
            {
                var pacManObservations = ProducePacManObservations();
                _latestKnownObservation = pacManObservations;
                _nextObservationTime = simulationTime + (_nextObservationTime == 0 ? Random.value * _observationUpdateInterval : _observationUpdateInterval);
            }
        }

        private PacManObservations ProducePacManObservations()
        {
            var pacManObservations = new PacManObservations
            {
                Index = _latestKnownObservation.Index + 1,
                ObservationFixedTime = GetSimulationTime(),
                AgentServerIndex = serverIndex
            };
            var doCheckVisibility = DoCheckVisibility(transform);
            doCheckVisibility = doCheckVisibility.ToList().FindAll(pair => !pair.agent.CompareTag(tag));
            pacManObservations.Observations = doCheckVisibility
                .Select(pair =>
                {
                    var agent = pair.agent;
                    var velocity = agent.GetComponent<Rigidbody>().linearVelocity;

                    var pacManObservation = new PacManObservation
                    {
                        Visible = pair.visible,
                        IsGhost = agent.isGhost,
                        HasFood = agent.foodCarried.Count > 0,
                        Position = agent.gameObject.transform.localPosition,
                        ServerIndex = agent.serverIndex,
                        LastRespawnStep = agent.GetLastRespawnStep()
                    };
                    if (!pair.visible)
                    {
                        if (velocity.magnitude <= 0.71f)
                        {
                            pacManObservation.Position = Vector3.zero;
                        }
                        else
                        {
                            var dispersion = _observationSpread * 0.5f + _observationSpread * 0.5f * (2.34f - velocity.magnitude) / 1.63f;
                            pacManObservation.ReadingDispersion = dispersion;
                            pacManObservation.Position += new Vector3(Random.value * dispersion - dispersion / 2, 0, Random.value * dispersion - dispersion / 2);
                        }
                    }

                    return pacManObservation;
                })
                .ToArray();
            return pacManObservations;
        }

        protected void UpdateAgentState()
        {
            if (isScared && scaredUntil < GetSimulationTime())
            {
                isScared = false;
                scaredUntil = 0.0;
            }

            if (gameObject.CompareTag("Red") && gameObject.transform.localPosition.x > 0.3f)
            {
                isGhost = true;
            }
            else if (gameObject.CompareTag("Red"))
            {
                isGhost = false;
            }

            if (gameObject.CompareTag("Blue") && gameObject.transform.localPosition.x < -0.3f)
            {
                isGhost = true;
            }
            else if (gameObject.CompareTag("Blue"))
            {
                isGhost = false;
            }
        }

        public virtual void UpdateFoodDelivered()
        {
            if (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && gameObject.transform.localPosition.x > -0.3f ||
                TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && gameObject.transform.localPosition.x < 0.3f)
            {
                if (foodCarried.Count > 0)
                {
                    PacManGameManager.DropFood(this, true);
                }
            }
        }

        public virtual void UpdateAction(PacManAction? overrideAction = null)
        {
            var nextAction = SampleAction(overrideAction);
            Movement.ApplyDesiredControl(nextAction.Acceleration);
        }

        public virtual PacManAction SampleAction(PacManAction? overrideAction = null)
        {
            if (overrideAction.HasValue)
            {
                action = overrideAction.Value;
            }
            else if (_pacManAI != null)
            {
                action = _pacManAI.Tick();
            }

            return action;
        }

        public void CompleteSimulationStep()
        {
            UpdateAgentState();
            UpdateFoodDelivered();
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            EnsureCombatVisualReferences();
            GhostObject.SetActive(isGhost);
            PacManObject.SetActive(!isGhost);
            UpdateHealthPresentation();
            UpdateLaserBeamVisual();
        }

        private IEnumerable<(PacManAgentManager agent, bool visible)> DoCheckVisibility(Transform sourceTransform)
        {
            return PacManGameManager.agents.Select(agent => agent.GetComponent<PacManAgentManager>()).ToList()
                .FindAll(agent => agent.gameObject.activeInHierarchy && !agent.gameObject.CompareTag(tag))
                .Select(agent =>
                    {
                        Physics.Raycast(agent.transform.position, sourceTransform.position - agent.transform.position, out RaycastHit hit, Mathf.Infinity, -1, QueryTriggerInteraction.Ignore);
                        return (agent, hit.transform != null && hit.transform == sourceTransform);
                    }
                );
        }

        public virtual void OnTriggerEnter(Collider other)
        {
            if (!IsGhost() && other.gameObject.name == "Food" && TeamAssignmentUtil.CheckTeam(other.gameObject) != TeamAssignmentUtil.CheckTeam(gameObject))
            {
                PacManGameManager.EatFood(other.gameObject);
                foodCarried.Add(other.gameObject);
            }

            if (!IsGhost() && other.gameObject.name == "Capsule")
            {
                PacManGameManager.EatCapsule(this, other.gameObject);
            }

            if (IsGhost() && other.gameObject.name == "Capsule")
            {
                PacManGameManager.RespawnAgentAtStart(this);
            }
        }

        public virtual void OnCollisionStay(Collision other)
        {
            var otherAgent = other.gameObject.GetComponent<PacManAgentManager>();
            if (otherAgent != null && other.gameObject.tag != tag && !otherAgent.isScared && (!IsGhost() && otherAgent.IsGhost() ||
                                                                                              IsGhost() && otherAgent.IsGhost() ||
                                                                                              !IsGhost() && !otherAgent.IsGhost()))
            {
                PacManGameManager.DropFood(this, false);
                PacManGameManager.RespawnAgentAtStart(this);
            }

            if (otherAgent != null && other.gameObject.tag != tag && IsGhost() && isScared && !otherAgent.IsGhost())
            {
                PacManGameManager.RespawnAgentAtStart(this);
            }
        }

        public bool IsGhost()
        {
            return isGhost;
        }

        public List<PacManAgentManager> GetTeamAgents()
        {
            if (CompareTag("Red")) return PacManGameManager?.redAgents;
            return PacManGameManager?.blueAgents;
        }

        public List<PacManAgentManager> GetFriendlyAgents()
        {
            return PacManGameManager?.agents
                .Select(agent => agent.GetComponent<PacManAgentManager>())
                .ToList()
                .FindAll(obj => obj.CompareTag(tag) && obj.gameObject != gameObject);
        }

        public List<PacManAgentManager> GetVisibleEnemyAgents()
        {
            return (GetTeamAgents() ?? new List<PacManAgentManager>())
                .SelectMany(teamAgent => DoCheckVisibility(teamAgent.transform)
                    .Where(pair => pair.visible)
                    .Select(pair => pair.agent))
                .Distinct()
                .ToList();
        }

        public bool IsScared()
        {
            return isScared;
        }

        public bool IsPoweredUp()
        {
            return TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && PacManGameManager.blueAgents[0].GetComponent<PacManAgentManager>().IsScared() ||
                   TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && PacManGameManager.redAgents[0].GetComponent<PacManAgentManager>().IsScared();
        }

        public float GetScaredRemainingDuration()
        {
            return (float)(scaredUntil - GetSimulationTime());
        }

        public float GetHealth()
        {
            return _currentHealth;
        }

        public float GetMaxHealth()
        {
            return MaxHealthValue;
        }

        public float GetHealthNormalized()
        {
            return _currentHealth / MaxHealthValue;
        }

        public bool IsAlive()
        {
            return _currentHealth > 0f;
        }

        public float GetSimulationTime()
        {
            return PacManGameManager != null ? PacManGameManager.CurrentSimulationTime : Time.fixedTime;
        }

        public int GetStepsSinceMatchStart()
        {
            return PacManGameManager != null ? PacManGameManager.CurrentSimulationStep : 0;
        }

        public int GetStepsRemaining()
        {
            return PacManGameManager != null ? PacManGameManager.RemainingSimulationSteps : 0;
        }

        public PacManObservations GetEnemyObservations()
        {
            return _latestKnownObservation;
        }

        public void SetEnemyObservations(PacManObservations observations)
        {
            observations.Observations ??= System.Array.Empty<PacManObservation>();
            if (observations.AgentServerIndex < 0 ||
                observations.AgentServerIndex == 0 && serverIndex > 0)
            {
                observations.AgentServerIndex = serverIndex;
            }

            _latestKnownObservation = observations;
        }

        public Vector3 GetStartPosition()
        {
            return PacManGameManager.transform.InverseTransformPoint(globalStartPosition);
        }

        public int GetCarriedFoodCount()
        {
            return foodCarried.Count;
        }

        public int GetLastRespawnStep()
        {
            return _lastRespawnStep;
        }

        public List<GameObject> GetCapsuleObjects(bool filterActive = true)
        {
            return PacManGameManager?.capsules?
                .Where(obj => obj != null && (obj.activeSelf || !filterActive))
                .Select(obj => obj.gameObject)
                .ToList() ?? new List<GameObject>();
        }

        public List<GameObject> GetFoodObjects()
        {
            return PacManGameManager?.foodList.Select(obj => obj.gameObject).ToList();
        }

        public Vector3 GetVelocity()
        {
            return GetComponent<Rigidbody>().linearVelocity;
        }

        public float GetTimeRemaining()
        {
            if (PacManGameManager.matchLength == 0) return 0;
            return PacManGameManager.matchLength - PacManGameManager.matchTime;
        }

        public int GetScore()
        {
            return TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue ? PacManGameManager.blueScore : PacManGameManager.redScore;
        }

        public void SetLastRespawnStep(int step)
        {
            _lastRespawnStep = Mathf.Max(0, step);
        }

        public void ResetHealth()
        {
            _currentHealth = MaxHealthValue;
            _lastDamageTime = double.NegativeInfinity;
            UpdateHealthPresentation();
        }

        public void SetSyncedHealth(float health)
        {
            _currentHealth = Mathf.Clamp(health, 0f, MaxHealthValue);
            UpdateHealthPresentation();
        }

        public void ApplyLaserDamage(float damage, double simulationTime)
        {
            if (damage <= 0f || !IsAlive())
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            _lastDamageTime = simulationTime;
            UpdateHealthPresentation();
        }

        public void RegenerateHealth(float deltaTime, double simulationTime)
        {
            if (!IsAlive() || _currentHealth >= MaxHealthValue)
            {
                return;
            }

            if (simulationTime - _lastDamageTime < HealthRegenDelaySeconds)
            {
                return;
            }

            _currentHealth = Mathf.Min(MaxHealthValue, _currentHealth + HealthRegenPerSecond * deltaTime);
            UpdateHealthPresentation();
        }

        public void SetFiringTarget(PacManAgentManager target)
        {
            _currentFiringTarget = target != null && target.IsAlive() ? target : null;
            UpdateLaserBeamVisual();
        }

        public void ClearFiringTarget()
        {
            _currentFiringTarget = null;
            UpdateLaserBeamVisual();
        }

        public void SetScared(bool b, double serverTimeFixedTime)
        {
            isScared = true;
            scaredUntil = serverTimeFixedTime;
        }

        private void EnsureCombatVisualReferences()
        {
            _healthBar ??= GetComponent<HealthBar>();
            if (_healthBar != null)
            {
                EnsureLaserBeamVisual();
            }
        }

        private void UpdateHealthPresentation()
        {
            EnsureCombatVisualReferences();
            _healthBar?.SetHealth(_currentHealth, MaxHealthValue);
        }

        private void EnsureLaserBeamVisual()
        {
            if (_laserBeamTransform != null || _healthBar == null)
            {
                return;
            }

            var beamParent = transform.Find("visuals") ?? transform;
            var existingBeam = beamParent.Find("LaserBeam") ?? transform.Find("LaserBeam");
            if (existingBeam == null)
            {
                var beamObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beamObject.name = "LaserBeam";
                beamObject.layer = gameObject.layer;
                beamObject.transform.SetParent(beamParent, false);
                beamObject.transform.localScale = new Vector3(LaserBeamRadius, LaserBeamMinHalfLength, LaserBeamRadius);

                var collider = beamObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                existingBeam = beamObject.transform;
            }

            _laserBeamTransform = existingBeam;
            _laserBeamRenderer = _laserBeamTransform.GetComponent<Renderer>();
            if (_laserBeamRenderer != null)
            {
                var laserMaterial = _laserBeamRenderer.sharedMaterial != null
                    ? new Material(_laserBeamRenderer.sharedMaterial)
                    : _laserBeamRenderer.material;
                laserMaterial.color = new Color(0.95f, 0.15f, 0.15f, 1f);
                _laserBeamRenderer.material = laserMaterial;
                _laserBeamRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _laserBeamRenderer.receiveShadows = false;
            }

            _laserBeamTransform.gameObject.SetActive(false);
        }

        private void UpdateLaserBeamVisual()
        {
            if (_laserBeamTransform == null)
            {
                return;
            }

            if (_currentFiringTarget == null ||
                !IsAlive() ||
                !_currentFiringTarget.IsAlive() ||
                !_currentFiringTarget.gameObject.activeInHierarchy)
            {
                _laserBeamTransform.gameObject.SetActive(false);
                return;
            }

            var origin = transform.position + Vector3.up * LaserBeamVerticalOffset;
            var targetPosition = _currentFiringTarget.transform.position + Vector3.up * LaserBeamVerticalOffset;
            var direction = targetPosition - origin;
            var distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                _laserBeamTransform.gameObject.SetActive(false);
                return;
            }

            _laserBeamTransform.gameObject.SetActive(true);
            _laserBeamTransform.position = origin + direction * 0.5f;
            _laserBeamTransform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            _laserBeamTransform.localScale = new Vector3(LaserBeamRadius, Mathf.Max(LaserBeamMinHalfLength, distance * 0.5f), LaserBeamRadius);
        }
    }
}

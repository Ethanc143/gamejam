using System.Collections.Generic;
using FocusSim.Cognitive;
using FocusSim.Core;
using FocusSim.Distraction;
using FocusSim.Traffic;
using FocusSim.Vehicle;
using UnityEngine;

namespace FocusSim.Risk
{
    [RequireComponent(typeof(Collider))]
    public sealed class RiskAssessmentSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VehicleController playerVehicle;
        [SerializeField] private DistractionManager distractionManager;
        [SerializeField] private CognitiveLoadManager cognitiveLoadManager;
        [SerializeField] private List<Transform> intersectionPoints = new List<Transform>();

        [Header("Risk Thresholds")]
        [SerializeField] private float speedLimitMps = 20.117f; // 45 mph
        [SerializeField] private float hazardTtcThresholdSeconds = 3f;
        [SerializeField] private float nearMissDistanceMeters = 4.8f;
        [SerializeField] private float nearMissMinimumDistanceMeters = 0.8f;
        [SerializeField] private float imminentCollisionSuppressionTtcSeconds = 0.25f;
        [SerializeField] private float nearMissClosingSpeedThreshold = 0.25f;
        [SerializeField] private float nearMissPerVehicleReplayCooldownSeconds = 1.2f;
        [SerializeField] private float suppressNearMissAfterCollisionSeconds = 0.8f;
        [SerializeField] private float highRiskSpikeThreshold = 0.76f;
        [SerializeField] private float intersectionRiskRadius = 20f;
        [SerializeField] private float collisionGracePeriodSeconds = 0.75f;
        [SerializeField] private float trafficCollisionGracePeriodSeconds = 0.15f;
        [SerializeField] private float minCollisionRelativeSpeed = 0.8f;
        [SerializeField] private float minTrafficCollisionSpeed = 0.2f;

        private readonly Dictionary<int, float> _nearMissCooldownUntil = new Dictionary<int, float>();
        private float _latestTtc = float.PositiveInfinity;
        private bool _hazardActive;
        private float _hazardStartTime;
        private float _speedComplianceAccum;
        private int _speedComplianceSamples;
        private float _followingDistanceSum;
        private float _followingDistanceSqSum;
        private int _followingDistanceSamples;
        private float _lastSpeed;
        private bool _hardBrakeLatched;
        private bool _highRiskLatched;
        private float _lastCollisionTime = -999f;

        public float CurrentRisk { get; private set; }
        public int NearMissCount { get; private set; }
        public int CollisionCount { get; private set; }
        public int HardBrakeCount { get; private set; }
        public float EyesOffRoadSeconds { get; private set; }
        public float LongestDistractionSeconds { get; private set; }
        public float TotalReactionDelaySeconds { get; private set; }
        public int ReactionSamples { get; private set; }

        public float AverageFollowingDistance
        {
            get
            {
                if (_followingDistanceSamples == 0)
                {
                    return 0f;
                }

                return _followingDistanceSum / _followingDistanceSamples;
            }
        }

        public float FollowingDistanceVariance
        {
            get
            {
                if (_followingDistanceSamples == 0)
                {
                    return 0f;
                }

                float mean = _followingDistanceSum / _followingDistanceSamples;
                float meanSq = _followingDistanceSqSum / _followingDistanceSamples;
                return Mathf.Max(0f, meanSq - mean * mean);
            }
        }

        public float SpeedCompliancePercent
        {
            get
            {
                if (_speedComplianceSamples == 0)
                {
                    return 1f;
                }

                return _speedComplianceAccum / _speedComplianceSamples;
            }
        }

        public void Initialize(
            VehicleController vehicleController,
            DistractionManager distraction,
            CognitiveLoadManager cognitive,
            List<Transform> intersections)
        {
            UnsubscribeFromDistractionEvents();
            playerVehicle = vehicleController;
            distractionManager = distraction;
            cognitiveLoadManager = cognitive;
            intersectionPoints.Clear();
            if (intersections != null)
            {
                intersectionPoints.AddRange(intersections);
            }
            SubscribeToDistractionEvents();
        }

        private void OnEnable()
        {
            SubscribeToDistractionEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromDistractionEvents();
        }

        private void SubscribeToDistractionEvents()
        {
            if (distractionManager != null)
            {
                distractionManager.EpisodeEnded += OnEpisodeEnded;
            }
        }

        private void UnsubscribeFromDistractionEvents()
        {
            if (distractionManager != null)
            {
                distractionManager.EpisodeEnded -= OnEpisodeEnded;
            }
        }

        private void Update()
        {
            if (playerVehicle == null)
            {
                return;
            }

            UpdateEyesOffRoad();
            UpdateSpeedCompliance();
            UpdateFollowingDistanceStats(out float nearestDistance);
            UpdateTtcAndHazards(out Vector3 hazardPosition, out bool nearMissDetected);
            UpdateHardBraking();
            UpdateRisk(nearestDistance);

            FocusEventBus.RaiseRiskUpdated(
                new RiskSnapshot(CurrentRisk, _latestTtc, _hazardActive, nearMissDetected, CollisionCount));

            if (CurrentRisk >= highRiskSpikeThreshold && !_highRiskLatched)
            {
                _highRiskLatched = true;
                FocusEventBus.RaiseReplayTriggerRequested(new ReplayTriggerEvent(ReplayTriggerType.HighRiskSpike, hazardPosition, CurrentRisk));
            }
            else if (CurrentRisk < highRiskSpikeThreshold * 0.85f)
            {
                _highRiskLatched = false;
            }
        }

        private void UpdateEyesOffRoad()
        {
            if (!distractionManager || distractionManager.ActiveEpisode == null)
            {
                return;
            }

            var activeEpisode = distractionManager.ActiveEpisode;
            if (activeEpisode.Definition == null)
            {
                return;
            }

            float fraction = activeEpisode.Definition.eyesOffRoadFraction;
            EyesOffRoadSeconds += Time.deltaTime * Mathf.Clamp01(fraction);
        }

        private void UpdateSpeedCompliance()
        {
            float speed = playerVehicle.SpeedMps;
            _speedComplianceAccum += speed <= speedLimitMps ? 1f : 0f;
            _speedComplianceSamples++;
        }

        private void UpdateFollowingDistanceStats(out float nearestForwardDistance)
        {
            nearestForwardDistance = float.MaxValue;
            Vector3 playerPos = playerVehicle.transform.position;
            Vector3 playerForward = playerVehicle.transform.forward;

            foreach (TrafficVehicleAI vehicle in TrafficRegistry.Vehicles)
            {
                if (vehicle == null)
                {
                    continue;
                }

                Vector3 toTraffic = vehicle.transform.position - playerPos;
                float forwardDot = Vector3.Dot(playerForward, toTraffic.normalized);
                if (forwardDot < 0.25f)
                {
                    continue;
                }

                float distance = toTraffic.magnitude;
                if (distance < nearestForwardDistance)
                {
                    nearestForwardDistance = distance;
                }
            }

            if (nearestForwardDistance < float.MaxValue)
            {
                _followingDistanceSum += nearestForwardDistance;
                _followingDistanceSqSum += nearestForwardDistance * nearestForwardDistance;
                _followingDistanceSamples++;
            }
        }

        private void UpdateTtcAndHazards(out Vector3 hazardPosition, out bool nearMissDetected)
        {
            hazardPosition = playerVehicle.transform.position + playerVehicle.transform.forward * 8f;
            nearMissDetected = false;
            _latestTtc = float.PositiveInfinity;
            bool suppressNearMiss = (Time.time - _lastCollisionTime) <= suppressNearMissAfterCollisionSeconds;

            Vector3 playerPos = playerVehicle.transform.position;
            Vector3 playerVel = playerVehicle.Velocity;

            foreach (TrafficVehicleAI traffic in TrafficRegistry.Vehicles)
            {
                if (traffic == null)
                {
                    continue;
                }

                Vector3 relativePosition = traffic.transform.position - playerPos;
                Vector3 relativeVelocity = playerVel - traffic.Velocity;
                float closingSpeed = Vector3.Dot(relativeVelocity, relativePosition.normalized);
                float distance = relativePosition.magnitude;
                float trafficTtc = float.PositiveInfinity;

                if (closingSpeed > 0.05f)
                {
                    trafficTtc = distance / closingSpeed;
                    if (trafficTtc < _latestTtc)
                    {
                        _latestTtc = trafficTtc;
                        hazardPosition = traffic.transform.position;
                    }
                }

                bool nearMissDistanceBand = distance <= nearMissDistanceMeters && distance >= nearMissMinimumDistanceMeters;
                bool likelyImminentCollision = trafficTtc <= imminentCollisionSuppressionTtcSeconds;
                if (!suppressNearMiss && nearMissDistanceBand && !likelyImminentCollision && closingSpeed > nearMissClosingSpeedThreshold)
                {
                    int id = traffic.GetInstanceID();
                    float now = Time.time;
                    if (!_nearMissCooldownUntil.TryGetValue(id, out float cooldownUntil) || now >= cooldownUntil)
                    {
                        NearMissCount++;
                        nearMissDetected = true;
                        FocusEventBus.RaiseReplayTriggerRequested(new ReplayTriggerEvent(ReplayTriggerType.NearMiss, traffic.transform.position, CurrentRisk));
                        _nearMissCooldownUntil[id] = now + nearMissPerVehicleReplayCooldownSeconds;
                    }
                }
                else if (distance > nearMissDistanceMeters * 1.8f)
                {
                    _nearMissCooldownUntil.Remove(traffic.GetInstanceID());
                }
            }

            bool hazardNow = _latestTtc <= hazardTtcThresholdSeconds;
            if (hazardNow && !_hazardActive)
            {
                _hazardActive = true;
                _hazardStartTime = Time.time;
            }
            else if (!hazardNow)
            {
                _hazardActive = false;
            }

            if (_hazardActive && playerVehicle.AppliedInput.Brake > 0.2f)
            {
                TotalReactionDelaySeconds += Mathf.Max(0f, Time.time - _hazardStartTime);
                ReactionSamples++;
                _hazardActive = false;
            }
        }

        private void UpdateHardBraking()
        {
            float speed = playerVehicle.SpeedMps;
            float deceleration = (speed - _lastSpeed) / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastSpeed = speed;

            bool hardBrake = deceleration < -6.8f;
            if (hardBrake && !_hardBrakeLatched)
            {
                HardBrakeCount++;
            }

            _hardBrakeLatched = hardBrake;
        }

        private void UpdateRisk(float nearestDistance)
        {
            float ttcRisk = float.IsInfinity(_latestTtc)
                ? 0f
                : Mathf.Clamp01((hazardTtcThresholdSeconds - _latestTtc) / Mathf.Max(0.1f, hazardTtcThresholdSeconds));
            float speedRisk = Mathf.Clamp01((playerVehicle.SpeedMps - speedLimitMps) / Mathf.Max(1f, speedLimitMps * 0.5f));
            float followingRisk = nearestDistance < float.MaxValue ? Mathf.Clamp01(1f - (nearestDistance / 18f)) : 0f;
            float loadRisk = cognitiveLoadManager != null ? cognitiveLoadManager.CurrentLoadNormalized : 0f;
            float distractionRisk = distractionManager != null && distractionManager.IsAttentionCompromised ? 0.2f : 0f;
            float intersectionRisk = IsNearIntersection() ? 0.08f : 0f;

            float instantaneous = (ttcRisk * 0.4f) + (speedRisk * 0.2f) + (followingRisk * 0.15f) + (loadRisk * 0.25f) + distractionRisk + intersectionRisk;
            instantaneous = Mathf.Clamp01(instantaneous);

            // Move toward the new risk value with light smoothing.
            CurrentRisk = Mathf.MoveTowards(CurrentRisk, instantaneous, Time.deltaTime * 1.8f);
        }

        private bool IsNearIntersection()
        {
            if (intersectionPoints.Count == 0)
            {
                return false;
            }

            Vector3 playerPos = playerVehicle.transform.position;
            for (int i = 0; i < intersectionPoints.Count; i++)
            {
                if (intersectionPoints[i] == null)
                {
                    continue;
                }

                if (Vector3.Distance(playerPos, intersectionPoints[i].position) <= intersectionRiskRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnEpisodeEnded(DistractionManager.RuntimeEpisode runtimeEpisode, EpisodeDecision _, float __)
        {
            if (runtimeEpisode != null)
            {
                LongestDistractionSeconds = Mathf.Max(LongestDistractionSeconds, runtimeEpisode.ElapsedSeconds);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider == null)
            {
                return;
            }

            string otherName = collision.collider.gameObject.name;
            if (otherName.Contains("Ground") || otherName.Contains("Road"))
            {
                return;
            }

            TrafficVehicleAI traffic = collision.collider.GetComponentInParent<TrafficVehicleAI>();
            bool isTrafficCollision = traffic != null;
            float collisionGrace = isTrafficCollision ? trafficCollisionGracePeriodSeconds : collisionGracePeriodSeconds;
            if (Time.timeSinceLevelLoad < collisionGrace)
            {
                return;
            }

            float relativeImpactSpeed = collision.relativeVelocity.magnitude;
            float playerImpactScale = isTrafficCollision ? 0.85f : 0.6f;
            float playerImpactHint = playerVehicle != null ? playerVehicle.SpeedMps * playerImpactScale : 0f;
            float effectiveImpactSpeed = Mathf.Max(relativeImpactSpeed, playerImpactHint);
            float minImpactSpeed = isTrafficCollision ? minTrafficCollisionSpeed : minCollisionRelativeSpeed;
            if (effectiveImpactSpeed < minImpactSpeed)
            {
                return;
            }

            _lastCollisionTime = Time.time;
            if (traffic != null)
            {
                _nearMissCooldownUntil.Remove(traffic.GetInstanceID());
            }

            CollisionCount++;
            Vector3 contact = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            FocusEventBus.RaiseReplayTriggerRequested(new ReplayTriggerEvent(ReplayTriggerType.Collision, contact, CurrentRisk));
        }
    }
}

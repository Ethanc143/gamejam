using System.Collections.Generic;
using FocusSim.Cognitive;
using FocusSim.Core;
using FocusSim.Distraction;
using FocusSim.Risk;
using FocusSim.Telemetry;
using FocusSim.Vehicle;
using UnityEngine;

namespace FocusSim.Telemetry
{
    public sealed class DriveTelemetryRecorder : MonoBehaviour
    {
        [SerializeField] private VehicleController playerVehicle;
        [SerializeField] private CognitiveLoadManager cognitiveLoadManager;
        [SerializeField] private RiskAssessmentSystem riskSystem;
        [SerializeField] private DistractionManager distractionManager;

        private readonly List<FrameTelemetrySample> _frameSamples = new List<FrameTelemetrySample>(6000);
        private readonly List<float> _riskTimeline = new List<float>(1200);
        private float _latestRisk;
        private float _latestTtc;
        private bool _hazardFlag;

        public IReadOnlyList<FrameTelemetrySample> Samples => _frameSamples;

        public void Initialize(
            VehicleController vehicleController,
            CognitiveLoadManager loadManager,
            RiskAssessmentSystem riskAssessment,
            DistractionManager distraction)
        {
            playerVehicle = vehicleController;
            cognitiveLoadManager = loadManager;
            riskSystem = riskAssessment;
            distractionManager = distraction;
        }

        private void OnEnable()
        {
            FocusEventBus.RiskUpdated += OnRiskUpdated;
        }

        private void OnDisable()
        {
            FocusEventBus.RiskUpdated -= OnRiskUpdated;
        }

        private void FixedUpdate()
        {
            if (playerVehicle == null)
            {
                return;
            }

            _frameSamples.Add(new FrameTelemetrySample
            {
                TimeSeconds = Time.timeSinceLevelLoad,
                VehiclePosition = playerVehicle.transform.position,
                SpeedMps = playerVehicle.SpeedMps,
                CognitiveLoadNormalized = cognitiveLoadManager != null ? cognitiveLoadManager.CurrentLoadNormalized : 0f,
                RiskValue = _latestRisk,
                TimeToCollisionSeconds = _latestTtc,
                SteeringInput = playerVehicle.AppliedInput.Steering,
                BrakeInput = playerVehicle.AppliedInput.Brake,
                HazardDetected = _hazardFlag,
                EyesOffRoad = distractionManager != null && distractionManager.IsAttentionCompromised
            });
        }

        private void OnRiskUpdated(RiskSnapshot snapshot)
        {
            _latestRisk = snapshot.RiskValue;
            _latestTtc = snapshot.TimeToCollisionSeconds;
            _hazardFlag = snapshot.HazardDetected;
            _riskTimeline.Add(snapshot.RiskValue);
        }

        public DriveTelemetrySummary BuildSummary(float durationSeconds)
        {
            DriveTelemetrySummary summary = new DriveTelemetrySummary
            {
                DriveDurationSeconds = durationSeconds,
                TotalEyesOffRoadSeconds = riskSystem != null ? riskSystem.EyesOffRoadSeconds : 0f,
                LongestDistractionSeconds = riskSystem != null ? riskSystem.LongestDistractionSeconds : 0f,
                AverageReactionDelaySeconds = riskSystem != null && riskSystem.ReactionSamples > 0
                    ? riskSystem.TotalReactionDelaySeconds / riskSystem.ReactionSamples
                    : 0f,
                HardBrakeCount = riskSystem != null ? riskSystem.HardBrakeCount : 0,
                NearMissCount = riskSystem != null ? riskSystem.NearMissCount : 0,
                CollisionCount = riskSystem != null ? riskSystem.CollisionCount : 0,
                AverageFollowingDistanceMeters = riskSystem != null ? riskSystem.AverageFollowingDistance : 0f,
                SpeedCompliancePercent = riskSystem != null ? riskSystem.SpeedCompliancePercent : 1f
            };

            summary.RiskTimeline.AddRange(_riskTimeline);
            return summary;
        }
    }
}

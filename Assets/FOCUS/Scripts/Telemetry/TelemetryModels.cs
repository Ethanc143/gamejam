using System;
using System.Collections.Generic;
using FocusSim.Core;
using UnityEngine;

namespace FocusSim.Telemetry
{
    [Serializable]
    public struct FrameTelemetrySample
    {
        public float TimeSeconds;
        public Vector3 VehiclePosition;
        public float SpeedMps;
        public float CognitiveLoadNormalized;
        public float RiskValue;
        public float TimeToCollisionSeconds;
        public float SteeringInput;
        public float BrakeInput;
        public bool HazardDetected;
        public bool EyesOffRoad;
    }

    [Serializable]
    public sealed class DriveTelemetrySummary
    {
        public float DriveDurationSeconds;
        public float TotalEyesOffRoadSeconds;
        public float LongestDistractionSeconds;
        public float AverageReactionDelaySeconds;
        public int HardBrakeCount;
        public int NearMissCount;
        public int CollisionCount;
        public float AverageFollowingDistanceMeters;
        public float SpeedCompliancePercent;
        public OutcomeState OutcomeState;
        public readonly List<float> RiskTimeline = new List<float>();
    }
}

using System;
using UnityEngine;

namespace FocusSim.Core
{
    public enum OutcomeState
    {
        SafeDrive = 0,
        NearMissDrive = 1,
        CollisionEvent = 2
    }

    public enum ReplayTriggerType
    {
        NearMiss = 0,
        Collision = 1,
        HighRiskSpike = 2
    }

    public enum EpisodeDecision
    {
        Ignore = 0,
        Engage = 1,
        TimedOut = 2
    }

    public readonly struct DistractionLifecycleEvent
    {
        public readonly string EpisodeId;
        public readonly string EpisodeLabel;
        public readonly bool IsStart;
        public readonly float DurationSeconds;
        public readonly float CognitiveLoadImpact;
        public readonly float RiskMultiplier;
        public readonly EpisodeDecision Decision;

        public DistractionLifecycleEvent(
            string episodeId,
            string episodeLabel,
            bool isStart,
            float durationSeconds,
            float cognitiveLoadImpact,
            float riskMultiplier,
            EpisodeDecision decision = EpisodeDecision.Ignore)
        {
            EpisodeId = episodeId;
            EpisodeLabel = episodeLabel;
            IsStart = isStart;
            DurationSeconds = durationSeconds;
            CognitiveLoadImpact = cognitiveLoadImpact;
            RiskMultiplier = riskMultiplier;
            Decision = decision;
        }
    }

    public readonly struct RiskSnapshot
    {
        public readonly float RiskValue;
        public readonly float TimeToCollisionSeconds;
        public readonly bool HazardDetected;
        public readonly bool NearMissDetected;
        public readonly int CollisionCount;

        public RiskSnapshot(float riskValue, float timeToCollisionSeconds, bool hazardDetected, bool nearMissDetected, int collisionCount)
        {
            RiskValue = riskValue;
            TimeToCollisionSeconds = timeToCollisionSeconds;
            HazardDetected = hazardDetected;
            NearMissDetected = nearMissDetected;
            CollisionCount = collisionCount;
        }
    }

    public readonly struct ReplayTriggerEvent
    {
        public readonly ReplayTriggerType TriggerType;
        public readonly Vector3 HazardPosition;
        public readonly float RiskAtTrigger;

        public ReplayTriggerEvent(ReplayTriggerType triggerType, Vector3 hazardPosition, float riskAtTrigger)
        {
            TriggerType = triggerType;
            HazardPosition = hazardPosition;
            RiskAtTrigger = riskAtTrigger;
        }
    }

    public readonly struct DriveFinishedEvent
    {
        public readonly OutcomeState Outcome;
        public readonly float DurationSeconds;

        public DriveFinishedEvent(OutcomeState outcome, float durationSeconds)
        {
            Outcome = outcome;
            DurationSeconds = durationSeconds;
        }
    }

    public static class FocusEventBus
    {
        public static event Action<DistractionLifecycleEvent> DistractionLifecycleChanged;
        public static event Action<float> CognitiveLoadChanged;
        public static event Action<RiskSnapshot> RiskUpdated;
        public static event Action<ReplayTriggerEvent> ReplayTriggerRequested;
        public static event Action<DriveFinishedEvent> DriveFinished;

        public static void RaiseDistractionLifecycle(DistractionLifecycleEvent evt) => DistractionLifecycleChanged?.Invoke(evt);
        public static void RaiseCognitiveLoadChanged(float normalizedLoad) => CognitiveLoadChanged?.Invoke(normalizedLoad);
        public static void RaiseRiskUpdated(RiskSnapshot snapshot) => RiskUpdated?.Invoke(snapshot);
        public static void RaiseReplayTriggerRequested(ReplayTriggerEvent evt) => ReplayTriggerRequested?.Invoke(evt);
        public static void RaiseDriveFinished(DriveFinishedEvent evt) => DriveFinished?.Invoke(evt);
    }
}

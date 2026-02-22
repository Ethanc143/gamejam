using FocusSim.Telemetry;
using UnityEngine;

namespace FocusSim.Scoring
{
    public readonly struct SafetyScoreBreakdown
    {
        public readonly int Score;
        public readonly float TotalWeight;

        public readonly float HardBrakePenalty01;
        public readonly float NearMissPenalty01;
        public readonly float CollisionPenalty01;
        public readonly float FollowingDistancePenalty01;
        public readonly float SpeedCompliancePenalty01;

        public readonly float HardBrakeWeight;
        public readonly float NearMissWeight;
        public readonly float CollisionWeight;
        public readonly float FollowingDistanceWeight;
        public readonly float SpeedComplianceWeight;

        public SafetyScoreBreakdown(
            int score,
            float totalWeight,
            float hardBrakePenalty01,
            float nearMissPenalty01,
            float collisionPenalty01,
            float followingDistancePenalty01,
            float speedCompliancePenalty01,
            float hardBrakeWeight,
            float nearMissWeight,
            float collisionWeight,
            float followingDistanceWeight,
            float speedComplianceWeight)
        {
            Score = score;
            TotalWeight = totalWeight;
            HardBrakePenalty01 = hardBrakePenalty01;
            NearMissPenalty01 = nearMissPenalty01;
            CollisionPenalty01 = collisionPenalty01;
            FollowingDistancePenalty01 = followingDistancePenalty01;
            SpeedCompliancePenalty01 = speedCompliancePenalty01;
            HardBrakeWeight = hardBrakeWeight;
            NearMissWeight = nearMissWeight;
            CollisionWeight = collisionWeight;
            FollowingDistanceWeight = followingDistanceWeight;
            SpeedComplianceWeight = speedComplianceWeight;
        }

        public static SafetyScoreBreakdown Empty => new SafetyScoreBreakdown(
            0,
            1f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f);
    }

    public sealed class DriveScoringSystem : MonoBehaviour
    {
        [Header("Bad-Range Normalization")]
        [SerializeField, Range(1f, 25f)] private float hardBrakesBadCount = 8f;
        [SerializeField, Range(1f, 15f)] private float nearMissesBadCount = 3f;
        [SerializeField, Range(1f, 8f)] private float collisionsBadCount = 1f;
        [SerializeField, Range(2f, 25f)] private float targetFollowingDistanceMeters = 10f;

        [Header("Score Weights")]
        [SerializeField, Range(0f, 1f)] private float hardBrakeWeight = 0.10f;
        [SerializeField, Range(0f, 1f)] private float nearMissWeight = 0.25f;
        [SerializeField, Range(0f, 1f)] private float collisionWeight = 0.45f;
        [SerializeField, Range(0f, 1f)] private float followingDistanceWeight = 0.10f;
        [SerializeField, Range(0f, 1f)] private float speedComplianceWeight = 0.10f;

        public int ComputeScore(DriveTelemetrySummary summary)
        {
            return ComputeBreakdown(summary).Score;
        }

        public SafetyScoreBreakdown ComputeBreakdown(DriveTelemetrySummary summary)
        {
            if (summary == null)
            {
                return SafetyScoreBreakdown.Empty;
            }

            float hardBrakePenalty = Normalize(summary.HardBrakeCount, hardBrakesBadCount);
            float nearMissPenalty = Normalize(summary.NearMissCount, nearMissesBadCount);
            float collisionPenalty = Normalize(summary.CollisionCount, collisionsBadCount);
            float followingPenalty = GetFollowingDistancePenalty(summary.AverageFollowingDistanceMeters);
            float speedPenalty = Mathf.Clamp01(1f - summary.SpeedCompliancePercent);

            float weightedPenalty = 0f;
            float totalWeight = 0f;
            AddWeightedPenalty(hardBrakePenalty, hardBrakeWeight, ref weightedPenalty, ref totalWeight);
            AddWeightedPenalty(nearMissPenalty, nearMissWeight, ref weightedPenalty, ref totalWeight);
            AddWeightedPenalty(collisionPenalty, collisionWeight, ref weightedPenalty, ref totalWeight);
            AddWeightedPenalty(followingPenalty, followingDistanceWeight, ref weightedPenalty, ref totalWeight);
            AddWeightedPenalty(speedPenalty, speedComplianceWeight, ref weightedPenalty, ref totalWeight);

            if (totalWeight <= 0.0001f)
            {
                totalWeight = 1f;
            }

            float normalizedPenalty = Mathf.Clamp01(weightedPenalty / totalWeight);
            float score = (1f - normalizedPenalty) * 100f;
            int roundedScore = Mathf.Clamp(Mathf.RoundToInt(score), 0, 100);

            return new SafetyScoreBreakdown(
                roundedScore,
                totalWeight,
                hardBrakePenalty,
                nearMissPenalty,
                collisionPenalty,
                followingPenalty,
                speedPenalty,
                hardBrakeWeight,
                nearMissWeight,
                collisionWeight,
                followingDistanceWeight,
                speedComplianceWeight);
        }

        private static float Normalize(float value, float badValue)
        {
            return Mathf.Clamp01(Mathf.Max(0f, value) / Mathf.Max(0.001f, badValue));
        }

        private float GetFollowingDistancePenalty(float averageFollowingDistanceMeters)
        {
            if (averageFollowingDistanceMeters <= 0.001f)
            {
                // No valid following-distance sample should not punish the player.
                return 0f;
            }

            float shortage = Mathf.Max(0f, targetFollowingDistanceMeters - averageFollowingDistanceMeters);
            return Mathf.Clamp01(shortage / Mathf.Max(0.001f, targetFollowingDistanceMeters));
        }

        private static void AddWeightedPenalty(float penalty, float weight, ref float weightedPenalty, ref float totalWeight)
        {
            float clampedWeight = Mathf.Max(0f, weight);
            weightedPenalty += Mathf.Clamp01(penalty) * clampedWeight;
            totalWeight += clampedWeight;
        }
    }
}

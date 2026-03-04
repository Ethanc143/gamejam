using UnityEngine;

namespace FocusSim.Scoring
{
    [CreateAssetMenu(menuName = "FOCUS/Scoring/Scoring Profile", fileName = "ScoringProfile")]
    public sealed class ScoringProfile : ScriptableObject
    {
        [Header("Penalties")]
        [Range(0f, 10f)] public float eyesOffRoadPenaltyPerSecond = 0.45f;
        [Range(0f, 30f)] public float hardBrakePenalty = 3f;
        [Range(0f, 50f)] public float nearMissPenalty = 18f;
        [Range(0f, 100f)] public float collisionPenalty = 45f;
        [Range(0f, 20f)] public float reactionDelayPenaltyPerSecond = 8f;
        [Range(0f, 2f)] public float speedNonCompliancePenaltyFactor = 0.8f;

        [Header("Bonuses")]
        [Range(0f, 30f)] public float safeCompletionBonus = 12f;
        [Range(0f, 20f)] public float lowRiskConsistencyBonus = 6f;
    }
}

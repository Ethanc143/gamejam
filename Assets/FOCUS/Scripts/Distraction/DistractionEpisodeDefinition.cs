using UnityEngine;

namespace FocusSim.Distraction
{
    public enum DistractionEpisodeType
    {
        PhoneMessage = 0,
        NavigationUpdate = 1,
        PassengerInteraction = 2,
        WorkCall = 3,
        FatigueEvent = 4
    }

    public enum EpisodeTriggerMode
    {
        TimeWindow = 0,
        Contextual = 1,
        TimeOrContext = 2
    }

    [CreateAssetMenu(menuName = "FOCUS/Distraction/Episode Definition", fileName = "Episode_")]
    public sealed class DistractionEpisodeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string episodeId = "episode_id";
        public string displayName = "New Episode";
        public DistractionEpisodeType episodeType = DistractionEpisodeType.PhoneMessage;
        [TextArea] public string prompt = "Incoming distraction";

        [Header("Trigger Conditions")]
        public EpisodeTriggerMode triggerMode = EpisodeTriggerMode.TimeWindow;
        [Min(0f)] public float minDriveTimeSeconds = 15f;
        [Min(0f)] public float maxDriveTimeSeconds = 360f;
        [Min(0f)] public float minVehicleSpeedMps = 4f;
        public bool requireIntersectionProximity;

        [Header("Episode Dynamics")]
        [Min(1f)] public float durationSeconds = 5f;
        [Range(1f, 40f)] public float cognitiveLoadValue = 14f;
        [Range(1f, 3f)] public float riskMultiplier = 1.15f;
        [Range(0f, 30f)] public float successLoadBonus = 5f;
        [Range(0f, 30f)] public float failureLoadPenalty = 10f;

        [Header("Telemetry Impact")]
        [Range(0f, 1f)] public float eyesOffRoadFraction = 0.65f;
        [Range(0f, 1f)] public float inputCompetitionStrength = 0.5f;
    }
}

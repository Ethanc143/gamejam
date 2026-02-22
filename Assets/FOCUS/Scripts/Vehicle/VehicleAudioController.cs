using FocusSim.Cognitive;
using FocusSim.Risk;
using UnityEngine;

namespace FocusSim.Vehicle
{
    [RequireComponent(typeof(AudioLowPassFilter))]
    public sealed class VehicleAudioController : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private CognitiveLoadManager cognitiveLoadManager;
        [SerializeField] private RiskAssessmentSystem riskSystem;
        [SerializeField] private AudioSource engineSource;
        [SerializeField] private AudioSource tireSource;
        [SerializeField] private AudioSource ambienceSource;

        private AudioLowPassFilter _lowPass;

        private void Awake()
        {
            _lowPass = GetComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 22000f;
        }

        private void Update()
        {
            if (vehicleController == null)
            {
                return;
            }

            float speedRatio = Mathf.Clamp01(vehicleController.SpeedMps / Mathf.Max(1f, vehicleController.TopSpeedMps));
            if (engineSource != null)
            {
                engineSource.pitch = Mathf.Lerp(0.8f, 1.8f, speedRatio);
                engineSource.volume = Mathf.Lerp(0.15f, 0.72f, speedRatio);
            }

            if (tireSource != null)
            {
                tireSource.pitch = Mathf.Lerp(0.9f, 1.35f, speedRatio);
                tireSource.volume = Mathf.Lerp(0.05f, 0.5f, speedRatio);
            }

            if (ambienceSource != null)
            {
                ambienceSource.volume = Mathf.Lerp(0.22f, 0.42f, speedRatio);
            }

            float cognitiveLoad = cognitiveLoadManager != null ? cognitiveLoadManager.CurrentLoadNormalized : 0f;
            float risk = riskSystem != null ? riskSystem.CurrentRisk : 0f;
            float muffling = Mathf.Clamp01((cognitiveLoad * 0.75f) + (risk * 0.25f));
            _lowPass.cutoffFrequency = Mathf.Lerp(22000f, 1800f, muffling);
        }

        public void Initialize(
            VehicleController controller,
            CognitiveLoadManager loadManager,
            RiskAssessmentSystem assessmentSystem,
            AudioSource engineAudio = null,
            AudioSource tireAudio = null,
            AudioSource ambienceAudio = null)
        {
            vehicleController = controller;
            cognitiveLoadManager = loadManager;
            riskSystem = assessmentSystem;
            engineSource = engineAudio;
            tireSource = tireAudio;
            ambienceSource = ambienceAudio;
        }
    }
}

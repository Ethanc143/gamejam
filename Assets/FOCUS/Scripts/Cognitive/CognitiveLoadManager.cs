using System.Collections.Generic;
using FocusSim.Accessibility;
using FocusSim.Core;
using UnityEngine;

namespace FocusSim.Cognitive
{
    public sealed class CognitiveLoadManager : MonoBehaviour
    {
        [Header("Load Dynamics")]
        [SerializeField, Range(0f, 100f)] private float maxImpairmentLoad = 100f;
        [SerializeField, Range(1f, 10f)] private float episodeImpactScale = 3f;
        [SerializeField, Range(1f, 80f)] private float loadRisePerSecond = 32f;
        [SerializeField, Range(0f, 25f)] private float loadDecayPerSecond = 7f;
        [SerializeField, Range(0.01f, 1f)] private float blendLerpSpeed = 0.12f;

        [Header("Impairment Curves (x = normalized load)")]
        [SerializeField] private AnimationCurve steeringPrecisionCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.55f);
        [SerializeField] private AnimationCurve inputDelayCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.35f);
        [SerializeField] private AnimationCurve brakeDelayCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.45f);
        [SerializeField] private AnimationCurve peripheralSuppressionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.75f);
        [SerializeField] private AccessibilitySettings accessibilitySettings;

        private readonly Dictionary<string, float> _activeEpisodeImpacts = new Dictionary<string, float>();
        private float _rawLoad;
        private float _smoothedLoad;

        public float CurrentLoadNormalized => Mathf.Clamp01(_smoothedLoad / maxImpairmentLoad);
        public float SteeringPrecisionMultiplier => steeringPrecisionCurve.Evaluate(CurrentLoadNormalized);
        public float InputDelaySeconds => inputDelayCurve.Evaluate(CurrentLoadNormalized) + GetAssistDelayPadding();
        public float BrakeReactionDelaySeconds => brakeDelayCurve.Evaluate(CurrentLoadNormalized) + GetAssistDelayPadding();
        public float PeripheralSuppression => peripheralSuppressionCurve.Evaluate(CurrentLoadNormalized);

        public void Initialize(AccessibilitySettings settings)
        {
            accessibilitySettings = settings;
        }

        private void OnEnable()
        {
            FocusEventBus.DistractionLifecycleChanged += OnDistractionLifecycleChanged;
        }

        private void OnDisable()
        {
            FocusEventBus.DistractionLifecycleChanged -= OnDistractionLifecycleChanged;
        }

        private void Update()
        {
            float activeContribution = 0f;
            foreach (float impact in _activeEpisodeImpacts.Values)
            {
                activeContribution += impact;
            }

            // Simple model: active distractions define a target load; load rises toward it, then decays.
            float targetLoad = Mathf.Clamp(activeContribution * episodeImpactScale, 0f, maxImpairmentLoad);
            float moveRate = targetLoad > _rawLoad ? loadRisePerSecond : loadDecayPerSecond;
            _rawLoad = Mathf.MoveTowards(_rawLoad, targetLoad, moveRate * Time.deltaTime);

            _smoothedLoad = Mathf.Lerp(_smoothedLoad, _rawLoad, blendLerpSpeed);

            FocusEventBus.RaiseCognitiveLoadChanged(CurrentLoadNormalized);
        }

        public void AddImpulse(float rawLoadAmount)
        {
            _rawLoad = Mathf.Clamp(_rawLoad + rawLoadAmount, 0f, maxImpairmentLoad);
        }

        private void OnDistractionLifecycleChanged(DistractionLifecycleEvent evt)
        {
            float intensityScale = accessibilitySettings != null ? accessibilitySettings.distractionIntensityScale : 1f;
            float scaledImpact = evt.CognitiveLoadImpact * intensityScale;

            if (evt.IsStart)
            {
                _activeEpisodeImpacts[evt.EpisodeId] = scaledImpact;
                AddImpulse(scaledImpact * 0.35f);
                return;
            }

            _activeEpisodeImpacts.Remove(evt.EpisodeId);

            if (evt.Decision == EpisodeDecision.Engage)
            {
                AddImpulse(scaledImpact * 0.75f);
            }
            else if (evt.Decision == EpisodeDecision.TimedOut)
            {
                AddImpulse(scaledImpact * 1.1f);
            }
            else
            {
                AddImpulse(scaledImpact * 0.25f);
            }
        }

        private float GetAssistDelayPadding()
        {
            // Assist mode slightly offsets the effective delay under high load.
            if (accessibilitySettings == null)
            {
                return 0f;
            }

            float assist = accessibilitySettings.reactionBufferAssistSeconds;
            return Mathf.Lerp(0f, -assist, CurrentLoadNormalized);
        }
    }
}

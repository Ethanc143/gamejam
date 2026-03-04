using FocusSim.Core;
using UnityEngine;

namespace FocusSim.Traffic
{
    public enum TrafficLightPhase
    {
        Green = 0,
        Yellow = 1,
        Red = 2
    }

    public sealed class TrafficLightController : MonoBehaviour
    {
        [SerializeField] private float greenDuration = 14f;
        [SerializeField] private float yellowDuration = 3f;
        [SerializeField] private float redDuration = 13f;
        [SerializeField] private Renderer visualRenderer;

        private float _timer;
        private TrafficLightPhase _phase;

        public TrafficLightPhase CurrentPhase => _phase;
        public bool IsStopRequired => _phase == TrafficLightPhase.Red || _phase == TrafficLightPhase.Yellow;

        public void Initialize(Renderer renderer)
        {
            visualRenderer = renderer;
            _phase = TrafficLightPhase.Green;
            _timer = 0f;
            ApplyVisual();
        }

        private void Start()
        {
            _phase = TrafficLightPhase.Green;
            ApplyVisual();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float duration = GetDuration(_phase);
            if (_timer < duration)
            {
                return;
            }

            _timer = 0f;
            _phase = _phase switch
            {
                TrafficLightPhase.Green => TrafficLightPhase.Yellow,
                TrafficLightPhase.Yellow => TrafficLightPhase.Red,
                _ => TrafficLightPhase.Green
            };
            ApplyVisual();
        }

        private float GetDuration(TrafficLightPhase phase)
        {
            return phase switch
            {
                TrafficLightPhase.Green => greenDuration,
                TrafficLightPhase.Yellow => yellowDuration,
                _ => redDuration
            };
        }

        private void ApplyVisual()
        {
            if (visualRenderer == null)
            {
                return;
            }

            Color color = _phase switch
            {
                TrafficLightPhase.Green => new Color(0.2f, 0.9f, 0.2f),
                TrafficLightPhase.Yellow => new Color(1f, 0.83f, 0.2f),
                _ => new Color(1f, 0.25f, 0.25f)
            };
            RuntimeMaterialHelper.ApplyColor(visualRenderer, color);
        }
    }
}

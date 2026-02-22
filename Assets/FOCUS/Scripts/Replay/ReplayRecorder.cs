using System.Collections.Generic;
using FocusSim.Cognitive;
using FocusSim.Vehicle;
using UnityEngine;

namespace FocusSim.Replay
{
    public sealed class ReplayRecorder : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private CognitiveLoadManager cognitiveLoadManager;
        [SerializeField, Range(10f, 60f)] private float sampleRate = 30f;
        [SerializeField, Range(5f, 30f)] private float bufferDurationSeconds = 16f;

        private readonly List<ReplayFrame> _frames = new List<ReplayFrame>(1000);
        private float _sampleTimer;
        private int _maxFrames;

        public readonly struct ReplayFrame
        {
            public readonly float Time;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float SpeedMps;
            public readonly float Steering;
            public readonly float Brake;
            public readonly float CognitiveLoad;

            public ReplayFrame(float time, Vector3 position, Quaternion rotation, float speedMps, float steering, float brake, float cognitiveLoad)
            {
                Time = time;
                Position = position;
                Rotation = rotation;
                SpeedMps = speedMps;
                Steering = steering;
                Brake = brake;
                CognitiveLoad = cognitiveLoad;
            }
        }

        private void Awake()
        {
            _maxFrames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * bufferDurationSeconds));
        }

        public void Initialize(VehicleController controller, CognitiveLoadManager loadManager)
        {
            vehicleController = controller;
            cognitiveLoadManager = loadManager;
        }

        private void LateUpdate()
        {
            if (vehicleController == null)
            {
                return;
            }

            _sampleTimer += Time.deltaTime;
            float interval = 1f / sampleRate;
            if (_sampleTimer < interval)
            {
                return;
            }

            _sampleTimer = 0f;
            CaptureFrame();
        }

        private void CaptureFrame()
        {
            _frames.Add(
                new ReplayFrame(
                    Time.timeSinceLevelLoad,
                    vehicleController.transform.position,
                    vehicleController.transform.rotation,
                    vehicleController.SpeedMps,
                    vehicleController.AppliedInput.Steering,
                    vehicleController.AppliedInput.Brake,
                    cognitiveLoadManager != null ? cognitiveLoadManager.CurrentLoadNormalized : 0f));

            if (_frames.Count > _maxFrames)
            {
                _frames.RemoveAt(0);
            }
        }

        public List<ReplayFrame> GetLastSeconds(float seconds)
        {
            List<ReplayFrame> segment = new List<ReplayFrame>();
            if (_frames.Count == 0)
            {
                return segment;
            }

            float latestTime = _frames[_frames.Count - 1].Time;
            float minTime = latestTime - Mathf.Max(0.5f, seconds);
            for (int i = 0; i < _frames.Count; i++)
            {
                if (_frames[i].Time >= minTime)
                {
                    segment.Add(_frames[i]);
                }
            }

            return segment;
        }
    }
}

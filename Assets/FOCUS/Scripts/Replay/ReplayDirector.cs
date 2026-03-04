using System.Collections;
using System.Collections.Generic;
using FocusSim.Core;
using FocusSim.UI;
using FocusSim.Vehicle;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace FocusSim.Replay
{
    public sealed class ReplayDirector : MonoBehaviour
    {
        [SerializeField] private ReplayRecorder replayRecorder;
        [SerializeField] private VehicleController playerVehicle;
        [SerializeField] private FocusHUD hud;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float lookbackSeconds = 4f;
        [SerializeField] private float postReplayPauseSeconds = 0.75f;
        [SerializeField] private float replayCooldownSeconds = 2f;
        [SerializeField] private float nearMissReplayCooldownSeconds = 0.7f;
        [SerializeField] private float replayStartDelaySeconds = 8f;
        [SerializeField] private float minVehicleSpeedForReplayMps = 2f;
        [SerializeField] private float cameraHeightOffset = 2.3f;
        [SerializeField] private float cameraDistanceOffset = 5.7f;

        private Camera _replayCamera;
        private PlayableDirector _playableDirector;
        private TimelineAsset _timelineAsset;
        private bool _isReplaying;
        private Rigidbody _vehicleRb;
        private VehicleInputSource _inputSource;
        private bool _preReplayVehicleEnabled;
        private bool _preReplayInputEnabled;
        private bool _driveFinished;
        private float _nextReplayAllowedUnscaledTime;
        private float _preReplayTimeScale = 1f;
        private RigidbodyInterpolation _preReplayVehicleInterpolation = RigidbodyInterpolation.None;
        private Vector3 _preReplayPlayerPosition;
        private Quaternion _preReplayPlayerRotation;
        private readonly List<RigidbodySnapshot> _worldSnapshots = new List<RigidbodySnapshot>();

        private readonly struct RigidbodySnapshot
        {
            public readonly Rigidbody Body;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 LinearVelocity;
            public readonly Vector3 AngularVelocity;
            public readonly bool IsKinematic;
            public readonly bool UseGravity;
            public readonly bool WasSleeping;

            public RigidbodySnapshot(
                Rigidbody body,
                Vector3 position,
                Quaternion rotation,
                Vector3 linearVelocity,
                Vector3 angularVelocity,
                bool isKinematic,
                bool useGravity,
                bool wasSleeping)
            {
                Body = body;
                Position = position;
                Rotation = rotation;
                LinearVelocity = linearVelocity;
                AngularVelocity = angularVelocity;
                IsKinematic = isKinematic;
                UseGravity = useGravity;
                WasSleeping = wasSleeping;
            }
        }

        private void Awake()
        {
            _playableDirector = gameObject.AddComponent<PlayableDirector>();
            _playableDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            _vehicleRb = playerVehicle != null ? playerVehicle.GetComponent<Rigidbody>() : null;
            _inputSource = playerVehicle != null ? playerVehicle.GetComponent<VehicleInputSource>() : null;
        }

        public void Configure(ReplayRecorder recorder, VehicleController vehicle, FocusHUD focusHud, Camera mainCamera)
        {
            replayRecorder = recorder;
            playerVehicle = vehicle;
            hud = focusHud;
            gameplayCamera = mainCamera;
            _vehicleRb = playerVehicle != null ? playerVehicle.GetComponent<Rigidbody>() : null;
            _inputSource = playerVehicle != null ? playerVehicle.GetComponent<VehicleInputSource>() : null;
        }

        private void OnEnable()
        {
            FocusEventBus.ReplayTriggerRequested += OnReplayRequested;
            FocusEventBus.DriveFinished += OnDriveFinished;
        }

        private void OnDisable()
        {
            FocusEventBus.ReplayTriggerRequested -= OnReplayRequested;
            FocusEventBus.DriveFinished -= OnDriveFinished;
            RestoreStateAfterReplayInterrupt();
        }

        private void OnReplayRequested(ReplayTriggerEvent evt)
        {
            if (_isReplaying || _driveFinished || replayRecorder == null || playerVehicle == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextReplayAllowedUnscaledTime)
            {
                return;
            }

            if (Time.timeSinceLevelLoad < replayStartDelaySeconds)
            {
                return;
            }

            if (evt.TriggerType != ReplayTriggerType.Collision && playerVehicle.SpeedMps < minVehicleSpeedForReplayMps)
            {
                return;
            }

            List<ReplayRecorder.ReplayFrame> frames = replayRecorder.GetLastSeconds(lookbackSeconds);
            if (frames.Count < 2)
            {
                return;
            }

            StartCoroutine(PlayReplaySequence(evt, frames));
        }

        private IEnumerator PlayReplaySequence(ReplayTriggerEvent triggerEvent, List<ReplayRecorder.ReplayFrame> frames)
        {
            _isReplaying = true;
            EnsureReplayCamera();
            BuildTimelineForReplay();
            _preReplayPlayerPosition = playerVehicle.transform.position;
            _preReplayPlayerRotation = playerVehicle.transform.rotation;
            CaptureWorldSnapshot();
            _preReplayTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = false;
            }

            _replayCamera.enabled = true;
            _playableDirector.Play();

            _preReplayVehicleEnabled = playerVehicle.enabled;
            _preReplayInputEnabled = _inputSource != null && _inputSource.enabled;
            playerVehicle.enabled = false;
            if (_inputSource != null)
            {
                _inputSource.enabled = false;
            }

            if (_vehicleRb != null)
            {
                _preReplayVehicleInterpolation = _vehicleRb.interpolation;
                _vehicleRb.interpolation = RigidbodyInterpolation.None;
                _vehicleRb.isKinematic = true;
            }

            for (int i = 0; i < frames.Count; i++)
            {
                ReplayRecorder.ReplayFrame frame = frames[i];
                if (_vehicleRb != null)
                {
                    _vehicleRb.position = frame.Position;
                    _vehicleRb.rotation = frame.Rotation;
                }

                playerVehicle.transform.SetPositionAndRotation(frame.Position, frame.Rotation);
                Physics.SyncTransforms();
                PositionReplayCamera(frame.Position, frame.Rotation, triggerEvent.HazardPosition);
                hud?.SetReplayTelemetry(frame.SpeedMps, frame.CognitiveLoad, triggerEvent.TriggerType);
                yield return new WaitForSecondsRealtime(1f / 24f);
            }

            RestoreWorldSnapshot();
            RestorePlayerToPreReplayTransform();
            if (_vehicleRb != null)
            {
                _vehicleRb.interpolation = _preReplayVehicleInterpolation;
            }

            if (!_driveFinished)
            {
                playerVehicle.enabled = true;
                if (_inputSource != null)
                {
                    _inputSource.enabled = true;
                }
            }
            else
            {
                playerVehicle.enabled = _preReplayVehicleEnabled;
                if (_inputSource != null)
                {
                    _inputSource.enabled = _preReplayInputEnabled;
                }
            }

            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = true;
            }

            _replayCamera.enabled = false;
            hud?.ClearReplayTelemetry();
            if (postReplayPauseSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(postReplayPauseSeconds);
            }

            Time.timeScale = _preReplayTimeScale;
            _isReplaying = false;
            _nextReplayAllowedUnscaledTime = Time.unscaledTime + GetReplayCooldownForTrigger(triggerEvent.TriggerType);
        }

        private void EnsureReplayCamera()
        {
            if (_replayCamera != null)
            {
                return;
            }

            GameObject cameraGo = new GameObject("ReplayCamera");
            _replayCamera = cameraGo.AddComponent<Camera>();
            _replayCamera.fieldOfView = 55f;
            _replayCamera.enabled = false;
        }

        private void PositionReplayCamera(Vector3 vehiclePos, Quaternion vehicleRot, Vector3 hazardPos)
        {
            Vector3 back = -(vehicleRot * Vector3.forward) * cameraDistanceOffset;
            Vector3 elevated = Vector3.up * cameraHeightOffset;
            Vector3 hazardBias = (hazardPos - vehiclePos).normalized * 1.2f;

            _replayCamera.transform.position = vehiclePos + back + elevated + hazardBias;
            _replayCamera.transform.LookAt(vehiclePos + Vector3.up * 1f);
        }

        private void BuildTimelineForReplay()
        {
            _timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
            TrackAsset markerTrack = _timelineAsset.CreateTrack<MarkerTrack>(null, "ReplayMarkerTrack");
            markerTrack.CreateMarker<SignalEmitter>(0.05f);

            _playableDirector.playableAsset = _timelineAsset;
            _playableDirector.initialTime = 0f;
        }

        private void OnDriveFinished(DriveFinishedEvent _)
        {
            _driveFinished = true;
        }

        private void RestoreStateAfterReplayInterrupt()
        {
            if (!_isReplaying)
            {
                return;
            }

            RestoreWorldSnapshot();
            RestorePlayerToPreReplayTransform();
            Time.timeScale = _preReplayTimeScale;
            if (_vehicleRb != null)
            {
                _vehicleRb.interpolation = _preReplayVehicleInterpolation;
            }

            if (playerVehicle != null)
            {
                playerVehicle.enabled = _preReplayVehicleEnabled;
            }

            if (_inputSource != null)
            {
                _inputSource.enabled = _preReplayInputEnabled;
            }

            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = true;
            }

            if (_replayCamera != null)
            {
                _replayCamera.enabled = false;
            }

            hud?.ClearReplayTelemetry();
            _isReplaying = false;
        }

        private void CaptureWorldSnapshot()
        {
            _worldSnapshots.Clear();
#if UNITY_2022_2_OR_NEWER
            Rigidbody[] bodies = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            Rigidbody[] bodies = Object.FindObjectsOfType<Rigidbody>();
#endif
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null)
                {
                    continue;
                }

                _worldSnapshots.Add(new RigidbodySnapshot(
                    body,
                    body.position,
                    body.rotation,
                    body.linearVelocity,
                    body.angularVelocity,
                    body.isKinematic,
                    body.useGravity,
                    body.IsSleeping()));
            }
        }

        private void RestoreWorldSnapshot()
        {
            if (_worldSnapshots.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _worldSnapshots.Count; i++)
            {
                RigidbodySnapshot snapshot = _worldSnapshots[i];
                if (snapshot.Body == null)
                {
                    continue;
                }

                snapshot.Body.position = snapshot.Position;
                snapshot.Body.rotation = snapshot.Rotation;
                snapshot.Body.transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
                snapshot.Body.useGravity = snapshot.UseGravity;
                snapshot.Body.isKinematic = snapshot.IsKinematic;
                if (!snapshot.IsKinematic)
                {
                    snapshot.Body.linearVelocity = snapshot.LinearVelocity;
                    snapshot.Body.angularVelocity = snapshot.AngularVelocity;
                }

                if (snapshot.WasSleeping)
                {
                    snapshot.Body.Sleep();
                }
                else
                {
                    snapshot.Body.WakeUp();
                }
            }

            _worldSnapshots.Clear();
        }

        private void RestorePlayerToPreReplayTransform()
        {
            if (playerVehicle == null)
            {
                return;
            }

            playerVehicle.transform.SetPositionAndRotation(_preReplayPlayerPosition, _preReplayPlayerRotation);
            if (_vehicleRb != null)
            {
                _vehicleRb.position = _preReplayPlayerPosition;
                _vehicleRb.rotation = _preReplayPlayerRotation;
            }

            Physics.SyncTransforms();
        }

        private float GetReplayCooldownForTrigger(ReplayTriggerType triggerType)
        {
            if (triggerType == ReplayTriggerType.NearMiss)
            {
                return Mathf.Max(0f, nearMissReplayCooldownSeconds);
            }

            return Mathf.Max(0f, replayCooldownSeconds);
        }
    }
}

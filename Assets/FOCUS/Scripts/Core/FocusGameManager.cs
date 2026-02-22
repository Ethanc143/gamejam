using FocusSim.Telemetry;
using FocusSim.UI;
using FocusSim.Vehicle;
using FocusSim.Risk;
using FocusSim.Scoring;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FocusSim.Core
{
    public sealed class FocusGameManager : MonoBehaviour
    {
        [SerializeField] private float sessionDurationSeconds = 360f;
        [SerializeField] private bool endDriveOnCollision = true;
        [SerializeField] private VehicleController playerVehicle;
        [SerializeField] private RiskAssessmentSystem riskAssessment;
        [SerializeField] private DriveTelemetryRecorder telemetryRecorder;
        [SerializeField] private DriveScoringSystem driveScoringSystem;
        [SerializeField] private PostDriveAnalyticsUI analyticsUi;

        private float _elapsed;
        private bool _ended;

        public void Initialize(
            VehicleController vehicleController,
            RiskAssessmentSystem riskSystem,
            DriveTelemetryRecorder recorder,
            DriveScoringSystem scoringSystem,
            PostDriveAnalyticsUI analyticsScreen)
        {
            playerVehicle = vehicleController;
            riskAssessment = riskSystem;
            telemetryRecorder = recorder;
            driveScoringSystem = scoringSystem;
            analyticsUi = analyticsScreen;
        }

        private void Update()
        {
            if (_ended)
            {
                bool restartPressed = false;
#if ENABLE_INPUT_SYSTEM
                restartPressed |= Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                restartPressed |= Input.GetKeyDown(KeyCode.R);
#endif

                if (restartPressed)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                }
                return;
            }

            _elapsed += Time.deltaTime;

            bool collisionEnded = endDriveOnCollision && riskAssessment != null && riskAssessment.CollisionCount > 0;
            bool timeEnded = _elapsed >= sessionDurationSeconds;
            if (collisionEnded || timeEnded)
            {
                EndDrive();
            }
        }

        private void EndDrive()
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            DriveTelemetrySummary summary = telemetryRecorder.BuildSummary(_elapsed);
            summary.OutcomeState = ResolveOutcome(summary);
            SafetyScoreBreakdown safetyBreakdown = driveScoringSystem != null
                ? driveScoringSystem.ComputeBreakdown(summary)
                : SafetyScoreBreakdown.Empty;

            FocusEventBus.RaiseDriveFinished(new DriveFinishedEvent(summary.OutcomeState, _elapsed));
            analyticsUi.Show(summary, safetyBreakdown);

            if (playerVehicle != null)
            {
                playerVehicle.enabled = false;
            }

            VehicleInputSource input = playerVehicle != null ? playerVehicle.GetComponent<VehicleInputSource>() : null;
            if (input != null)
            {
                input.enabled = false;
            }
        }

        private static OutcomeState ResolveOutcome(DriveTelemetrySummary summary)
        {
            if (summary.CollisionCount > 0)
            {
                return OutcomeState.CollisionEvent;
            }

            if (summary.NearMissCount > 0)
            {
                return OutcomeState.NearMissDrive;
            }

            return OutcomeState.SafeDrive;
        }
    }
}

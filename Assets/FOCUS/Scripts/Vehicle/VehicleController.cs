using System.Collections.Generic;
using FocusSim.Cognitive;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FocusSim.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleInputSource))]
    public sealed class VehicleController : MonoBehaviour
    {
        [Header("Wheel Setup")]
        [SerializeField] private WheelCollider frontLeftWheel;
        [SerializeField] private WheelCollider frontRightWheel;
        [SerializeField] private WheelCollider rearLeftWheel;
        [SerializeField] private WheelCollider rearRightWheel;
        [SerializeField] private Transform frontLeftVisual;
        [SerializeField] private Transform frontRightVisual;
        [SerializeField] private Transform rearLeftVisual;
        [SerializeField] private Transform rearRightVisual;

        [Header("Powertrain")]
        [SerializeField] private float maxMotorTorque = 1200f;
        [SerializeField] private float maxBrakeTorque = 2600f;
        [SerializeField] private float maxSteerAngle = 30f;
        [SerializeField] private float topSpeedMps = 44.704f; // 100 mph
        [SerializeField] private float handbrakeTorque = 4800f;

        [Header("Handling")]
        [SerializeField] private float steeringResponsiveness = 6.5f;
        [SerializeField] private float tractionStiffnessDry = 1.35f;
        [SerializeField] private float tractionStiffnessRain = 0.95f;
        [SerializeField] private float baselineDecelerationMps2 = 7.2f;
        [SerializeField] private bool disableWheelColliderPhysics = true;
        [SerializeField] private bool rainModeEnabled;
        [SerializeField] private float steeringNoiseAtMaxLoad = 0.2f;
        [SerializeField] private bool enableWASDFallbackDrive = true;
        [SerializeField] private float fallbackAcceleration = 6f;
        [SerializeField] private float fallbackTurnRate = 54f;

        [Header("Assistive Arcade Drive")]
        [SerializeField] private bool forceSmoothArcadeDrive = true;
        [SerializeField] private bool forceIdealizedTransformDrive = true;
        [SerializeField] private float arcadeForwardAcceleration = 9f;
        [SerializeField] private float arcadeBrakeDeceleration = 26f;
        [SerializeField] private float arcadeReverseAcceleration = 8f;
        [SerializeField] private float arcadeCoastDeceleration = 7f;
        [SerializeField] private float arcadeReverseSpeedMps = 8f;
        [SerializeField] private float arcadeTurnRate = 78f;
        [SerializeField] private float arcadeLateralGrip = 9.5f;

        [Header("References")]
        [SerializeField] private CognitiveLoadManager cognitiveLoadManager;

        private readonly Queue<DelayedInputSample> _delayedInputQueue = new Queue<DelayedInputSample>();
        private VehicleInputSource _inputSource;
        private Rigidbody _rigidbody;
        private VehicleInputState _appliedInput;
        private float _smoothedSteer;
        private float _nextBrakeActivationTime;
        private float _previousBrakeCommand;
        private float _idealizedForwardSpeed;
        private Vector3 _reportedVelocity;
        private float _baseDriveY;

        public float SpeedMps => forceIdealizedTransformDrive ? _reportedVelocity.magnitude : (_rigidbody != null ? _rigidbody.linearVelocity.magnitude : 0f);
        public float TopSpeedMps => topSpeedMps;
        public Vector3 Velocity => forceIdealizedTransformDrive ? _reportedVelocity : (_rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero);
        public bool UsesIdealizedTransformDrive => forceIdealizedTransformDrive;
        public VehicleInputState AppliedInput => _appliedInput;
        public float EstimatedBrakingDistanceMeters
        {
            get
            {
                float speed = SpeedMps;
                float reactionDelay = cognitiveLoadManager != null ? cognitiveLoadManager.BrakeReactionDelaySeconds : 0f;
                float reactionDistance = speed * reactionDelay;
                float brakingDistance = (speed * speed) / Mathf.Max(1f, 2f * baselineDecelerationMps2);
                return reactionDistance + brakingDistance;
            }
        }

        private readonly struct DelayedInputSample
        {
            public readonly float ApplyAt;
            public readonly VehicleInputState Input;

            public DelayedInputSample(float applyAt, VehicleInputState input)
            {
                ApplyAt = applyAt;
                Input = input;
            }
        }

        private void OnValidate()
        {
            EnsureNonZeroAcceleration();
        }

        private void Awake()
        {
            EnsureNonZeroAcceleration();
            _inputSource = GetComponent<VehicleInputSource>();
            _rigidbody = GetComponent<Rigidbody>();
            _baseDriveY = transform.position.y;

            if (_rigidbody != null)
            {
                _rigidbody.mass = 1450f;
                _rigidbody.centerOfMass = new Vector3(0f, -0.45f, 0f);
                if (forceIdealizedTransformDrive)
                {
                    _rigidbody.isKinematic = true;
                    _rigidbody.useGravity = false;
                }
            }

            DisableWheelColliderPhysicsIfNeeded();
        }

        private void FixedUpdate()
        {
            if (_inputSource == null || _rigidbody == null)
            {
                return;
            }

            if (forceIdealizedTransformDrive)
            {
                // Idealized drive should always use live keyboard/gamepad input (no delayed queue),
                // otherwise stale input can latch and make steering appear unresponsive.
                VehicleInputState input = ReadImmediateInput();
                _appliedInput = input;
                ApplyIdealizedTransformDrive(input);
                SyncWheelVisuals();
                return;
            }

            if (disableWheelColliderPhysics)
            {
                // In idealized mode, read live input directly so movement is always responsive.
                _appliedInput = ReadImmediateInput();
            }
            else
            {
                QueueInputWithDelay(_inputSource.CurrentInput);
                PullAppliedInput();
            }

            ApplyVehicleForces();
            SyncWheelVisuals();
        }

        public void SetRainMode(bool enabled)
        {
            rainModeEnabled = enabled;
        }

        public void Configure(
            WheelCollider frontLeft,
            WheelCollider frontRight,
            WheelCollider rearLeft,
            WheelCollider rearRight,
            Transform frontLeftMesh,
            Transform frontRightMesh,
            Transform rearLeftMesh,
            Transform rearRightMesh,
            CognitiveLoadManager loadManager)
        {
            frontLeftWheel = frontLeft;
            frontRightWheel = frontRight;
            rearLeftWheel = rearLeft;
            rearRightWheel = rearRight;
            frontLeftVisual = frontLeftMesh;
            frontRightVisual = frontRightMesh;
            rearLeftVisual = rearLeftMesh;
            rearRightVisual = rearRightMesh;
            cognitiveLoadManager = loadManager;
            DisableWheelColliderPhysicsIfNeeded();
        }

        private void QueueInputWithDelay(VehicleInputState latestInput)
        {
            float delay = cognitiveLoadManager != null ? Mathf.Max(0f, cognitiveLoadManager.InputDelaySeconds) : 0f;
            _delayedInputQueue.Enqueue(new DelayedInputSample(Time.time + delay, latestInput));
        }

        private void PullAppliedInput()
        {
            if (_delayedInputQueue.Count == 0)
            {
                return;
            }

            while (_delayedInputQueue.Count > 0 && _delayedInputQueue.Peek().ApplyAt <= Time.time)
            {
                _appliedInput = _delayedInputQueue.Dequeue().Input;
            }
        }

        private void ApplyVehicleForces()
        {
            bool hasWheelSetup = frontLeftWheel != null && frontRightWheel != null && rearLeftWheel != null && rearRightWheel != null;
            bool useIdealizedDriveOnly = forceSmoothArcadeDrive || disableWheelColliderPhysics;
            float load = cognitiveLoadManager != null ? cognitiveLoadManager.CurrentLoadNormalized : 0f;
            VehicleInputState input = GetEffectiveInput();
            float steeringNoise = (Mathf.PerlinNoise(Time.time * 1.9f, 0.37f) - 0.5f) * 2f * steeringNoiseAtMaxLoad * load;
            float desiredSteer = Mathf.Clamp(input.Steering + steeringNoise, -1f, 1f);
            _smoothedSteer = Mathf.Lerp(_smoothedSteer, desiredSteer, Time.fixedDeltaTime * steeringResponsiveness);

            if (!useIdealizedDriveOnly && hasWheelSetup)
            {
                frontLeftWheel.steerAngle = _smoothedSteer * maxSteerAngle;
                frontRightWheel.steerAngle = _smoothedSteer * maxSteerAngle;
            }

            if (!useIdealizedDriveOnly && hasWheelSetup)
            {
                float currentSpeed = SpeedMps;
                float speedLimiter = currentSpeed >= topSpeedMps ? 0f : 1f;
                float motorTorque = input.Throttle * maxMotorTorque * speedLimiter;
                rearLeftWheel.motorTorque = motorTorque;
                rearRightWheel.motorTorque = motorTorque;
            }

            float brakeInput = input.Brake;
            float brakeDelay = cognitiveLoadManager != null ? Mathf.Max(0f, cognitiveLoadManager.BrakeReactionDelaySeconds) : 0f;
            bool brakePressedThisFrame = input.Brake > 0.01f && _previousBrakeCommand <= 0.01f;
            if (brakePressedThisFrame)
            {
                _nextBrakeActivationTime = Time.time + brakeDelay;
            }

            if (brakeInput > 0.01f && Time.time < _nextBrakeActivationTime)
            {
                brakeInput = 0f;
            }

            _previousBrakeCommand = input.Brake;

            if (useIdealizedDriveOnly)
            {
                if (disableWheelColliderPhysics)
                {
                    ApplyBrake(0f);
                }
                else if (hasWheelSetup)
                {
                    ClearWheelForces();
                }

                ApplyArcadeDrive(input, brakeInput);
                return;
            }

            if (hasWheelSetup)
            {
                float brakeTorque = brakeInput * maxBrakeTorque;
                ApplyBrake(brakeTorque);

                if (input.Handbrake)
                {
                    rearLeftWheel.brakeTorque = handbrakeTorque;
                    rearRightWheel.brakeTorque = handbrakeTorque;
                }

                ApplyDynamicTraction(load);
            }

            ApplyFallbackDriveIfNeeded(input);
        }

        private void ApplyBrake(float brakeTorque)
        {
            frontLeftWheel.brakeTorque = brakeTorque;
            frontRightWheel.brakeTorque = brakeTorque;
            rearLeftWheel.brakeTorque = Mathf.Max(rearLeftWheel.brakeTorque, brakeTorque);
            rearRightWheel.brakeTorque = Mathf.Max(rearRightWheel.brakeTorque, brakeTorque);
        }

        private void ApplyDynamicTraction(float load)
        {
            float baseTraction = rainModeEnabled ? tractionStiffnessRain : tractionStiffnessDry;
            float cognitivePenalty = Mathf.Lerp(0f, 0.28f, load);
            float targetStiffness = Mathf.Max(0.55f, baseTraction - cognitivePenalty);

            ApplyWheelTraction(frontLeftWheel, targetStiffness);
            ApplyWheelTraction(frontRightWheel, targetStiffness);
            ApplyWheelTraction(rearLeftWheel, targetStiffness);
            ApplyWheelTraction(rearRightWheel, targetStiffness);
        }

        private static void ApplyWheelTraction(WheelCollider wheel, float stiffness)
        {
            WheelFrictionCurve forward = wheel.forwardFriction;
            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            forward.stiffness = stiffness;
            sideways.stiffness = stiffness;
            wheel.forwardFriction = forward;
            wheel.sidewaysFriction = sideways;
        }

        private void SyncWheelVisuals()
        {
            if (disableWheelColliderPhysics)
            {
                return;
            }

            UpdateWheelVisual(frontLeftWheel, frontLeftVisual);
            UpdateWheelVisual(frontRightWheel, frontRightVisual);
            UpdateWheelVisual(rearLeftWheel, rearLeftVisual);
            UpdateWheelVisual(rearRightWheel, rearRightVisual);
        }

        private void ApplyIdealizedTransformDrive(VehicleInputState input)
        {
            float dt = Time.fixedDeltaTime;
            float targetForwardSpeed = 0f;
            float accelRate = arcadeCoastDeceleration;

            if (input.Throttle > 0.01f)
            {
                targetForwardSpeed = input.Throttle * topSpeedMps;
                accelRate = arcadeForwardAcceleration;
            }
            else if (input.Brake > 0.01f)
            {
                if (_idealizedForwardSpeed > 0.25f)
                {
                    targetForwardSpeed = 0f;
                    accelRate = arcadeBrakeDeceleration;
                }
                else
                {
                    targetForwardSpeed = -input.Brake * arcadeReverseSpeedMps;
                    accelRate = arcadeReverseAcceleration;
                }
            }

            _idealizedForwardSpeed = Mathf.MoveTowards(_idealizedForwardSpeed, targetForwardSpeed, accelRate * dt);

            float speedFactor = Mathf.Clamp01(Mathf.Abs(_idealizedForwardSpeed) / Mathf.Max(1f, topSpeedMps));
            float steeringAuthority = Mathf.Lerp(0.45f, 1f, speedFactor);
            float handbrakeTurnBoost = input.Handbrake ? 1.15f : 1f;
            float reverseSteer = _idealizedForwardSpeed < -0.1f ? -1f : 1f;
            float yawStep = input.Steering * arcadeTurnRate * steeringAuthority * handbrakeTurnBoost * reverseSteer * dt;
            if (Mathf.Abs(yawStep) > 0.0001f)
            {
                transform.rotation = transform.rotation * Quaternion.Euler(0f, yawStep, 0f);
            }

            Vector3 oldPos = transform.position;
            Vector3 move = transform.forward * (_idealizedForwardSpeed * dt);
            Vector3 targetPos = oldPos + move;
            targetPos.y = _baseDriveY;
            transform.position = targetPos;
            _reportedVelocity = (targetPos - oldPos) / Mathf.Max(0.0001f, dt);

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void ApplyArcadeDrive(VehicleInputState input, float brakeInput)
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float forwardSpeed = Vector3.Dot(planarVelocity, transform.forward);
            Vector3 lateralVelocity = planarVelocity - (transform.forward * forwardSpeed);

            float targetForwardSpeed = 0f;
            float accelRate = arcadeCoastDeceleration;

            if (input.Throttle > 0.01f)
            {
                targetForwardSpeed = input.Throttle * topSpeedMps;
                accelRate = arcadeForwardAcceleration;
            }
            else if (brakeInput > 0.01f)
            {
                if (forwardSpeed > 0.25f)
                {
                    targetForwardSpeed = 0f;
                    accelRate = arcadeBrakeDeceleration;
                }
                else
                {
                    targetForwardSpeed = -brakeInput * arcadeReverseSpeedMps;
                    accelRate = arcadeReverseAcceleration;
                }
            }

            float newForwardSpeed = Mathf.MoveTowards(forwardSpeed, targetForwardSpeed, accelRate * Time.fixedDeltaTime);
            float lateralBlend = 1f - Mathf.Exp(-arcadeLateralGrip * Time.fixedDeltaTime);
            Vector3 newLateralVelocity = Vector3.Lerp(lateralVelocity, Vector3.zero, lateralBlend);
            Vector3 newPlanarVelocity = (transform.forward * newForwardSpeed) + newLateralVelocity;
            _rigidbody.linearVelocity = new Vector3(newPlanarVelocity.x, velocity.y, newPlanarVelocity.z);

            float speedFactor = Mathf.Clamp01(Mathf.Abs(newForwardSpeed) / Mathf.Max(1f, topSpeedMps));
            float steeringAuthority = Mathf.Lerp(0.45f, 1f, speedFactor);
            float handbrakeTurnBoost = input.Handbrake ? 1.2f : 1f;
            float reverseSteer = newForwardSpeed < -0.1f ? -1f : 1f;
            float yawStep = input.Steering * arcadeTurnRate * steeringAuthority * handbrakeTurnBoost * reverseSteer * Time.fixedDeltaTime;
            if (Mathf.Abs(yawStep) > 0.0001f)
            {
                _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.Euler(0f, yawStep, 0f));
            }

            // Keep yaw controlled by input so collisions do not spin-lock the player.
            _rigidbody.angularVelocity = Vector3.zero;
        }

        private void ClearWheelForces()
        {
            if (frontLeftWheel != null)
            {
                frontLeftWheel.motorTorque = 0f;
                frontLeftWheel.brakeTorque = 0f;
            }

            if (frontRightWheel != null)
            {
                frontRightWheel.motorTorque = 0f;
                frontRightWheel.brakeTorque = 0f;
            }

            if (rearLeftWheel != null)
            {
                rearLeftWheel.motorTorque = 0f;
                rearLeftWheel.brakeTorque = 0f;
            }

            if (rearRightWheel != null)
            {
                rearRightWheel.motorTorque = 0f;
                rearRightWheel.brakeTorque = 0f;
            }
        }

        private void ApplyFallbackDriveIfNeeded(VehicleInputState input)
        {
            if (!enableWASDFallbackDrive || _rigidbody == null)
            {
                return;
            }

            bool hasDriveInput = input.Throttle > 0.01f || input.Brake > 0.01f || Mathf.Abs(input.Steering) > 0.01f;
            if (!hasDriveInput || IsAnyWheelGrounded())
            {
                return;
            }

            float driveAxis = input.Throttle - input.Brake;
            if (Mathf.Abs(driveAxis) > 0.01f)
            {
                _rigidbody.linearVelocity += transform.forward * (driveAxis * fallbackAcceleration * Time.fixedDeltaTime);
                _rigidbody.linearVelocity = Vector3.ClampMagnitude(_rigidbody.linearVelocity, topSpeedMps);
            }

            if (Mathf.Abs(input.Steering) > 0.01f)
            {
                float speedFactor = Mathf.Clamp01(0.2f + (SpeedMps / Mathf.Max(1f, topSpeedMps)));
                float turnAmount = input.Steering * fallbackTurnRate * speedFactor * Time.fixedDeltaTime;
                _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.Euler(0f, turnAmount, 0f));
            }
        }

        private bool IsAnyWheelGrounded()
        {
            return (frontLeftWheel != null && frontLeftWheel.isGrounded) ||
                   (frontRightWheel != null && frontRightWheel.isGrounded) ||
                   (rearLeftWheel != null && rearLeftWheel.isGrounded) ||
                   (rearRightWheel != null && rearRightWheel.isGrounded);
        }

        private static void UpdateWheelVisual(WheelCollider wheel, Transform visual)
        {
            if (wheel == null || visual == null)
            {
                return;
            }

            wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visual.SetPositionAndRotation(position, rotation);
        }

        private void EnsureNonZeroAcceleration()
        {
            maxMotorTorque = Mathf.Max(100f, maxMotorTorque);
            fallbackAcceleration = Mathf.Max(0.5f, fallbackAcceleration);

            arcadeForwardAcceleration = Mathf.Max(0.5f, arcadeForwardAcceleration);
            arcadeBrakeDeceleration = Mathf.Max(0.5f, arcadeBrakeDeceleration);
            arcadeReverseAcceleration = Mathf.Max(0.5f, arcadeReverseAcceleration);
            arcadeCoastDeceleration = Mathf.Max(0.1f, arcadeCoastDeceleration);
            arcadeReverseSpeedMps = Mathf.Max(0.5f, arcadeReverseSpeedMps);
            topSpeedMps = Mathf.Max(1f, topSpeedMps);
        }

        private void DisableWheelColliderPhysicsIfNeeded()
        {
            if (!disableWheelColliderPhysics)
            {
                return;
            }

            DisableWheelCollider(frontLeftWheel);
            DisableWheelCollider(frontRightWheel);
            DisableWheelCollider(rearLeftWheel);
            DisableWheelCollider(rearRightWheel);
        }

        private static void DisableWheelCollider(WheelCollider wheel)
        {
            if (wheel == null)
            {
                return;
            }

            wheel.motorTorque = 0f;
            wheel.brakeTorque = 0f;
            wheel.enabled = false;
        }

        private VehicleInputState GetEffectiveInput()
        {
            VehicleInputState delayedInput = _appliedInput;
            bool hasDelayedInput = Mathf.Abs(delayedInput.Steering) > 0.01f || delayedInput.Throttle > 0.01f || delayedInput.Brake > 0.01f || delayedInput.Handbrake;
            if (hasDelayedInput)
            {
                return delayedInput;
            }

            VehicleInputState immediateInput = ReadImmediateInput();
            bool hasImmediateInput = Mathf.Abs(immediateInput.Steering) > 0.01f || immediateInput.Throttle > 0.01f || immediateInput.Brake > 0.01f || immediateInput.Handbrake;
            return hasImmediateInput ? immediateInput : delayedInput;
        }

        private static VehicleInputState ReadImmediateInput()
        {
            float steering = 0f;
            float throttle = 0f;
            float brake = 0f;
            bool handbrake = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                steering += Keyboard.current.aKey.isPressed ? -1f : 0f;
                steering += Keyboard.current.dKey.isPressed ? 1f : 0f;
                steering += Keyboard.current.leftArrowKey.isPressed ? -1f : 0f;
                steering += Keyboard.current.rightArrowKey.isPressed ? 1f : 0f;
                throttle = Keyboard.current.wKey.isPressed ? 1f : 0f;
                throttle = Mathf.Max(throttle, Keyboard.current.upArrowKey.isPressed ? 1f : 0f);
                brake = Keyboard.current.sKey.isPressed ? 1f : 0f;
                brake = Mathf.Max(brake, Keyboard.current.downArrowKey.isPressed ? 1f : 0f);
                handbrake = Keyboard.current.spaceKey.isPressed;
            }

            if (Gamepad.current != null)
            {
                steering = Mathf.Abs(Gamepad.current.leftStick.ReadValue().x) > Mathf.Abs(steering)
                    ? Gamepad.current.leftStick.ReadValue().x
                    : steering;
                throttle = Mathf.Max(throttle, Gamepad.current.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, Gamepad.current.leftTrigger.ReadValue());
                handbrake |= Gamepad.current.buttonSouth.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            steering += Input.GetKey(KeyCode.A) ? -1f : 0f;
            steering += Input.GetKey(KeyCode.D) ? 1f : 0f;
            steering += Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f;
            steering += Input.GetKey(KeyCode.RightArrow) ? 1f : 0f;
            throttle = Mathf.Max(throttle, Input.GetKey(KeyCode.W) ? 1f : 0f);
            throttle = Mathf.Max(throttle, Input.GetKey(KeyCode.UpArrow) ? 1f : 0f);
            brake = Mathf.Max(brake, Input.GetKey(KeyCode.S) ? 1f : 0f);
            brake = Mathf.Max(brake, Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            handbrake |= Input.GetKey(KeyCode.Space);
#endif

            return new VehicleInputState(steering, throttle, brake, handbrake);
        }

        public void ForceRecoverControlState()
        {
            _delayedInputQueue.Clear();
            _appliedInput = new VehicleInputState(0f, 0f, 0f, false);
            _previousBrakeCommand = 0f;
            _nextBrakeActivationTime = 0f;
            _idealizedForwardSpeed = 0f;
            _reportedVelocity = Vector3.zero;
            _baseDriveY = transform.position.y;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = forceIdealizedTransformDrive;
                _rigidbody.useGravity = !forceIdealizedTransformDrive;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.WakeUp();
            }
        }
    }
}

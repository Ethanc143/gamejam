using FocusSim.Vehicle;
using UnityEngine;

namespace FocusSim.Traffic
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TrafficVehicleAI : MonoBehaviour
    {
        [Header("Route")]
        [SerializeField] private LanePath lanePath;
        [SerializeField] private int startWaypointIndex;
        [SerializeField] private float waypointReachThreshold = 4f;

        [Header("Behavior")]
        [SerializeField] private float desiredSpeedMps = 11f;
        [SerializeField] private float acceleration = 3f;
        [SerializeField] private float braking = 5f;
        [SerializeField] private float steeringLerp = 3.8f;
        [SerializeField, Range(0f, 1f)] private float laneTangentBlend = 0.45f;
        [SerializeField, Range(0f, 20f)] private float laneCenteringStrength = 9f;
        [SerializeField, Range(0f, 30f)] private float hardRecenterDistance = 7f;
        [SerializeField] private float followingDistance = 10f;
        [SerializeField] private float obstacleSensorDistance = 17f;
        [SerializeField] private float predictiveCheckDistance = 28f;
        [SerializeField] private float predictiveCollisionRadius = 2.8f;
        [SerializeField] private float criticalCollisionTtcSeconds = 0.8f;
        [SerializeField] private float predictiveHorizonSeconds = 3.2f;
        [SerializeField] private float emergencyStopDistance = 2.4f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Traffic Light Compliance")]
        [SerializeField] private TrafficLightController trafficLight;
        [SerializeField] private Transform stopLine;
        [SerializeField] private float stopLineRadius = 9f;

        private Rigidbody _rb;
        private int _waypointIndex;
        private float _currentSpeed;
        private VehicleController _playerVehicle;

        public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
        public float CurrentSpeedMps => _currentSpeed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 1300f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _waypointIndex = startWaypointIndex;
        }

        private void OnEnable()
        {
            TrafficRegistry.Register(this);
        }

        private void OnDisable()
        {
            TrafficRegistry.Unregister(this);
        }

        private void FixedUpdate()
        {
            if (lanePath == null || lanePath.Count == 0)
            {
                return;
            }

            Vector3 targetWaypoint = lanePath.GetWaypoint(_waypointIndex);
            Vector3 toWaypoint = targetWaypoint - transform.position;
            toWaypoint.y = 0f;

            if (toWaypoint.magnitude <= waypointReachThreshold)
            {
                _waypointIndex = lanePath.GetNextIndex(_waypointIndex);
                targetWaypoint = lanePath.GetWaypoint(_waypointIndex);
                toWaypoint = targetWaypoint - transform.position;
                toWaypoint.y = 0f;
            }

            Vector3 laneCenter = lanePath.GetNearestPointOnPath(transform.position, out Vector3 laneTangent);
            laneTangent.y = 0f;
            Vector3 waypointHeading = toWaypoint.sqrMagnitude > 0.01f ? toWaypoint.normalized : transform.forward;
            Vector3 heading = waypointHeading;
            if (laneTangent.sqrMagnitude > 0.0001f)
            {
                heading = Vector3.Slerp(waypointHeading, laneTangent.normalized, Mathf.Clamp01(laneTangentBlend));
            }

            if (heading.sqrMagnitude < 0.0001f)
            {
                heading = transform.forward;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(heading, Vector3.up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, desiredRotation, steeringLerp * Time.fixedDeltaTime));

            float speedLimit = Mathf.Min(desiredSpeedMps, lanePath.LaneSpeedLimitMps);
            float targetSpeed = EvaluateTargetSpeed(speedLimit);

            if (_currentSpeed < targetSpeed)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
            }
            else
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, braking * Time.fixedDeltaTime);
            }

            _rb.linearVelocity = transform.forward * _currentSpeed;
            ConstrainToRoadLane(laneCenter, laneTangent);
        }

        public void AssignLane(LanePath path, int initialWaypoint = 0)
        {
            lanePath = path;
            _waypointIndex = Mathf.Max(0, initialWaypoint);
        }

        public void AssignTrafficControl(TrafficLightController light, Transform line)
        {
            trafficLight = light;
            stopLine = line;
        }

        public void AssignPlayerVehicle(VehicleController playerVehicle)
        {
            _playerVehicle = playerVehicle;
        }

        private float EvaluateTargetSpeed(float baseSpeed)
        {
            float target = baseSpeed;

            if (RequiresTrafficLightStop())
            {
                target = 0f;
            }

            target = Mathf.Min(target, EvaluateSensorFollowingTarget(baseSpeed));
            target = Mathf.Min(target, EvaluatePredictiveAvoidanceTarget(baseSpeed));
            return Mathf.Max(0f, target);
        }

        private float EvaluateSensorFollowingTarget(float baseSpeed)
        {
            float target = baseSpeed;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.9f, transform.forward, out RaycastHit hit, obstacleSensorDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody != _rb)
                {
                    float distance = Mathf.Max(0.1f, hit.distance - 1f);
                    float followFactor = Mathf.InverseLerp(0f, followingDistance, distance);
                    target = Mathf.Min(target, baseSpeed * followFactor);
                }
            }

            return Mathf.Max(0f, target);
        }

        private float EvaluatePredictiveAvoidanceTarget(float baseSpeed)
        {
            if (_rb == null)
            {
                return baseSpeed;
            }

            float target = baseSpeed;
            Vector3 selfPos = transform.position;
            Vector3 selfVel = _rb.linearVelocity;
            selfVel.y = 0f;

            foreach (TrafficVehicleAI other in TrafficRegistry.Vehicles)
            {
                if (other == null || other == this)
                {
                    continue;
                }

                target = Mathf.Min(target, EvaluateObstacleTarget(baseSpeed, selfPos, selfVel, other.transform.position, other.Velocity));
                if (target <= 0.01f)
                {
                    return 0f;
                }
            }

            if (_playerVehicle != null)
            {
                target = Mathf.Min(target, EvaluateObstacleTarget(baseSpeed, selfPos, selfVel, _playerVehicle.transform.position, _playerVehicle.Velocity));
            }

            return Mathf.Clamp(target, 0f, baseSpeed);
        }

        private float EvaluateObstacleTarget(float baseSpeed, Vector3 selfPos, Vector3 selfVel, Vector3 otherPos, Vector3 otherVel)
        {
            Vector3 toOther = otherPos - selfPos;
            toOther.y = 0f;
            float distance = toOther.magnitude;
            if (distance < 0.001f || distance > predictiveCheckDistance)
            {
                return baseSpeed;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            float forwardDistance = Vector3.Dot(toOther, forward);
            if (forwardDistance < -1.5f)
            {
                return baseSpeed;
            }

            if (distance <= emergencyStopDistance)
            {
                return 0f;
            }

            Vector3 planarOtherVelocity = otherVel;
            planarOtherVelocity.y = 0f;
            Vector3 relativePosition = toOther;
            Vector3 relativeVelocity = planarOtherVelocity - selfVel;

            float relativeSpeedSqr = relativeVelocity.sqrMagnitude;
            float timeToClosest = 0f;
            if (relativeSpeedSqr > 0.01f)
            {
                timeToClosest = Mathf.Clamp(-Vector3.Dot(relativePosition, relativeVelocity) / relativeSpeedSqr, 0f, predictiveHorizonSeconds);
            }

            Vector3 closestOffset = relativePosition + (relativeVelocity * timeToClosest);
            float closestDistance = closestOffset.magnitude;
            bool likelyCollisionPath = closestDistance <= predictiveCollisionRadius && (timeToClosest > 0.01f || forwardDistance > 0f);
            if (!likelyCollisionPath)
            {
                if (forwardDistance <= 0f)
                {
                    return baseSpeed;
                }

                Vector3 closingRelative = selfVel - planarOtherVelocity;
                float closingSpeed = Vector3.Dot(closingRelative, toOther.normalized);
                if (closingSpeed <= 0.05f || distance >= followingDistance * 1.8f)
                {
                    return baseSpeed;
                }

                float followFactor = Mathf.InverseLerp(0f, Mathf.Max(0.1f, followingDistance * 1.8f), distance);
                return baseSpeed * followFactor;
            }

            if (timeToClosest <= criticalCollisionTtcSeconds)
            {
                return 0f;
            }

            float ttcFactor = Mathf.InverseLerp(criticalCollisionTtcSeconds, predictiveHorizonSeconds, timeToClosest);
            float distanceFactor = Mathf.Clamp01((closestDistance - 0.3f) / Mathf.Max(0.1f, predictiveCollisionRadius));
            float safeFactor = Mathf.Clamp01(Mathf.Min(ttcFactor, distanceFactor));
            return baseSpeed * safeFactor;
        }

        private bool RequiresTrafficLightStop()
        {
            if (trafficLight == null || stopLine == null || !trafficLight.IsStopRequired)
            {
                return false;
            }

            float stopDistance = Vector3.Distance(transform.position, stopLine.position);
            if (stopDistance > stopLineRadius)
            {
                return false;
            }

            Vector3 toLine = (stopLine.position - transform.position).normalized;
            return Vector3.Dot(transform.forward, toLine) > 0.4f;
        }

        private void ConstrainToRoadLane(Vector3 laneCenter, Vector3 laneTangent)
        {
            Vector3 current = _rb.position;
            Vector3 target = new Vector3(laneCenter.x, current.y, laneCenter.z);
            Vector3 delta = target - current;
            delta.y = 0f;
            float planarDistance = delta.magnitude;
            if (planarDistance < 0.001f)
            {
                return;
            }

            if (planarDistance > hardRecenterDistance)
            {
                _rb.position = target;
                Vector3 forward = laneTangent.sqrMagnitude > 0.001f ? laneTangent.normalized : transform.forward;
                _rb.rotation = Quaternion.LookRotation(forward, Vector3.up);
                _rb.linearVelocity = forward * _currentSpeed;
                return;
            }

            float centerBlend = 1f - Mathf.Exp(-laneCenteringStrength * Time.fixedDeltaTime);
            Vector3 corrected = Vector3.Lerp(current, target, centerBlend);
            _rb.MovePosition(corrected);
        }
    }
}

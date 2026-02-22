using UnityEngine;

namespace FocusSim.Traffic
{
    public sealed class PedestrianAgent : MonoBehaviour
    {
        [SerializeField] private Transform pointA;
        [SerializeField] private Transform pointB;
        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float waitAtEndsSeconds = 2f;
        [SerializeField] private TrafficLightController linkedTrafficLight;

        private Transform _target;
        private float _waitTimer;

        public void Initialize(Transform a, Transform b, TrafficLightController light = null)
        {
            pointA = a;
            pointB = b;
            linkedTrafficLight = light;
            _target = pointB;
            if (pointA != null)
            {
                transform.position = pointA.position;
            }
        }

        private void Start()
        {
            _target = pointB;
        }

        private void Update()
        {
            if (pointA == null || pointB == null)
            {
                return;
            }

            bool canCross = linkedTrafficLight == null || linkedTrafficLight.CurrentPhase == TrafficLightPhase.Red;
            if (!canCross)
            {
                return;
            }

            if (_waitTimer > 0f)
            {
                _waitTimer -= Time.deltaTime;
                return;
            }

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= 0.15f)
            {
                _waitTimer = waitAtEndsSeconds;
                _target = _target == pointA ? pointB : pointA;
                return;
            }

            Vector3 step = toTarget.normalized * (walkSpeed * Time.deltaTime);
            transform.position += step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget.normalized, Vector3.up), Time.deltaTime * 8f);
        }
    }
}

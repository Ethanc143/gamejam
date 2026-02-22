using FocusSim.Cognitive;
using UnityEngine;

namespace FocusSim.Vehicle
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollowRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody targetRigidbody;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4.5f, -8.5f);
        [SerializeField] private float followSmoothTime = 0.16f;
        [SerializeField] private float turnAlignSpeed = 5.8f;
        [SerializeField] private float velocityLookAhead = 0.08f;
        [SerializeField] private CognitiveLoadManager cognitiveLoadManager;
        [SerializeField] private float baseFov = 64f;
        [SerializeField] private float stressFovBoost = 6f;

        private Camera _camera;
        private Vector3 _velocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.fieldOfView = baseFov;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 forward = target.forward;
            if (targetRigidbody != null && targetRigidbody.linearVelocity.sqrMagnitude > 1f)
            {
                forward = Vector3.Slerp(forward, targetRigidbody.linearVelocity.normalized, velocityLookAhead);
            }

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = target.forward;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            Quaternion heading = Quaternion.LookRotation(forward.normalized, Vector3.up);
            Vector3 desiredPos = target.position + heading * offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocity, followSmoothTime);

            Vector3 lookTarget = target.position + Vector3.up * 1.3f;
            Vector3 lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * turnAlignSpeed);
            }

            float stress = cognitiveLoadManager != null ? cognitiveLoadManager.CurrentLoadNormalized : 0f;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, baseFov + stress * stressFovBoost, Time.deltaTime * 3f);
        }

        public void Bind(Transform followTarget, Rigidbody followRb)
        {
            target = followTarget;
            targetRigidbody = followRb;
        }

        public void Configure(Transform followTarget, Rigidbody followRb, CognitiveLoadManager loadManager)
        {
            Bind(followTarget, followRb);
            cognitiveLoadManager = loadManager;
        }
    }
}

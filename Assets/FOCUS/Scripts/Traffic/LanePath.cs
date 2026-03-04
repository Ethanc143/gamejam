using System.Collections.Generic;
using UnityEngine;

namespace FocusSim.Traffic
{
    public sealed class LanePath : MonoBehaviour
    {
        [SerializeField] private List<Transform> waypoints = new List<Transform>();
        [SerializeField] private bool loop = true;
        [SerializeField] private float laneSpeedLimitMps = 14f;

        public int Count => waypoints.Count;
        public bool Loop => loop;
        public float LaneSpeedLimitMps => laneSpeedLimitMps;

        public void Initialize(List<Transform> pathPoints, bool shouldLoop, float speedLimit)
        {
            waypoints.Clear();
            if (pathPoints != null)
            {
                waypoints.AddRange(pathPoints);
            }

            loop = shouldLoop;
            laneSpeedLimitMps = speedLimit;
        }

        private void OnValidate()
        {
            if (waypoints.Count == 0 && transform.childCount > 0)
            {
                waypoints.Clear();
                for (int i = 0; i < transform.childCount; i++)
                {
                    waypoints.Add(transform.GetChild(i));
                }
            }
        }

        public Vector3 GetWaypoint(int index)
        {
            if (waypoints.Count == 0)
            {
                return transform.position;
            }

            int clamped = Mathf.Clamp(index, 0, waypoints.Count - 1);
            return waypoints[clamped].position;
        }

        public int GetNextIndex(int currentIndex)
        {
            if (waypoints.Count == 0)
            {
                return 0;
            }

            int next = currentIndex + 1;
            if (next < waypoints.Count)
            {
                return next;
            }

            return loop ? 0 : waypoints.Count - 1;
        }

        public Vector3 GetNearestPointOnPath(Vector3 point, out Vector3 tangent)
        {
            if (waypoints.Count == 0)
            {
                tangent = Vector3.forward;
                return transform.position;
            }

            if (waypoints.Count == 1)
            {
                tangent = Vector3.forward;
                return waypoints[0].position;
            }

            float bestDistanceSqr = float.MaxValue;
            Vector3 bestPoint = waypoints[0].position;
            Vector3 bestTangent = Vector3.forward;
            int segmentCount = loop ? waypoints.Count : waypoints.Count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                int next = (i + 1) % waypoints.Count;
                Vector3 a = waypoints[i].position;
                Vector3 b = waypoints[next].position;
                Vector3 ab = b - a;
                float lengthSqr = ab.sqrMagnitude;
                if (lengthSqr < 0.0001f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr);
                Vector3 projected = a + ab * t;
                Vector3 planarDelta = projected - point;
                planarDelta.y = 0f;
                float distanceSqr = planarDelta.sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                bestPoint = projected;
                bestTangent = ab.normalized;
            }

            tangent = bestTangent;
            return bestPoint;
        }
    }
}

using System.Collections.Generic;
using FocusSim.Core;
using FocusSim.Vehicle;
using UnityEngine;

namespace FocusSim.Traffic
{
    public sealed class TrafficSpawnManager : MonoBehaviour
    {
        [SerializeField] private List<LanePath> lanes = new List<LanePath>();
        [SerializeField] private GameObject trafficVehiclePrefab;
        [SerializeField, Range(2, 40)] private int maxVehicles = 18;
        [SerializeField, Range(0.1f, 1f)] private float density = 0.7f;
        [SerializeField] private float spawnIntervalSeconds = 1.1f;
        [SerializeField] private float despawnDistance = 240f;
        [SerializeField] private float minSpawnDistanceFromPlayer = 45f;
        [SerializeField] private int maxSpawnAttemptsPerTick = 5;
        [SerializeField] private Transform player;
        [SerializeField] private TrafficLightController sharedTrafficLight;
        [SerializeField] private Transform sharedStopLine;

        private readonly List<TrafficVehicleAI> _spawnedVehicles = new List<TrafficVehicleAI>();
        private float _spawnTimer;
        private VehicleController _playerVehicle;

        public void Initialize(
            List<LanePath> lanePaths,
            Transform playerTransform,
            GameObject trafficPrefab = null,
            TrafficLightController trafficLight = null,
            Transform stopLine = null)
        {
            lanes.Clear();
            if (lanePaths != null)
            {
                lanes.AddRange(lanePaths);
            }

            player = playerTransform;
            _playerVehicle = playerTransform != null ? playerTransform.GetComponent<VehicleController>() : null;
            if (trafficPrefab != null)
            {
                trafficVehiclePrefab = trafficPrefab;
            }

            sharedTrafficLight = trafficLight;
            sharedStopLine = stopLine;
        }

        private void Update()
        {
            CleanupVehicles();
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer < spawnIntervalSeconds)
            {
                return;
            }

            _spawnTimer = 0f;
            int desiredCount = Mathf.RoundToInt(maxVehicles * density);
            if (_spawnedVehicles.Count >= desiredCount || lanes.Count == 0)
            {
                return;
            }

            SpawnOnRandomLane();
        }

        private void SpawnOnRandomLane()
        {
            for (int attempt = 0; attempt < Mathf.Max(1, maxSpawnAttemptsPerTick); attempt++)
            {
                LanePath lane = lanes[Random.Range(0, lanes.Count)];
                if (lane == null || lane.Count == 0)
                {
                    continue;
                }

                int startIndex = Random.Range(0, lane.Count);
                Vector3 spawnPosition = lane.GetWaypoint(startIndex) + Vector3.up * 0.5f;
                if (player != null && Vector3.Distance(player.position, spawnPosition) < minSpawnDistanceFromPlayer)
                {
                    continue;
                }

                Quaternion spawnRotation = Quaternion.LookRotation(GetLaneDirection(lane, startIndex), Vector3.up);
                GameObject vehicle = Instantiate(GetOrCreateTrafficPrefab(), spawnPosition, spawnRotation, transform);
                vehicle.SetActive(true);
                TrafficVehicleAI ai = vehicle.GetComponent<TrafficVehicleAI>();
                ai.enabled = true;
                ai.AssignLane(lane, startIndex);
                ai.AssignTrafficControl(sharedTrafficLight, sharedStopLine);
                ai.AssignPlayerVehicle(_playerVehicle);

                _spawnedVehicles.Add(ai);
                return;
            }
        }

        private void CleanupVehicles()
        {
            for (int i = _spawnedVehicles.Count - 1; i >= 0; i--)
            {
                TrafficVehicleAI vehicle = _spawnedVehicles[i];
                if (vehicle == null)
                {
                    _spawnedVehicles.RemoveAt(i);
                    continue;
                }

                if (player == null)
                {
                    continue;
                }

                if (Vector3.Distance(player.position, vehicle.transform.position) > despawnDistance)
                {
                    Destroy(vehicle.gameObject);
                    _spawnedVehicles.RemoveAt(i);
                }
            }
        }

        private GameObject GetOrCreateTrafficPrefab()
        {
            if (trafficVehiclePrefab != null)
            {
                return trafficVehiclePrefab;
            }

            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "TrafficVehicle_Fallback";
            fallback.transform.localScale = new Vector3(1.9f, 1.2f, 4.1f);
            RuntimeMaterialHelper.ApplyColor(fallback.GetComponent<Renderer>(), new Color(0.35f, 0.35f, 0.4f));
            var rb = fallback.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            fallback.AddComponent<TrafficVehicleAI>();
            fallback.SetActive(false);
            trafficVehiclePrefab = fallback;
            return trafficVehiclePrefab;
        }

        public void SetDensity(float normalizedDensity)
        {
            density = Mathf.Clamp01(normalizedDensity);
        }

        private static Vector3 GetLaneDirection(LanePath lane, int index)
        {
            int next = lane.GetNextIndex(index);
            Vector3 direction = lane.GetWaypoint(next) - lane.GetWaypoint(index);
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.forward;
            }

            return direction.normalized;
        }
    }
}

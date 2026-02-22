using System.Collections.Generic;
using FocusSim.Accessibility;
using FocusSim.Cognitive;
using FocusSim.Distraction;
using FocusSim.Replay;
using FocusSim.Risk;
using FocusSim.Scoring;
using FocusSim.Telemetry;
using FocusSim.Traffic;
using FocusSim.UI;
using FocusSim.Vehicle;
using UnityEngine;

namespace FocusSim.Core
{
    public sealed class FocusBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (FindSceneObject<FocusGameManager>() != null || FindSceneObject<FocusBootstrap>() != null)
            {
                return;
            }

            var bootstrapGo = new GameObject("FOCUS_Bootstrap");
            bootstrapGo.AddComponent<FocusBootstrap>();
        }

        private void Start()
        {
            BuildVerticalSlice();
            Destroy(gameObject);
        }

        private void BuildVerticalSlice()
        {
            GameObject root = new GameObject("FOCUS_VerticalSlice");
            CreateDirectionalLightIfMissing();

            GameObject systemsRoot = new GameObject("Systems");
            systemsRoot.transform.SetParent(root.transform, false);

            AccessibilitySettings accessibility = ScriptableObject.CreateInstance<AccessibilitySettings>();
            CognitiveLoadManager cognitiveLoad = systemsRoot.AddComponent<CognitiveLoadManager>();
            cognitiveLoad.Initialize(accessibility);

            GameObject worldRoot = new GameObject("World");
            worldRoot.transform.SetParent(root.transform, false);

            List<Transform> intersections = new List<Transform>();
            List<LanePath> lanes = CreateRoadNetwork(worldRoot.transform, intersections);
            CreateTrafficSignal(worldRoot.transform, out TrafficLightController trafficLight, out Transform stopLine);
            CreatePedestrian(worldRoot.transform, trafficLight);

            GameObject player = CreatePlayerVehicle(root.transform, cognitiveLoad, out VehicleController playerVehicle, out Rigidbody playerRb);
            Camera gameplayCamera = SetupCamera(playerVehicle.transform, playerRb, cognitiveLoad);

            DistractionManager distraction = systemsRoot.AddComponent<DistractionManager>();
            distraction.Initialize(playerVehicle, intersections);

            RiskAssessmentSystem risk = player.AddComponent<RiskAssessmentSystem>();
            risk.Initialize(playerVehicle, distraction, cognitiveLoad, intersections);
            AttachVehicleAudio(player, playerVehicle, cognitiveLoad, risk);

            DriveTelemetryRecorder telemetry = systemsRoot.AddComponent<DriveTelemetryRecorder>();
            telemetry.Initialize(playerVehicle, cognitiveLoad, risk, distraction);
            DriveScoringSystem scoring = systemsRoot.AddComponent<DriveScoringSystem>();
            FocusHUD hud = systemsRoot.AddComponent<FocusHUD>();
            hud.Initialize(playerVehicle, cognitiveLoad, risk, distraction, accessibility);

            PostDriveAnalyticsUI postDriveAnalytics = systemsRoot.AddComponent<PostDriveAnalyticsUI>();

            ReplayRecorder replayRecorder = systemsRoot.AddComponent<ReplayRecorder>();
            replayRecorder.Initialize(playerVehicle, cognitiveLoad);

            ReplayDirector replayDirector = systemsRoot.AddComponent<ReplayDirector>();
            replayDirector.Configure(replayRecorder, playerVehicle, hud, gameplayCamera);

            TrafficSpawnManager trafficSpawning = systemsRoot.AddComponent<TrafficSpawnManager>();
            trafficSpawning.Initialize(lanes, player.transform, null, trafficLight, stopLine);

            FocusGameManager gameManager = systemsRoot.AddComponent<FocusGameManager>();
            gameManager.Initialize(playerVehicle, risk, telemetry, scoring, postDriveAnalytics);
        }

        private static GameObject CreatePlayerVehicle(
            Transform parent,
            CognitiveLoadManager loadManager,
            out VehicleController controller,
            out Rigidbody rb)
        {
            var vehicleRoot = new GameObject("PlayerVehicle");
            vehicleRoot.transform.SetParent(parent, false);
            vehicleRoot.transform.position = new Vector3(-67f, 1f, -66f);
            vehicleRoot.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            BoxCollider bodyCollider = vehicleRoot.AddComponent<BoxCollider>();
            bodyCollider.size = new Vector3(1.95f, 1.2f, 4.2f);
            bodyCollider.center = new Vector3(0f, 0.55f, 0f);

            rb = vehicleRoot.AddComponent<Rigidbody>();
            rb.mass = 1450f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyVisual.name = "BodyVisual";
            bodyVisual.transform.SetParent(vehicleRoot.transform, false);
            bodyVisual.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            bodyVisual.transform.localScale = new Vector3(1.9f, 1.1f, 4f);
            bodyVisual.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.2f);
            Destroy(bodyVisual.GetComponent<Collider>());

            var inputSource = vehicleRoot.AddComponent<VehicleInputSource>();
            controller = vehicleRoot.AddComponent<VehicleController>();

            CreateWheel(vehicleRoot.transform, "FL", new Vector3(-0.86f, 0.35f, 1.45f), out WheelCollider fl, out Transform flVisual);
            CreateWheel(vehicleRoot.transform, "FR", new Vector3(0.86f, 0.35f, 1.45f), out WheelCollider fr, out Transform frVisual);
            CreateWheel(vehicleRoot.transform, "RL", new Vector3(-0.86f, 0.35f, -1.45f), out WheelCollider rl, out Transform rlVisual);
            CreateWheel(vehicleRoot.transform, "RR", new Vector3(0.86f, 0.35f, -1.45f), out WheelCollider rr, out Transform rrVisual);

            controller.Configure(fl, fr, rl, rr, flVisual, frVisual, rlVisual, rrVisual, loadManager);
            inputSource.enabled = true;
            return vehicleRoot;
        }

        private static void CreateWheel(
            Transform parent,
            string id,
            Vector3 localPosition,
            out WheelCollider wheelCollider,
            out Transform wheelVisual)
        {
            var wheelRoot = new GameObject($"Wheel_{id}");
            wheelRoot.transform.SetParent(parent, false);
            wheelRoot.transform.localPosition = localPosition;

            wheelCollider = wheelRoot.AddComponent<WheelCollider>();
            wheelCollider.radius = 0.35f;
            wheelCollider.suspensionDistance = 0.2f;
            JointSpring spring = wheelCollider.suspensionSpring;
            spring.spring = 32000f;
            spring.damper = 4200f;
            spring.targetPosition = 0.5f;
            wheelCollider.suspensionSpring = spring;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = $"WheelVisual_{id}";
            visual.transform.SetParent(wheelRoot.transform, false);
            visual.transform.localScale = new Vector3(0.72f, 0.2f, 0.72f);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            visual.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.08f);
            Destroy(visual.GetComponent<Collider>());

            wheelVisual = visual.transform;
        }

        private static Camera SetupCamera(Transform target, Rigidbody targetRb, CognitiveLoadManager loadManager)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = FindSceneObject<Camera>();
            }

            if (camera == null)
            {
                GameObject cameraGo = new GameObject("Main Camera");
                camera = cameraGo.AddComponent<Camera>();
                camera.tag = "MainCamera";
                cameraGo.AddComponent<AudioListener>();
            }

            CameraFollowRig followRig = camera.GetComponent<CameraFollowRig>();
            if (followRig == null)
            {
                followRig = camera.gameObject.AddComponent<CameraFollowRig>();
            }

            followRig.Configure(target, targetRb, loadManager);
            camera.transform.position = target.position + new Vector3(0f, 4.5f, -8.5f);
            camera.transform.LookAt(target.position + Vector3.up * 1.2f);
            return camera;
        }

        private static List<LanePath> CreateRoadNetwork(Transform parent, List<Transform> intersections)
        {
            var lanes = new List<LanePath>();
            GameObject roadRoot = new GameObject("RoadNetwork");
            roadRoot.transform.SetParent(parent, false);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(roadRoot.transform, false);
            ground.transform.localScale = new Vector3(36f, 1f, 36f);
            ground.GetComponent<Renderer>().material.color = new Color(0.29f, 0.34f, 0.29f);

            CreateRoadVisual(roadRoot.transform, new Vector3(0f, 0.03f, 72f), new Vector3(170f, 0.1f, 14f));
            CreateRoadVisual(roadRoot.transform, new Vector3(0f, 0.03f, -72f), new Vector3(170f, 0.1f, 14f));
            CreateRoadVisual(roadRoot.transform, new Vector3(72f, 0.03f, 0f), new Vector3(14f, 0.1f, 170f));
            CreateRoadVisual(roadRoot.transform, new Vector3(-72f, 0.03f, 0f), new Vector3(14f, 0.1f, 170f));
            CreateRoadVisual(roadRoot.transform, new Vector3(0f, 0.03f, 0f), new Vector3(170f, 0.1f, 12f));
            CreateRoadVisual(roadRoot.transform, new Vector3(0f, 0.03f, 0f), new Vector3(12f, 0.1f, 170f));

            CreateLaneMarkings(roadRoot.transform);
            CreateBuildingBlocks(parent);

            intersections.Add(CreateMarker(roadRoot.transform, "Intersection_Center", Vector3.zero));
            intersections.Add(CreateMarker(roadRoot.transform, "Intersection_NW", new Vector3(-72f, 0f, 72f)));
            intersections.Add(CreateMarker(roadRoot.transform, "Intersection_NE", new Vector3(72f, 0f, 72f)));
            intersections.Add(CreateMarker(roadRoot.transform, "Intersection_SW", new Vector3(-72f, 0f, -72f)));
            intersections.Add(CreateMarker(roadRoot.transform, "Intersection_SE", new Vector3(72f, 0f, -72f)));

            lanes.Add(CreateLanePath(
                roadRoot.transform,
                "Lane_LoopClockwise",
                new[]
                {
                    new Vector3(-67f, 0.2f, -67f),
                    new Vector3(-67f, 0.2f, 67f),
                    new Vector3(67f, 0.2f, 67f),
                    new Vector3(67f, 0.2f, -67f)
                },
                14f));

            lanes.Add(CreateLanePath(
                roadRoot.transform,
                "Lane_LoopCounter",
                new[]
                {
                    new Vector3(74f, 0.2f, -74f),
                    new Vector3(74f, 0.2f, 74f),
                    new Vector3(-74f, 0.2f, 74f),
                    new Vector3(-74f, 0.2f, -74f)
                },
                13f));

            lanes.Add(CreateLanePath(
                roadRoot.transform,
                "Lane_CrossHorizontal",
                new[]
                {
                    new Vector3(-92f, 0.2f, 4f),
                    new Vector3(92f, 0.2f, 4f)
                },
                10.5f));

            lanes.Add(CreateLanePath(
                roadRoot.transform,
                "Lane_CrossVertical",
                new[]
                {
                    new Vector3(4f, 0.2f, -92f),
                    new Vector3(4f, 0.2f, 92f)
                },
                10.5f));

            return lanes;
        }

        private static LanePath CreateLanePath(Transform parent, string laneName, Vector3[] points, float speedLimit)
        {
            GameObject laneGo = new GameObject(laneName);
            laneGo.transform.SetParent(parent, false);
            LanePath lanePath = laneGo.AddComponent<LanePath>();

            List<Transform> pathPoints = new List<Transform>(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                GameObject waypoint = new GameObject($"WP_{i:00}");
                waypoint.transform.SetParent(laneGo.transform, false);
                waypoint.transform.position = points[i];
                pathPoints.Add(waypoint.transform);
            }

            lanePath.Initialize(pathPoints, true, speedLimit);
            return lanePath;
        }

        private static void CreateLaneMarkings(Transform parent)
        {
            for (int i = -8; i <= 8; i++)
            {
                float offset = i * 10f;
                CreateRoadStripe(parent, new Vector3(offset, 0.08f, 0f), new Vector3(4f, 0.03f, 0.33f));
                CreateRoadStripe(parent, new Vector3(0f, 0.08f, offset), new Vector3(0.33f, 0.03f, 4f));
            }
        }

        private static void CreateRoadVisual(Transform parent, Vector3 position, Vector3 size)
        {
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "RoadSegment";
            road.transform.SetParent(parent, false);
            road.transform.position = position;
            road.transform.localScale = size;
            road.GetComponent<Renderer>().material.color = new Color(0.14f, 0.14f, 0.15f);
            Destroy(road.GetComponent<Collider>());
        }

        private static void CreateRoadStripe(Transform parent, Vector3 position, Vector3 size)
        {
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "LaneStripe";
            stripe.transform.SetParent(parent, false);
            stripe.transform.position = position;
            stripe.transform.localScale = size;
            stripe.GetComponent<Renderer>().material.color = new Color(0.92f, 0.92f, 0.85f);
            Destroy(stripe.GetComponent<Collider>());
        }

        private static void CreateBuildingBlocks(Transform parent)
        {
            Vector3[] positions =
            {
                new Vector3(-110f, 6f, -110f), new Vector3(-110f, 8f, 0f), new Vector3(-110f, 7f, 110f),
                new Vector3(110f, 7f, -110f), new Vector3(110f, 6f, 0f), new Vector3(110f, 9f, 110f),
                new Vector3(0f, 10f, -110f), new Vector3(0f, 9f, 110f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                building.name = $"Building_{i:00}";
                building.transform.SetParent(parent, false);
                building.transform.position = positions[i];
                building.transform.localScale = new Vector3(18f, positions[i].y * 1.8f, 18f);
                building.GetComponent<Renderer>().material.color = new Color(0.22f + (i % 3) * 0.06f, 0.23f, 0.27f);
            }
        }

        private static Transform CreateMarker(Transform parent, string name, Vector3 position)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            return marker.transform;
        }

        private static void CreateTrafficSignal(Transform parent, out TrafficLightController lightController, out Transform stopLine)
        {
            GameObject signal = new GameObject("TrafficSignal");
            signal.transform.SetParent(parent, false);
            signal.transform.position = new Vector3(8f, 0f, -8f);

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(signal.transform, false);
            pole.transform.localScale = new Vector3(0.2f, 2.2f, 0.2f);
            pole.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            pole.GetComponent<Renderer>().material.color = new Color(0.26f, 0.26f, 0.28f);

            GameObject lightVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lightVisual.name = "Lamp";
            lightVisual.transform.SetParent(signal.transform, false);
            lightVisual.transform.localScale = Vector3.one * 0.65f;
            lightVisual.transform.localPosition = new Vector3(0f, 4.6f, 0f);

            lightController = signal.AddComponent<TrafficLightController>();
            lightController.Initialize(lightVisual.GetComponent<Renderer>());

            GameObject stopLineGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stopLineGo.name = "StopLine";
            stopLineGo.transform.SetParent(parent, false);
            stopLineGo.transform.position = new Vector3(0f, 0.06f, -9f);
            stopLineGo.transform.localScale = new Vector3(11f, 0.03f, 0.5f);
            stopLineGo.GetComponent<Renderer>().material.color = Color.white;
            Destroy(stopLineGo.GetComponent<Collider>());
            stopLine = stopLineGo.transform;
        }

        private static void CreatePedestrian(Transform parent, TrafficLightController trafficLight)
        {
            GameObject pedRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pedRoot.name = "Pedestrian";
            pedRoot.transform.SetParent(parent, false);
            pedRoot.transform.position = new Vector3(-2f, 1f, -13f);
            pedRoot.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            pedRoot.GetComponent<Renderer>().material.color = new Color(0.87f, 0.73f, 0.63f);

            Collider defaultCollider = pedRoot.GetComponent<Collider>();
            if (defaultCollider != null)
            {
                Destroy(defaultCollider);
            }

            // Explicit box hitbox so pedestrian contact is predictable.
            BoxCollider collisionBox = pedRoot.AddComponent<BoxCollider>();
            collisionBox.center = Vector3.zero;
            collisionBox.size = new Vector3(0.9f, 1.9f, 0.9f);

            GameObject pointA = new GameObject("PedPointA");
            pointA.transform.SetParent(parent, false);
            pointA.transform.position = new Vector3(-2f, 0.9f, -13f);

            GameObject pointB = new GameObject("PedPointB");
            pointB.transform.SetParent(parent, false);
            pointB.transform.position = new Vector3(-2f, 0.9f, 13f);

            PedestrianAgent agent = pedRoot.AddComponent<PedestrianAgent>();
            agent.Initialize(pointA.transform, pointB.transform, trafficLight);
        }

        private static void CreateDirectionalLightIfMissing()
        {
            if (FindSceneObject<Light>() != null)
            {
                return;
            }

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.18f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        }

        private static void AttachVehicleAudio(
            GameObject playerVehicleRoot,
            VehicleController vehicleController,
            CognitiveLoadManager cognitiveLoad,
            RiskAssessmentSystem riskSystem)
        {
            AudioSource engine = playerVehicleRoot.GetComponent<AudioSource>();
            if (engine == null)
            {
                engine = playerVehicleRoot.AddComponent<AudioSource>();
            }

            AudioSource tire = playerVehicleRoot.AddComponent<AudioSource>();
            AudioSource ambience = playerVehicleRoot.AddComponent<AudioSource>();
            VehicleAudioController audioController = playerVehicleRoot.AddComponent<VehicleAudioController>();

            engine.loop = true;
            tire.loop = true;
            ambience.loop = true;
            engine.playOnAwake = false;
            tire.playOnAwake = false;
            ambience.playOnAwake = false;

            audioController.Initialize(vehicleController, cognitiveLoad, riskSystem, engine, tire, ambience);
        }

        private static T FindSceneObject<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return FindAnyObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }
}

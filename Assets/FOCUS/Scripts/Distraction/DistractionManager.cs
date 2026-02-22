using System;
using System.Collections.Generic;
using FocusSim.Core;
using FocusSim.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FocusSim.Distraction
{
    public sealed class DistractionManager : MonoBehaviour
    {
        [SerializeField] private DistractionEpisodeCatalog episodeCatalog;
        [SerializeField] private VehicleController playerVehicle;
        [SerializeField] private List<Transform> intersectionPoints = new List<Transform>();
        [SerializeField] private float intersectionContextRadius = 22f;
        [SerializeField] private float minEpisodeGapSeconds = 18f;
        [SerializeField] private bool useFallbackEpisodesIfCatalogMissing = true;

        private readonly List<DistractionEpisodeDefinition> _episodes = new List<DistractionEpisodeDefinition>();
        private readonly HashSet<string> _playedEpisodeIds = new HashSet<string>();
        private RuntimeEpisode _activeEpisode;
        private float _driveTime;
        private float _cooldownTimer;
        private bool _episodesLoaded;

        public event Action<RuntimeEpisode> EpisodeStarted;
        public event Action<RuntimeEpisode> EpisodeUpdated;
        public event Action<RuntimeEpisode, EpisodeDecision, float> EpisodeEnded;

        public RuntimeEpisode ActiveEpisode => _activeEpisode != null && _activeEpisode.Definition != null ? _activeEpisode : null;
        public float CurrentRiskMultiplier => _activeEpisode != null && _activeEpisode.Definition != null ? _activeEpisode.Definition.riskMultiplier : 1f;
        public bool IsAttentionCompromised => _activeEpisode != null && _activeEpisode.Definition != null;

        [Serializable]
        public sealed class RuntimeEpisode
        {
            public DistractionEpisodeDefinition Definition;
            public float ElapsedSeconds;
            public bool IsResolved;
        }

        private void Start()
        {
            EnsureEpisodesLoaded();
        }

        private void Update()
        {
            _driveTime += Time.deltaTime;

            if (_activeEpisode != null)
            {
                TickActiveEpisode();
                return;
            }

            _cooldownTimer += Time.deltaTime;
            if (_cooldownTimer < minEpisodeGapSeconds)
            {
                return;
            }

            DistractionEpisodeDefinition candidate = FindNextEpisode();
            if (candidate != null)
            {
                StartEpisode(candidate);
                _cooldownTimer = 0f;
            }
        }

        private void LoadEpisodes()
        {
            _episodes.Clear();

            if (episodeCatalog != null)
            {
                for (int i = 0; i < episodeCatalog.episodes.Count; i++)
                {
                    DistractionEpisodeDefinition episode = episodeCatalog.episodes[i];
                    if (episode != null)
                    {
                        _episodes.Add(episode);
                    }
                }
            }

            if (_episodes.Count == 0 && useFallbackEpisodesIfCatalogMissing)
            {
                _episodes.AddRange(BuildFallbackEpisodes());
            }

            _episodesLoaded = true;
        }

        public void Initialize(VehicleController vehicleController, List<Transform> intersections, DistractionEpisodeCatalog catalog = null)
        {
            playerVehicle = vehicleController;
            intersectionPoints.Clear();
            if (intersections != null)
            {
                intersectionPoints.AddRange(intersections);
            }

            if (catalog != null)
            {
                episodeCatalog = catalog;
            }

            EnsureEpisodesLoaded();
        }

        private void EnsureEpisodesLoaded()
        {
            if (!_episodesLoaded)
            {
                LoadEpisodes();
            }
        }

        private DistractionEpisodeDefinition FindNextEpisode()
        {
            if (_episodes.Count == 0)
            {
                return null;
            }

            List<DistractionEpisodeDefinition> candidates = new List<DistractionEpisodeDefinition>();
            for (int i = 0; i < _episodes.Count; i++)
            {
                DistractionEpisodeDefinition episode = _episodes[i];
                if (episode == null || _playedEpisodeIds.Contains(episode.episodeId))
                {
                    continue;
                }

                if (CanTrigger(episode))
                {
                    candidates.Add(episode);
                }
            }

            if (candidates.Count == 0 && _playedEpisodeIds.Count >= _episodes.Count)
            {
                _playedEpisodeIds.Clear();
                return FindNextEpisode();
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private bool CanTrigger(DistractionEpisodeDefinition episode)
        {
            if (_driveTime < episode.minDriveTimeSeconds || _driveTime > episode.maxDriveTimeSeconds)
            {
                return false;
            }

            float speed = playerVehicle != null ? playerVehicle.SpeedMps : 0f;
            if (speed < episode.minVehicleSpeedMps)
            {
                return false;
            }

            bool hasIntersectionContext = IsNearIntersection();
            return episode.triggerMode switch
            {
                EpisodeTriggerMode.TimeWindow => true,
                EpisodeTriggerMode.Contextual => !episode.requireIntersectionProximity || hasIntersectionContext,
                EpisodeTriggerMode.TimeOrContext => !episode.requireIntersectionProximity || hasIntersectionContext,
                _ => false
            };
        }

        private bool IsNearIntersection()
        {
            if (playerVehicle == null || intersectionPoints.Count == 0)
            {
                return false;
            }

            Vector3 playerPos = playerVehicle.transform.position;
            for (int i = 0; i < intersectionPoints.Count; i++)
            {
                Transform point = intersectionPoints[i];
                if (point == null)
                {
                    continue;
                }

                if (Vector3.Distance(playerPos, point.position) <= intersectionContextRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private void StartEpisode(DistractionEpisodeDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            _activeEpisode = new RuntimeEpisode
            {
                Definition = definition,
                ElapsedSeconds = 0f,
                IsResolved = false
            };

            FocusEventBus.RaiseDistractionLifecycle(
                new DistractionLifecycleEvent(
                    definition.episodeId,
                    definition.displayName,
                    true,
                    definition.durationSeconds,
                    definition.cognitiveLoadValue,
                    definition.riskMultiplier));

            EpisodeStarted?.Invoke(_activeEpisode);
        }

        private void TickActiveEpisode()
        {
            if (_activeEpisode == null)
            {
                return;
            }

            if (_activeEpisode.Definition == null)
            {
                _activeEpisode = null;
                return;
            }

            _activeEpisode.ElapsedSeconds += Time.deltaTime;
            EpisodeUpdated?.Invoke(_activeEpisode);

            bool ignorePressed = false;
            bool engagePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key != null)
                {
                    ignorePressed |= Keyboard.current.digit1Key.wasPressedThisFrame;
                }

                if (Keyboard.current.digit2Key != null)
                {
                    engagePressed |= Keyboard.current.digit2Key.wasPressedThisFrame;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            ignorePressed |= Input.GetKeyDown(KeyCode.Alpha1);
            engagePressed |= Input.GetKeyDown(KeyCode.Alpha2);
#endif

            if (ignorePressed)
            {
                ResolveEpisode(EpisodeDecision.Ignore);
                return;
            }

            if (engagePressed)
            {
                ResolveEpisode(EpisodeDecision.Engage);
                return;
            }

            if (_activeEpisode.ElapsedSeconds >= _activeEpisode.Definition.durationSeconds)
            {
                ResolveEpisode(EpisodeDecision.TimedOut);
            }
        }

        private void ResolveEpisode(EpisodeDecision decision)
        {
            if (_activeEpisode == null)
            {
                return;
            }

            RuntimeEpisode finishedEpisode = _activeEpisode;
            _activeEpisode.IsResolved = true;
            _activeEpisode = null;

            if (finishedEpisode.Definition == null)
            {
                return;
            }

            _playedEpisodeIds.Add(finishedEpisode.Definition.episodeId);

            float eyesOffRoadSeconds = EstimateEyesOffRoadSeconds(finishedEpisode, decision);
            FocusEventBus.RaiseDistractionLifecycle(
                new DistractionLifecycleEvent(
                    finishedEpisode.Definition.episodeId,
                    finishedEpisode.Definition.displayName,
                    false,
                    finishedEpisode.ElapsedSeconds,
                    finishedEpisode.Definition.cognitiveLoadValue,
                    finishedEpisode.Definition.riskMultiplier,
                    decision));

            EpisodeEnded?.Invoke(finishedEpisode, decision, eyesOffRoadSeconds);
        }

        private static float EstimateEyesOffRoadSeconds(RuntimeEpisode episode, EpisodeDecision decision)
        {
            float baseFraction = episode.Definition.eyesOffRoadFraction;
            float modifier = decision switch
            {
                EpisodeDecision.Ignore => 0.35f,
                EpisodeDecision.Engage => 1f,
                EpisodeDecision.TimedOut => 0.85f,
                _ => 1f
            };
            return episode.ElapsedSeconds * baseFraction * modifier;
        }

        private static List<DistractionEpisodeDefinition> BuildFallbackEpisodes()
        {
            return new List<DistractionEpisodeDefinition>
            {
                CreateEpisode("phone_message", "Phone Message", DistractionEpisodeType.PhoneMessage, "Message from friend: 'Can you reply now?'", 22f, 65f, 11f, 1.18f),
                CreateEpisode("nav_update", "Navigation Update", DistractionEpisodeType.NavigationUpdate, "Route changed: heavy traffic ahead.", 50f, 130f, 9f, 1.1f),
                CreateEpisode("passenger_prompt", "Passenger Interaction", DistractionEpisodeType.PassengerInteraction, "Passenger asks for directions clarification.", 85f, 210f, 12f, 1.22f),
                CreateEpisode("work_call", "Work Call", DistractionEpisodeType.WorkCall, "Incoming call: Team lead.", 130f, 280f, 15f, 1.3f),
                CreateEpisode("fatigue", "Fatigue Event", DistractionEpisodeType.FatigueEvent, "Microsleep warning. Vision narrows.", 175f, 350f, 18f, 1.35f)
            };
        }

        private static DistractionEpisodeDefinition CreateEpisode(
            string id,
            string label,
            DistractionEpisodeType type,
            string prompt,
            float minTime,
            float maxTime,
            float load,
            float risk)
        {
            DistractionEpisodeDefinition definition = ScriptableObject.CreateInstance<DistractionEpisodeDefinition>();
            definition.episodeId = id;
            definition.displayName = label;
            definition.episodeType = type;
            definition.prompt = prompt;
            definition.triggerMode = EpisodeTriggerMode.TimeOrContext;
            definition.minDriveTimeSeconds = minTime;
            definition.maxDriveTimeSeconds = maxTime;
            definition.minVehicleSpeedMps = 5f;
            definition.requireIntersectionProximity = type == DistractionEpisodeType.NavigationUpdate || type == DistractionEpisodeType.PassengerInteraction;
            definition.durationSeconds = type == DistractionEpisodeType.WorkCall ? 8f : 6f;
            definition.cognitiveLoadValue = load;
            definition.riskMultiplier = risk;
            definition.successLoadBonus = load * 0.25f;
            definition.failureLoadPenalty = load * 0.6f;
            definition.eyesOffRoadFraction = Mathf.Clamp01(0.45f + load * 0.01f);
            definition.inputCompetitionStrength = Mathf.Clamp01(0.4f + (risk - 1f) * 0.35f);
            return definition;
        }
    }
}

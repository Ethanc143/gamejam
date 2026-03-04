using FocusSim.Distraction;
using UnityEditor;
using UnityEngine;

namespace FocusSim.Editor
{
    public static class DistractionCatalogSeeder
    {
        private const string CatalogFolder = "Assets/FOCUS/Resources/DistractionEpisodes";
        private const string CatalogPath = "Assets/FOCUS/Resources/DistractionEpisodeCatalog.asset";

        [MenuItem("FOCUS/Generate Default Distraction Catalog")]
        public static void GenerateDefaultCatalog()
        {
            EnsureFolder("Assets/FOCUS");
            EnsureFolder("Assets/FOCUS/Resources");
            EnsureFolder(CatalogFolder);

            DistractionEpisodeCatalog catalog = AssetDatabase.LoadAssetAtPath<DistractionEpisodeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DistractionEpisodeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.episodes.Clear();
            catalog.episodes.Add(CreateEpisode("phone_message", "Phone Message", DistractionEpisodeType.PhoneMessage, "Message from friend: 'Can you reply now?'", 22f, 65f, 11f, 1.18f));
            catalog.episodes.Add(CreateEpisode("nav_update", "Navigation Update", DistractionEpisodeType.NavigationUpdate, "Route changed: heavy traffic ahead.", 50f, 130f, 9f, 1.1f));
            catalog.episodes.Add(CreateEpisode("passenger_prompt", "Passenger Interaction", DistractionEpisodeType.PassengerInteraction, "Passenger asks for directions clarification.", 85f, 210f, 12f, 1.22f));
            catalog.episodes.Add(CreateEpisode("work_call", "Work Call", DistractionEpisodeType.WorkCall, "Incoming call: Team lead.", 130f, 280f, 15f, 1.3f));
            catalog.episodes.Add(CreateEpisode("fatigue", "Fatigue Event", DistractionEpisodeType.FatigueEvent, "Microsleep warning. Vision narrows.", 175f, 350f, 18f, 1.35f));

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;
            Debug.Log("FOCUS: Default distraction catalog generated.");
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
            string path = $"{CatalogFolder}/{id}.asset";
            DistractionEpisodeDefinition episode = AssetDatabase.LoadAssetAtPath<DistractionEpisodeDefinition>(path);
            if (episode == null)
            {
                episode = ScriptableObject.CreateInstance<DistractionEpisodeDefinition>();
                AssetDatabase.CreateAsset(episode, path);
            }

            episode.episodeId = id;
            episode.displayName = label;
            episode.episodeType = type;
            episode.prompt = prompt;
            episode.triggerMode = EpisodeTriggerMode.TimeOrContext;
            episode.minDriveTimeSeconds = minTime;
            episode.maxDriveTimeSeconds = maxTime;
            episode.minVehicleSpeedMps = 5f;
            episode.requireIntersectionProximity = type == DistractionEpisodeType.NavigationUpdate || type == DistractionEpisodeType.PassengerInteraction;
            episode.durationSeconds = type == DistractionEpisodeType.WorkCall ? 8f : 6f;
            episode.cognitiveLoadValue = load;
            episode.riskMultiplier = risk;
            episode.successLoadBonus = load * 0.25f;
            episode.failureLoadPenalty = load * 0.6f;
            episode.eyesOffRoadFraction = Mathf.Clamp01(0.45f + load * 0.01f);
            episode.inputCompetitionStrength = Mathf.Clamp01(0.4f + (risk - 1f) * 0.35f);

            EditorUtility.SetDirty(episode);
            return episode;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
            {
                return;
            }

            string parent = path.Substring(0, slash);
            string folder = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}

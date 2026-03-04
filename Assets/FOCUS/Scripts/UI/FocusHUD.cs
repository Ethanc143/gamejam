using FocusSim.Accessibility;
using FocusSim.Cognitive;
using FocusSim.Core;
using FocusSim.Distraction;
using FocusSim.Risk;
using FocusSim.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace FocusSim.UI
{
    public sealed class FocusHUD : MonoBehaviour
    {
        [SerializeField] private VehicleController playerVehicle;
        [SerializeField] private CognitiveLoadManager cognitiveLoadManager;
        [SerializeField] private RiskAssessmentSystem riskSystem;
        [SerializeField] private DistractionManager distractionManager;
        [SerializeField] private AccessibilitySettings accessibilitySettings;

        private Canvas _canvas;
        private Text _speedText;
        private Text _loadText;
        private Text _riskText;
        private Text _controlsText;
        private Image _stressVignette;
        private GameObject _distractionPanel;
        private Text _distractionHeader;
        private Text _distractionPrompt;
        private GameObject _replayPanel;
        private Text _replayText;
        private DistractionManager _boundDistractionManager;
        private bool _driveFinished;
        private const float MpsToMph = 2.2369363f;

        private void Awake()
        {
            BuildUi();
        }

        private void OnEnable()
        {
            FocusEventBus.DriveFinished += OnDriveFinished;
            RebindDistractionEvents();
        }

        private void OnDisable()
        {
            FocusEventBus.DriveFinished -= OnDriveFinished;
            UnbindDistractionEvents();
        }

        private void Update()
        {
            if (_driveFinished)
            {
                return;
            }

            if (_speedText != null && playerVehicle != null)
            {
                _speedText.text = $"Speed {playerVehicle.SpeedMps * MpsToMph:0} mph";
            }

            if (_loadText != null && cognitiveLoadManager != null)
            {
                _loadText.text = $"Load {cognitiveLoadManager.CurrentLoadNormalized * 100f:0}%";
            }

            if (_riskText != null && riskSystem != null)
            {
                _riskText.text = $"Risk {riskSystem.CurrentRisk * 100f:0}%";
                _riskText.color = GetRiskColor(riskSystem.CurrentRisk);
            }

            if (_stressVignette != null && cognitiveLoadManager != null)
            {
                float stressScale = accessibilitySettings != null ? accessibilitySettings.stressVisualIntensity : 1f;
                float alpha = Mathf.Lerp(0f, 0.38f, cognitiveLoadManager.PeripheralSuppression) * stressScale;
                _stressVignette.color = new Color(0.8f, 0.1f, 0.12f, alpha);
            }
        }

        public void SetReplayTelemetry(float speedMps, float cognitiveLoad, ReplayTriggerType triggerType)
        {
            if (_replayPanel == null || _replayText == null)
            {
                return;
            }

            _replayPanel.SetActive(true);
            string attentionShift = cognitiveLoad > 0.45f ? "Attention shift highlighted" : "Attentive baseline";
            _replayText.text = $"Replay: {triggerType}\nSpeed {speedMps * MpsToMph:0} mph\nLoad {cognitiveLoad * 100f:0}%\n{attentionShift}";
        }

        public void ClearReplayTelemetry()
        {
            if (_replayPanel != null)
            {
                _replayPanel.SetActive(false);
            }
        }

        public void Initialize(
            VehicleController vehicleController,
            CognitiveLoadManager loadManager,
            RiskAssessmentSystem riskAssessment,
            DistractionManager distraction,
            AccessibilitySettings settings = null)
        {
            playerVehicle = vehicleController;
            cognitiveLoadManager = loadManager;
            riskSystem = riskAssessment;
            distractionManager = distraction;
            accessibilitySettings = settings;
            RebindDistractionEvents();
        }

        private void OnEpisodeStarted(DistractionManager.RuntimeEpisode runtimeEpisode)
        {
            if (_distractionPanel == null || runtimeEpisode == null || runtimeEpisode.Definition == null)
            {
                return;
            }

            _distractionPanel.SetActive(true);
            _distractionHeader.text = runtimeEpisode.Definition.displayName;
            _distractionPrompt.text = $"{runtimeEpisode.Definition.prompt}\n[1] Ignore  [2] Engage";
        }

        private void OnEpisodeUpdated(DistractionManager.RuntimeEpisode runtimeEpisode)
        {
            if (_distractionPanel == null || !_distractionPanel.activeSelf || runtimeEpisode == null || runtimeEpisode.Definition == null)
            {
                return;
            }

            float remaining = Mathf.Max(0f, runtimeEpisode.Definition.durationSeconds - runtimeEpisode.ElapsedSeconds);
            _distractionPrompt.text = $"{runtimeEpisode.Definition.prompt}\n[1] Ignore  [2] Engage   ({remaining:0.0}s)";
        }

        private void OnEpisodeEnded(DistractionManager.RuntimeEpisode _, EpisodeDecision decision, float __)
        {
            if (_distractionPanel == null)
            {
                return;
            }

            _distractionPanel.SetActive(false);
            _distractionHeader.text = decision switch
            {
                EpisodeDecision.Ignore => "Ignored",
                EpisodeDecision.Engage => "Engaged",
                _ => "Timed Out"
            };
        }

        private void BuildUi()
        {
            _canvas = GetComponentInChildren<Canvas>();
            if (_canvas != null)
            {
                return;
            }

            GameObject canvasGo = new GameObject("FocusHUDCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            _speedText = CreateText("SpeedText", new Vector2(120f, -40f), "Speed 0 mph");
            _loadText = CreateText("LoadText", new Vector2(120f, -68f), "Load 0%");
            _riskText = CreateText("RiskText", new Vector2(120f, -96f), "Risk 0%");
            _controlsText = CreateText("ControlsText", new Vector2(120f, -136f), "WASD Drive | Space Handbrake | 1/2 Distraction");
            _controlsText.fontSize = 16;
            _controlsText.color = new Color(0.85f, 0.9f, 1f, 0.95f);

            _stressVignette = CreatePanel("StressVignette", new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0.8f, 0.1f, 0.1f, 0f));
            _stressVignette.raycastTarget = false;

            _distractionPanel = CreateContainer("DistractionPanel", new Vector2(0.5f, 0.18f), new Vector2(560f, 122f), new Color(0.08f, 0.08f, 0.08f, 0.82f));
            _distractionHeader = CreateText("DistractionHeader", new Vector2(0f, -5f), "Episode");
            _distractionPrompt = CreateText("DistractionPrompt", new Vector2(0f, -36f), "Prompt");
            _distractionHeader.transform.SetParent(_distractionPanel.transform, false);
            _distractionPrompt.transform.SetParent(_distractionPanel.transform, false);
            _distractionPanel.SetActive(false);

            _replayPanel = CreateContainer("ReplayPanel", new Vector2(0.84f, 0.84f), new Vector2(220f, 88f), new Color(0f, 0f, 0f, 0.6f));
            _replayText = CreateText("ReplayText", Vector2.zero, "Replay");
            _replayText.transform.SetParent(_replayPanel.transform, false);
            _replayPanel.SetActive(false);
        }

        private void RebindDistractionEvents()
        {
            if (_boundDistractionManager != null)
            {
                _boundDistractionManager.EpisodeStarted -= OnEpisodeStarted;
                _boundDistractionManager.EpisodeUpdated -= OnEpisodeUpdated;
                _boundDistractionManager.EpisodeEnded -= OnEpisodeEnded;
            }

            _boundDistractionManager = distractionManager;
            if (_boundDistractionManager == null)
            {
                return;
            }

            _boundDistractionManager.EpisodeStarted += OnEpisodeStarted;
            _boundDistractionManager.EpisodeUpdated += OnEpisodeUpdated;
            _boundDistractionManager.EpisodeEnded += OnEpisodeEnded;
        }

        private void UnbindDistractionEvents()
        {
            if (_boundDistractionManager == null)
            {
                return;
            }

            _boundDistractionManager.EpisodeStarted -= OnEpisodeStarted;
            _boundDistractionManager.EpisodeUpdated -= OnEpisodeUpdated;
            _boundDistractionManager.EpisodeEnded -= OnEpisodeEnded;
            _boundDistractionManager = null;
        }

        private void OnDriveFinished(DriveFinishedEvent _)
        {
            _driveFinished = true;
            if (_canvas != null)
            {
                _canvas.enabled = false;
            }
        }

        private Color GetRiskColor(float normalizedRisk)
        {
            if (accessibilitySettings == null || accessibilitySettings.colorblindMode == ColorblindMode.None)
            {
                return Color.Lerp(new Color(0.65f, 0.92f, 0.65f), new Color(1f, 0.3f, 0.26f), normalizedRisk);
            }

            return accessibilitySettings.colorblindMode switch
            {
                ColorblindMode.DeuteranopiaSafe => Color.Lerp(new Color(0.6f, 0.78f, 1f), new Color(0.2f, 0.35f, 0.95f), normalizedRisk),
                ColorblindMode.ProtanopiaSafe => Color.Lerp(new Color(0.9f, 0.9f, 0.6f), new Color(0.95f, 0.7f, 0.2f), normalizedRisk),
                ColorblindMode.TritanopiaSafe => Color.Lerp(new Color(0.72f, 0.88f, 0.7f), new Color(0.25f, 0.65f, 0.25f), normalizedRisk),
                _ => Color.white
            };
        }

        private Text CreateText(string name, Vector2 anchoredPosition, string value)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            Text text = go.AddComponent<Text>();
            text.font = GetBuiltinUiFont();
            text.fontSize = 20;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.text = value;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(420f, 34f);
            return text;
        }

        private static Font GetBuiltinUiFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Image CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject panelGo = new GameObject(name);
            panelGo.transform.SetParent(_canvas.transform, false);
            Image image = panelGo.AddComponent<Image>();
            image.color = color;

            RectTransform rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private GameObject CreateContainer(string name, Vector2 anchor, Vector2 size, Color color)
        {
            GameObject panelGo = new GameObject(name);
            panelGo.transform.SetParent(_canvas.transform, false);
            Image image = panelGo.AddComponent<Image>();
            image.color = color;

            RectTransform rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return panelGo;
        }
    }
}

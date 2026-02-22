using System.Text;
using FocusSim.Scoring;
using FocusSim.Telemetry;
using UnityEngine;
using UnityEngine.UI;

namespace FocusSim.UI
{
    public sealed class PostDriveAnalyticsUI : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _root;
        private Text _title;
        private Text _scoreValue;
        private Text _scoreBand;
        private Text _breakdownBody;
        private Text _adviceBody;
        private Text _restartHint;

        private void Awake()
        {
            BuildUi();
            Hide();
        }

        public void Show(DriveTelemetrySummary summary, SafetyScoreBreakdown breakdown)
        {
            _root.SetActive(true);
            int score = Mathf.Clamp(breakdown.Score, 0, 100);
            _title.text = "Safety Score";
            _scoreValue.text = score.ToString("0");
            _scoreValue.color = GetScoreColor(score);
            _scoreBand.text = BuildBandLabel(score);
            _breakdownBody.text = BuildBreakdownText(breakdown);
            _adviceBody.text = BuildAdviceText(score, summary);
            _restartHint.text = "Press R to restart";
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void BuildUi()
        {
            GameObject canvasGo = new GameObject("PostDriveAnalyticsCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.6f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _root = new GameObject("AnalyticsRoot");
            _root.transform.SetParent(_canvas.transform, false);
            Image rootBg = _root.AddComponent<Image>();
            rootBg.color = new Color(0.02f, 0.02f, 0.02f, 0.9f);

            RectTransform rootRect = _root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject panelGo = new GameObject("SafetyPanel");
            panelGo.transform.SetParent(_root.transform, false);
            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.08f, 0.1f, 0.92f);

            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.04f, 0.05f);
            panelRect.anchorMax = new Vector2(0.96f, 0.95f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            _title = CreateText("Title", panelGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(960f, 56f), 40, TextAnchor.UpperCenter);
            _scoreValue = CreateText("SafetyScoreValue", panelGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(460f, 120f), 120, TextAnchor.UpperCenter);
            _scoreBand = CreateText("ScoreBand", panelGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -192f), new Vector2(980f, 50f), 24, TextAnchor.UpperCenter);

            Text breakdownHeader = CreateText("BreakdownHeader", panelGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -252f), new Vector2(980f, 40f), 26, TextAnchor.UpperCenter);
            breakdownHeader.text = "Score Breakdown";
            _breakdownBody = CreateText("BreakdownBody", panelGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -286f), new Vector2(980f, 150f), 19, TextAnchor.UpperLeft);

            Text adviceHeader = CreateText("AdviceHeader", panelGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -448f), new Vector2(980f, 36f), 24, TextAnchor.UpperCenter);
            adviceHeader.text = "Driving Advice";
            _adviceBody = CreateText("AdviceBody", panelGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -478f), new Vector2(980f, 130f), 20, TextAnchor.UpperLeft);

            _restartHint = CreateText("RestartHint", panelGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(860f, 36f), 22, TextAnchor.MiddleCenter);
            _restartHint.color = new Color(0.86f, 0.9f, 1f, 0.95f);
        }

        private static string BuildBandLabel(int score)
        {
            return score switch
            {
                >= 85 => "Excellent safety performance",
                >= 70 => "Good safety performance",
                >= 50 => "Moderate safety performance",
                _ => "High-risk driving pattern"
            };
        }

        private static string BuildBreakdownText(SafetyScoreBreakdown breakdown)
        {
            StringBuilder builder = new StringBuilder(512);
            AppendComponent(builder, "Collisions", breakdown.CollisionPenalty01, breakdown.CollisionWeight, breakdown.TotalWeight);
            AppendComponent(builder, "Near misses", breakdown.NearMissPenalty01, breakdown.NearMissWeight, breakdown.TotalWeight);
            AppendComponent(builder, "Hard braking", breakdown.HardBrakePenalty01, breakdown.HardBrakeWeight, breakdown.TotalWeight);
            AppendComponent(builder, "Following distance", breakdown.FollowingDistancePenalty01, breakdown.FollowingDistanceWeight, breakdown.TotalWeight);
            AppendComponent(builder, "Speed compliance", breakdown.SpeedCompliancePenalty01, breakdown.SpeedComplianceWeight, breakdown.TotalWeight);
            return builder.ToString();
        }

        private static string BuildAdviceText(int score, DriveTelemetrySummary summary)
        {
            StringBuilder builder = new StringBuilder(320);
            if (score >= 85)
            {
                builder.AppendLine("- Keep this consistency: smooth inputs and stable spacing.");
            }
            else if (score >= 70)
            {
                builder.AppendLine("- Solid drive. Main next step: reduce sudden braking.");
            }
            else if (score >= 50)
            {
                builder.AppendLine("- Focus on anticipation: scan ahead and brake earlier.");
            }
            else
            {
                builder.AppendLine("- Slow down and increase space first; control comes before speed.");
            }

            if (summary == null)
            {
                return builder.ToString();
            }

            if (summary.CollisionCount > 0)
            {
                builder.AppendLine("- Prioritize avoiding contact: leave more room before lane changes.");
            }

            if (summary.NearMissCount > 0)
            {
                builder.AppendLine("- Reduce close calls by scanning cross-traffic earlier.");
            }

            if (summary.HardBrakeCount > 3)
            {
                builder.AppendLine("- Brake sooner and more gradually to avoid emergency stops.");
            }

            if (summary.SpeedCompliancePercent < 0.85f)
            {
                builder.AppendLine("- Stay closer to the speed limit for more reaction margin.");
            }

            if (summary.AverageFollowingDistanceMeters > 0.01f && summary.AverageFollowingDistanceMeters < 9f)
            {
                builder.AppendLine("- Increase following distance to roughly a 2-second gap.");
            }

            return builder.ToString();
        }

        private static void AppendComponent(StringBuilder builder, string label, float penalty01, float weight, float totalWeight)
        {
            float deduction = GetDeductionPoints(penalty01, weight, totalWeight);
            builder.AppendLine($"- {label}: -{deduction:0.0} pts");
        }

        private static float GetDeductionPoints(float penalty01, float weight, float totalWeight)
        {
            if (totalWeight <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(penalty01) * Mathf.Max(0f, weight) / totalWeight * 100f;
        }

        private static Color GetScoreColor(int score)
        {
            if (score >= 85)
            {
                return new Color(0.28f, 0.92f, 0.42f);
            }

            if (score >= 70)
            {
                return new Color(0.72f, 0.9f, 0.32f);
            }

            if (score >= 50)
            {
                return new Color(0.97f, 0.73f, 0.24f);
            }

            return new Color(0.95f, 0.3f, 0.22f);
        }

        private Text CreateText(
            string name,
            Transform parent,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject textGo = new GameObject(name);
            textGo.transform.SetParent(parent, false);
            Text text = textGo.AddComponent<Text>();
            text.font = GetBuiltinUiFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;

            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        private static Font GetBuiltinUiFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}

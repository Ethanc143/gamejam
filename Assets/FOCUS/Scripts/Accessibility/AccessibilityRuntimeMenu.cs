using UnityEngine;

namespace FocusSim.Accessibility
{
    public sealed class AccessibilityRuntimeMenu : MonoBehaviour
    {
        [SerializeField] private AccessibilitySettings settings;
        [SerializeField] private bool visibleOnStart;

        private bool _visible;

        public void Initialize(AccessibilitySettings runtimeSettings)
        {
            settings = runtimeSettings;
            _visible = visibleOnStart;
        }

        private void OnGUI()
        {
            if (!_visible || settings == null)
            {
                return;
            }

            const float width = 360f;
            const float height = 210f;
            Rect rect = new Rect(20f, 20f, width, height);
            GUI.Box(rect, "Accessibility Options");

            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 30f, width - 24f, height - 40f));
            GUILayout.Label($"Distraction Intensity: {settings.distractionIntensityScale:0.00}");
            settings.distractionIntensityScale = GUILayout.HorizontalSlider(settings.distractionIntensityScale, 0.5f, 1.5f);

            GUILayout.Label($"Reaction Buffer Assist: {settings.reactionBufferAssistSeconds:0.00}s");
            settings.reactionBufferAssistSeconds = GUILayout.HorizontalSlider(settings.reactionBufferAssistSeconds, 0f, 0.3f);

            GUILayout.Label($"Stress Visual Intensity: {settings.stressVisualIntensity:0.00}");
            settings.stressVisualIntensity = GUILayout.HorizontalSlider(settings.stressVisualIntensity, 0f, 1f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Colorblind Mode:");
            if (GUILayout.Button(settings.colorblindMode.ToString(), GUILayout.Width(170f)))
            {
                settings.colorblindMode = settings.colorblindMode switch
                {
                    ColorblindMode.None => ColorblindMode.DeuteranopiaSafe,
                    ColorblindMode.DeuteranopiaSafe => ColorblindMode.ProtanopiaSafe,
                    ColorblindMode.ProtanopiaSafe => ColorblindMode.TritanopiaSafe,
                    _ => ColorblindMode.None
                };
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}

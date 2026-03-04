using UnityEngine;

namespace FocusSim.Accessibility
{
    public enum ColorblindMode
    {
        None = 0,
        DeuteranopiaSafe = 1,
        ProtanopiaSafe = 2,
        TritanopiaSafe = 3
    }

    [CreateAssetMenu(menuName = "FOCUS/Accessibility/Settings", fileName = "AccessibilitySettings")]
    public sealed class AccessibilitySettings : ScriptableObject
    {
        [Range(0.5f, 1.5f)] public float distractionIntensityScale = 1f;
        [Range(0f, 0.3f)] public float reactionBufferAssistSeconds = 0.1f;
        [Range(0f, 1f)] public float stressVisualIntensity = 0.7f;
        public ColorblindMode colorblindMode = ColorblindMode.None;
    }
}

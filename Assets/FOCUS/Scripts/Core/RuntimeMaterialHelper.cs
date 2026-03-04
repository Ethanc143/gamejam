using UnityEngine;

namespace FocusSim.Core
{
    /// <summary>
    /// Provides materials using URP shaders so runtime-created primitives
    /// render correctly in WebGL (CreatePrimitive uses built-in shaders that are not available in URP/WebGL).
    /// Uses a material in Resources so the shader is included in WebGL builds (e.g. itch.io).
    /// </summary>
    public static class RuntimeMaterialHelper
    {
        private static Material _cachedDefaultMaterial;
        private static Shader _cachedShader;
        private static bool _triedLoadMaterial;

        /// <summary>
        /// Prefer a material loaded from Resources so the shader is guaranteed in the build (WebGL/itch.io).
        /// </summary>
        private static Material GetDefaultMaterial()
        {
            if (_triedLoadMaterial)
            {
                return _cachedDefaultMaterial;
            }

            _triedLoadMaterial = true;
            _cachedDefaultMaterial = Resources.Load<Material>("FocusDefaultMaterial");
            return _cachedDefaultMaterial;
        }

        public static Shader GetSafeShader()
        {
            Material defaultMat = GetDefaultMaterial();
            if (defaultMat != null && defaultMat.shader != null)
            {
                return defaultMat.shader;
            }

            if (_cachedShader != null)
            {
                return _cachedShader;
            }

            _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Unlit/Color");
            }

            return _cachedShader;
        }

        /// <summary>
        /// Assigns a material with the given color using a WebGL/URP-safe shader. Use this for any renderer
        /// that came from CreatePrimitive or otherwise may have a missing shader in WebGL.
        /// </summary>
        public static void ApplyColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            Material defaultMat = GetDefaultMaterial();
            if (defaultMat != null && defaultMat.shader != null)
            {
                Material mat = new Material(defaultMat);
                mat.color = color;
                renderer.material = mat;
                return;
            }

            Shader shader = GetSafeShader();
            if (shader == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning(
                    "RuntimeMaterialHelper: No URP shader found. Use menu FOCUS > Create WebGL Default Material, then rebuild WebGL.");
#endif
                return;
            }

            Material fallbackMat = new Material(shader);
            fallbackMat.color = color;
            renderer.material = fallbackMat;
        }
    }
}

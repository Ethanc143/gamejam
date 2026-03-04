using UnityEditor;
using UnityEngine;

namespace FocusSim.Editor
{
    /// <summary>
    /// Creates a default material in Resources so WebGL builds include the URP shader (fixes pink materials on itch.io).
    /// The material is created automatically when you open the project in Unity — no menu needed.
    /// </summary>
    [InitializeOnLoad]
    public static class CreateWebGLDefaultMaterial
    {
        private const string MaterialPath = "Assets/Resources/FocusDefaultMaterial.mat";

        static CreateWebGLDefaultMaterial()
        {
            EditorApplication.delayCall += EnsureMaterialExists;
        }

        private static void EnsureMaterialExists()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            {
                return;
            }
            Create();
        }

        [MenuItem("FOCUS/Create WebGL Default Material (run manually if needed)")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                Debug.LogError("FOCUS: No URP shader found. Is Universal RP installed?");
                return;
            }

            Material mat = new Material(shader)
            {
                name = "FocusDefaultMaterial",
                color = Color.gray
            };

            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log("FOCUS: Created WebGL default material in Assets/Resources. Pink materials on itch.io should be fixed — rebuild your WebGL build and re-upload.");
        }
    }
}

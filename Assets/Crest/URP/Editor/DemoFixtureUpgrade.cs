using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crest.URP.Editor
{
    public static class DemoFixtureUpgrade
    {
        public static void Upgrade()
        {
            foreach (var name in new[] { "Ocean_Storm", "Ocean_Underwater" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/CrestPortDemos/" + name + ".unity");
                var ocean = Object.FindFirstObjectByType<OceanRenderer>();
                ocean.OceanMaterial.EnableKeyword("_SHADOWS_ON");
                ocean.OceanMaterial.SetFloat("_Shadows", 1);
                EditorUtility.SetDirty(ocean.OceanMaterial);
                var data = new SerializedObject(ocean);
                data.FindProperty("_createShadowData").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (GameObject.Find("Shadow reference pier") == null)
                {
                    var pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pier.name = "Shadow reference pier";
                    pier.transform.position = new Vector3(8, 3, 0);
                    pier.transform.localScale = new Vector3(8, 6, 8);
                    pier.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrestPortDemos/Reference.mat");
                }
                if (GameObject.Find("Seafloor cache") == null)
                {
                    var cache = new GameObject("Seafloor cache");
                    cache.transform.localScale = new Vector3(100, 1, 100);
                    var component = cache.AddComponent<OceanDepthCache>();
                    component._resolution = 256;
                    component._layers = 1;
                    component._cameraMaxTerrainHeight = 30;
                    component._cameraFarClipPlane = 100;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
    }
}

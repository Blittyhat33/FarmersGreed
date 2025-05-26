using UnityEditor;
using UnityEngine;
using System.IO;

public class AutoPrefabExporter : EditorWindow
{
    private GameObject[] objectsToConvert;
    private string prefabPath = "Assets/_Project/Prefabs/Weapons";
    private string materialPath = "Assets/Materials";

    [MenuItem("Tools/Auto Prefab Exporter")]
    public static void ShowWindow()
    {
        GetWindow<AutoPrefabExporter>("Auto Prefab Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Auto Prefab Exporter", EditorStyles.boldLabel);

        if (GUILayout.Button("Load Selected Objects"))
        {
            objectsToConvert = Selection.gameObjects;
        }

        EditorGUILayout.LabelField("Prefabs will be saved to:", prefabPath);
        EditorGUILayout.LabelField("Materials will be saved to:", materialPath);

        if (GUILayout.Button("Convert to Prefabs"))
        {
            if (objectsToConvert == null || objectsToConvert.Length == 0)
            {
                Debug.LogWarning("No objects selected.");
                return;
            }

            if (!Directory.Exists(prefabPath)) Directory.CreateDirectory(prefabPath);
            if (!Directory.Exists(materialPath)) Directory.CreateDirectory(materialPath);

            foreach (var obj in objectsToConvert)
            {
                ExtractMaterials(obj);
                SaveAsPrefab(obj);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Prefab export complete.");
        }
    }

    private void ExtractMaterials(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material mat = renderer.sharedMaterials[i];
                if (mat == null) continue;

                string matPath = Path.Combine(materialPath, mat.name + ".mat");
                if (!File.Exists(matPath))
                {
                    Material newMat = new Material(mat);
                    AssetDatabase.CreateAsset(newMat, matPath);
                    renderer.sharedMaterials[i] = newMat;
                }
                else
                {
                    Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    renderer.sharedMaterials[i] = existingMat;
                }
            }
        }
    }

    private void SaveAsPrefab(GameObject obj)
    {
        string path = Path.Combine(prefabPath, obj.name + ".prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(obj, path, InteractionMode.AutomatedAction);
    }
}

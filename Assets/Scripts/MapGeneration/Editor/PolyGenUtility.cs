using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class PolyGenUtility : EditorWindow
{
    [MenuItem("Tools/PolyGen Utilities")]
    public static void ShowWindow()
    {
        GetWindow<PolyGenUtility>("PolyGen Utilities");
    }

    private void OnGUI()
    {
        GUILayout.Label("PolyGen Mesh Utilities", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Combine Land Meshes"))
        {
            CombineMeshes("Assets/PolyGen/Land", "CombinedLand");
        }

        if (GUILayout.Button("Combine Water Meshes"))
        {
            CombineMeshes("Assets/PolyGen/Water", "CombinedWater");
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Create Land GameObject"))
        {
            CreateGameObject("Assets/PolyGen/Land", "Land", Color.green);
        }

        if (GUILayout.Button("Create Water GameObject"))
        {
            CreateGameObject("Assets/PolyGen/Water", "Water", Color.blue);
        }
    }

    private void CombineMeshes(string folderPath, string outputName)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"Folder not found: {folderPath}");
            return;
        }

        // Get all mesh assets in the folder
        string[] meshPaths = Directory.GetFiles(folderPath, "*.asset");
        List<Mesh> meshes = new List<Mesh>();

        foreach (string path in meshPaths)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                meshes.Add(mesh);
            }
        }

        if (meshes.Count == 0)
        {
            Debug.LogError($"No meshes found in {folderPath}");
            return;
        }

        // Combine meshes
        CombineInstance[] combine = new CombineInstance[meshes.Count];
        for (int i = 0; i < meshes.Count; i++)
        {
            combine[i].mesh = meshes[i];
            combine[i].transform = Matrix4x4.identity;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support larger meshes
        combinedMesh.CombineMeshes(combine, true, true);

        // Save the combined mesh
        string outputPath = $"Assets/PolyGen/{outputName}.asset";
        AssetDatabase.CreateAsset(combinedMesh, outputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Combined {meshes.Count} meshes into {outputPath}");
    }

    private void CreateGameObject(string folderPath, string objectName, Color color)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"Folder not found: {folderPath}");
            return;
        }

        // Get all mesh assets in the folder
        string[] meshPaths = Directory.GetFiles(folderPath, "*.asset");
        List<Mesh> meshes = new List<Mesh>();

        foreach (string path in meshPaths)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                meshes.Add(mesh);
            }
        }

        if (meshes.Count == 0)
        {
            Debug.LogError($"No meshes found in {folderPath}");
            return;
        }

        // Create parent game object
        GameObject parentObject = new GameObject(objectName);

        // Create child objects with meshes
        foreach (Mesh mesh in meshes)
        {
            GameObject childObject = new GameObject(mesh.name);
            childObject.transform.parent = parentObject.transform;

            // Add mesh filter and renderer
            MeshFilter meshFilter = childObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = childObject.AddComponent<MeshRenderer>();
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            meshRenderer.sharedMaterial = material;
        }

        // Select the parent object
        Selection.activeGameObject = parentObject;

        Debug.Log($"Created {objectName} GameObject with {meshes.Count} child meshes");
    }
}
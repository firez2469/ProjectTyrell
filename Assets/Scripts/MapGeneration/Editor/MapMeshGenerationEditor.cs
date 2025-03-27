using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using Unity.VisualScripting;

[CustomEditor(typeof(MapMeshGeneration))]
public class MapMeshGenerationEditor : Editor
{
    private SerializedProperty landMaterialProperty;
    private SerializedProperty seaMaterialProperty;
    private SerializedProperty regenerateMeshesProperty;
    private SerializedProperty polygonDataAssetProperty;
    private SerializedProperty tileDataAssetProperty;
    private SerializedProperty lineWidthProperty;
    private SerializedProperty worldLineColorProperty;
    private SerializedProperty highlightLandColorProperty;
    private SerializedProperty highlightSeaColorProperty;

    private bool showDebugOptions = false;

    private void OnEnable()
    {
        // Get serialized properties from MapMeshGeneration
        landMaterialProperty = serializedObject.FindProperty("landMaterial");
        seaMaterialProperty = serializedObject.FindProperty("seaMaterial");
        regenerateMeshesProperty = serializedObject.FindProperty("regenerateMeshes");
        polygonDataAssetProperty = serializedObject.FindProperty("polygonDataAsset");
        tileDataAssetProperty = serializedObject.FindProperty("tileDataAsset");
        lineWidthProperty = serializedObject.FindProperty("lineWidth");
        worldLineColorProperty = serializedObject.FindProperty("worldLineColor");
        highlightLandColorProperty = serializedObject.FindProperty("highlightLandColor");
        highlightSeaColorProperty = serializedObject.FindProperty("highlightSeaColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(landMaterialProperty, new GUIContent("Land Material", "Material to use for land tiles"));
        EditorGUILayout.PropertyField(seaMaterialProperty, new GUIContent("Sea Material", "Material to use for sea tiles"));
        EditorGUILayout.PropertyField(regenerateMeshesProperty, new GUIContent("Regenerate Meshes", "If true, will overwrite existing mesh assets"));
        EditorGUILayout.PropertyField(polygonDataAssetProperty, new GUIContent("Polygon Data Asset", "JSON TextAsset containing polygon data"));
        EditorGUILayout.PropertyField(tileDataAssetProperty, new GUIContent("Tile Data Asset", "JSON TextAsset containing tile data"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Line Renderer Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lineWidthProperty, new GUIContent("Line Width", "Width of the tile outline"));
        EditorGUILayout.PropertyField(worldLineColorProperty, new GUIContent("World Line Color", "Color of the global outline for all tiles"));
        EditorGUILayout.PropertyField(highlightLandColorProperty, new GUIContent("Highlight Land Color", "Color of the highlight for land tiles"));
        EditorGUILayout.PropertyField(highlightSeaColorProperty, new GUIContent("Highlight Sea Color", "Color of the highlight for sea tiles"));

        EditorGUILayout.HelpBox("If no Tile Data Asset is assigned, the script will try to load 'tile_data.json' from Resources folder", MessageType.Info);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);

        // Create button for generating meshes
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Meshes", GUILayout.Height(30)))
        {
            GenerateMeshes();
        }
        GUI.backgroundColor = Color.white;

        // Create button for clearing tile objects
        EditorGUILayout.Space(5);
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear Tile Objects", GUILayout.Height(25)))
        {
            ClearTileObjects();
        }
        GUI.backgroundColor = Color.white;

        // Debug section
        EditorGUILayout.Space(10);
        showDebugOptions = EditorGUILayout.Foldout(showDebugOptions, "Debug Options", true);
        if (showDebugOptions)
        {
            EditorGUI.indentLevel++;

            if (GUILayout.Button("View Polygon JSON Data", GUILayout.Height(25)))
            {
                ViewJsonData(((MapMeshGeneration)target).polygonDataAsset);
            }

            if (GUILayout.Button("View Tile JSON Data", GUILayout.Height(25)))
            {
                ViewJsonData(((MapMeshGeneration)target).tileDataAsset);
            }

            if (GUILayout.Button("Validate JSON Formats", GUILayout.Height(25)))
            {
                ValidateJsonFormats();
            }

            if (GUILayout.Button("Find Tile Data in Resources", GUILayout.Height(25)))
            {
                MapMeshGeneration mapGenerator = (MapMeshGeneration)target;
                TextAsset resourceTileData = Resources.Load<TextAsset>("tile_data");

                if (resourceTileData == null)
                {
                    EditorUtility.DisplayDialog("Info", "No 'tile_data.json' found in Resources folder.", "OK");
                }
                else
                {
                    Undo.RecordObject(mapGenerator, "Assign Tile Data Asset");
                    mapGenerator.tileDataAsset = resourceTileData;
                    EditorUtility.SetDirty(mapGenerator);
                    serializedObject.Update();
                    EditorUtility.DisplayDialog("Success", "Tile data asset found and assigned.", "OK");
                }
            }

            EditorGUI.indentLevel--;
        }
    }

    private void GenerateMeshes()
    {
        
        MapMeshGeneration mapGenerator = (MapMeshGeneration)target;
        mapGenerator.transform.localScale = Vector3.one;
        // Check if polygon data is assigned
        if (mapGenerator.polygonDataAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "Polygon Data Asset is not assigned. Please assign a TextAsset containing polygon data before generating meshes.", "OK");
            return;
        }

        // Validate the JSON format before proceeding
        if (!ValidatePolygonData(mapGenerator.polygonDataAsset))
        {
            return;
        }

        // Start recording undo
        Undo.RegisterFullObjectHierarchyUndo(mapGenerator.gameObject, "Generate Meshes");

        try
        {
            // Call the Load function
            mapGenerator.Load(mapGenerator.polygonDataAsset);

            // Mark scene as dirty
            EditorUtility.SetDirty(mapGenerator);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            Debug.Log("Mesh generation complete!");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"An error occurred during mesh generation: {e.Message}", "OK");
            Debug.LogException(e);
        }

        mapGenerator.transform.localScale = new Vector3(-12, -1, 7);
        for (var i= 0; i < mapGenerator.transform.childCount;i++)
        {
            mapGenerator.transform.GetChild(i).transform.localPosition = new Vector3(-0.5f, 0, -0.5f);
        }
        
    }

    private bool ValidatePolygonData(TextAsset polygonDataAsset)
    {
        try
        {
            // Check if the text asset is empty
            if (string.IsNullOrEmpty(polygonDataAsset.text))
            {
                EditorUtility.DisplayDialog("Error", "The polygon data asset is empty.", "OK");
                return false;
            }

            // Try to parse with Newtonsoft.Json
            try
            {
                var polygonData = JsonConvert.DeserializeObject<PolygonData>(polygonDataAsset.text);

                if (polygonData == null)
                {
                    EditorUtility.DisplayDialog("Error", "Failed to parse JSON data. The format may be incorrect.", "OK");
                    return false;
                }

                // Check for required arrays
                if (polygonData.vertices == null)
                {
                    EditorUtility.DisplayDialog("Error", "The polygon data is missing the 'vertices' array.", "OK");
                    return false;
                }

                if (polygonData.edges == null)
                {
                    EditorUtility.DisplayDialog("Error", "The polygon data is missing the 'edges' array.", "OK");
                    return false;
                }

                if (polygonData.polygons == null)
                {
                    EditorUtility.DisplayDialog("Error", "The polygon data is missing the 'polygons' array.", "OK");
                    return false;
                }

                // Check if arrays are empty
                if (polygonData.vertices.Count == 0)
                {
                    EditorUtility.DisplayDialog("Warning", "The 'vertices' array is empty.", "OK");
                }

                if (polygonData.edges.Count == 0)
                {
                    EditorUtility.DisplayDialog("Warning", "The 'edges' array is empty.", "OK");
                }

                if (polygonData.polygons.Count == 0)
                {
                    EditorUtility.DisplayDialog("Warning", "The 'polygons' array is empty.", "OK");
                }

                Debug.Log("Polygon JSON validation successful");
                return true;
            }
            catch (JsonException jsonEx)
            {
                EditorUtility.DisplayDialog("JSON Parse Error", $"The polygon JSON format is invalid: {jsonEx.Message}", "OK");
                Debug.LogException(jsonEx);
                return false;
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to validate polygon data: {e.Message}", "OK");
            Debug.LogException(e);
            return false;
        }
    }

    private bool ValidateTileData(TextAsset tileDataAsset)
    {
        if (tileDataAsset == null)
        {
            Debug.LogWarning("Tile data asset not found. This is optional, but recommended.");
            return true; // Not an error, just a warning
        }

        try
        {
            // Check if the text asset is empty
            if (string.IsNullOrEmpty(tileDataAsset.text))
            {
                EditorUtility.DisplayDialog("Warning", "The tile data asset is empty.", "OK");
                return true; // Not a critical error
            }

            // Try to parse with Newtonsoft.Json
            try
            {
                var tileDataList = JsonConvert.DeserializeObject<List<TileData>>(tileDataAsset.text);

                if (tileDataList == null)
                {
                    EditorUtility.DisplayDialog("Error", "Failed to parse tile data JSON. The format may be incorrect.", "OK");
                    return false;
                }

                Debug.Log($"Tile data JSON validation successful: {tileDataList.Count} tiles found");
                return true;
            }
            catch (JsonException jsonEx)
            {
                EditorUtility.DisplayDialog("JSON Parse Error", $"The tile data JSON format is invalid: {jsonEx.Message}", "OK");
                Debug.LogException(jsonEx);
                return false;
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to validate tile data: {e.Message}", "OK");
            Debug.LogException(e);
            return false;
        }
    }

    private void ValidateJsonFormats()
    {
        MapMeshGeneration mapGenerator = (MapMeshGeneration)target;
        bool polygonDataValid = false;
        bool tileDataValid = false;

        if (mapGenerator.polygonDataAsset != null)
        {
            polygonDataValid = ValidatePolygonData(mapGenerator.polygonDataAsset);
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "No polygon data asset assigned.", "OK");
        }

        tileDataValid = ValidateTileData(mapGenerator.tileDataAsset);

        string message = "Validation Results:\n\n";
        message += $"Polygon Data: {(polygonDataValid ? "Valid ✓" : "Invalid ✗")}\n";
        message += $"Tile Data: {(tileDataValid ? "Valid ✓" : mapGenerator.tileDataAsset == null ? "Not Found" : "Invalid ✗")}";

        EditorUtility.DisplayDialog("JSON Validation Results", message, "OK");
    }

    private void ClearTileObjects()
    {
        MapMeshGeneration mapGenerator = (MapMeshGeneration)target;

        // Find and delete existing TileMap
        Transform tileMapTransform = mapGenerator.transform.Find("TileMap");
        if (tileMapTransform != null)
        {
            Undo.RegisterFullObjectHierarchyUndo(mapGenerator.gameObject, "Clear Tile Objects");
            Undo.DestroyObjectImmediate(tileMapTransform.gameObject);

            Debug.Log("Tile objects cleared");
        }
        else
        {
            Debug.Log("No tile objects found to clear");
        }
    }

    private void ViewJsonData(TextAsset jsonAsset)
    {
        if (jsonAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "No JSON asset assigned.", "OK");
            return;
        }

        // Create a temporary window to view the JSON data
        EditorWindow window = EditorWindow.GetWindow(typeof(JsonViewerWindow), true, "JSON Data Viewer");
        var jsonViewer = window as JsonViewerWindow;
        if (jsonViewer != null)
        {
            jsonViewer.SetJsonText(jsonAsset.text);
        }
    }

    // Custom window for viewing JSON data
    public class JsonViewerWindow : EditorWindow
    {
        private string jsonText = "";
        private Vector2 scrollPosition;

        public void SetJsonText(string text)
        {
            jsonText = text;
        }

        private void OnGUI()
        {
            // Add a scroll view for the JSON text
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUIStyle style = new GUIStyle(EditorStyles.textArea);
            style.wordWrap = true;

            EditorGUILayout.LabelField("JSON Content:", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(true);
            jsonText = EditorGUILayout.TextArea(jsonText, style, GUILayout.ExpandHeight(true));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();
        }
    }
}
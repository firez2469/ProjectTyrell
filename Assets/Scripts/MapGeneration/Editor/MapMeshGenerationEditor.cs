using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using Unity.VisualScripting;

[CustomEditor(typeof(MapMeshGeneration))]
public class MapMeshGenerationEditor : Editor
{
    private SerializedProperty landMaterialProperty;
    private SerializedProperty seaMaterialProperty;
    private SerializedProperty regenerateMeshesProperty;
    private SerializedProperty combinedDataAssetProperty;
    private SerializedProperty lineWidthProperty;
    private SerializedProperty worldLineColorProperty;
    private SerializedProperty highlightLandColorProperty;
    private SerializedProperty highlightSeaColorProperty;
    private SerializedProperty biomeColorsProperty;

    private bool showDebugOptions = false;
    private bool showBiomeColors = false;

    private void OnEnable()
    {
        landMaterialProperty = serializedObject.FindProperty("landMaterial");
        seaMaterialProperty = serializedObject.FindProperty("seaMaterial");
        regenerateMeshesProperty = serializedObject.FindProperty("regenerateMeshes");
        combinedDataAssetProperty = serializedObject.FindProperty("combinedDataAsset");
        lineWidthProperty = serializedObject.FindProperty("lineWidth");
        worldLineColorProperty = serializedObject.FindProperty("worldLineColor");
        highlightLandColorProperty = serializedObject.FindProperty("highlightLandColor");
        highlightSeaColorProperty = serializedObject.FindProperty("highlightSeaColor");
        biomeColorsProperty = serializedObject.FindProperty("biomeColors");
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(landMaterialProperty, new GUIContent("Land Material", "Material to use for land tiles"));
        EditorGUILayout.PropertyField(seaMaterialProperty, new GUIContent("Sea Material", "Material to use for sea tiles"));
        EditorGUILayout.PropertyField(regenerateMeshesProperty, new GUIContent("Regenerate Meshes", "If true, will overwrite existing mesh assets"));
        EditorGUILayout.PropertyField(combinedDataAssetProperty, new GUIContent("Combined JSON Asset", "Unified JSON containing vertices, edges, and tiles"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Line Renderer Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lineWidthProperty, new GUIContent("Line Width", "Width of the tile outline"));
        EditorGUILayout.PropertyField(worldLineColorProperty, new GUIContent("World Line Color", "Color of the global outline for all tiles"));
        EditorGUILayout.PropertyField(highlightLandColorProperty, new GUIContent("Highlight Land Color", "Color of the highlight for land tiles"));
        EditorGUILayout.PropertyField(highlightSeaColorProperty, new GUIContent("Highlight Sea Color", "Color of the highlight for sea tiles"));

        // Biome color settings
        EditorGUILayout.Space(10);
        showBiomeColors = EditorGUILayout.Foldout(showBiomeColors, "Biome Color Settings", true);
        if (showBiomeColors)
        {
            EditorGUILayout.PropertyField(biomeColorsProperty, new GUIContent("Biome Colors", "Color mappings for different biome types"), true);

            EditorGUILayout.HelpBox("Define color mappings for each biome type. Any undefined biomes will use a default gray color.", MessageType.Info);

            // Button to extract biome types from JSON
            if (GUILayout.Button("Extract Biome Types from JSON", GUILayout.Height(25)))
            {
                ExtractBiomeTypesFromJson();
            }
        }

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
                ViewJsonData(((MapMeshGeneration)target).combinedDataAsset);
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

    private void ExtractBiomeTypesFromJson()
    {
        MapMeshGeneration mapGenerator = (MapMeshGeneration)target;
        if (mapGenerator.combinedDataAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "No combined data asset assigned.", "OK");
            return;
        }

        try
        {
            var data = JsonConvert.DeserializeObject<MapData.CombinedMapData>(mapGenerator.combinedDataAsset.text);
            if (data == null || data.tiles == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to parse tile data from combined file.", "OK");
                return;
            }

            HashSet<string> biomeTypes = new HashSet<string>();
            foreach (var tile in data.tiles)
            {
                if (!string.IsNullOrEmpty(tile.type))
                    biomeTypes.Add(tile.type);
            }

            if (biomeTypes.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No biomes found in combined data.", "OK");
                return;
            }

            // Get the existing biome colors
            serializedObject.Update();
            SerializedProperty biomeColorsArray = biomeColorsProperty;

            // Create map of existing biome colors
            Dictionary<string, Color> existingBiomeColors = new Dictionary<string, Color>();
            for (int i = 0; i < biomeColorsArray.arraySize; i++)
            {
                SerializedProperty biomeEntry = biomeColorsArray.GetArrayElementAtIndex(i);
                SerializedProperty biomeName = biomeEntry.FindPropertyRelative("biomeName");
                SerializedProperty biomeColor = biomeEntry.FindPropertyRelative("color");

                if (!string.IsNullOrEmpty(biomeName.stringValue))
                {
                    existingBiomeColors[biomeName.stringValue] = biomeColor.colorValue;
                }
            }

            // Resize the array to fit all biome types
            biomeColorsArray.arraySize = biomeTypes.Count;

            // Add or update biome colors
            int index = 0;

            // Define rainbow colors for new biomes
            Color[] rainbowColors = {
                new Color(1.0f, 0.0f, 0.0f), // Red
                new Color(1.0f, 0.5f, 0.0f), // Orange
                new Color(1.0f, 1.0f, 0.0f), // Yellow
                new Color(0.0f, 1.0f, 0.0f), // Green
                new Color(0.0f, 1.0f, 1.0f), // Cyan
                new Color(0.0f, 0.0f, 1.0f), // Blue
                new Color(0.5f, 0.0f, 0.5f)  // Purple
            };

            foreach (string biomeType in biomeTypes.OrderBy(b => b))
            {
                SerializedProperty biomeEntry = biomeColorsArray.GetArrayElementAtIndex(index);
                SerializedProperty biomeName = biomeEntry.FindPropertyRelative("biomeName");
                SerializedProperty biomeColor = biomeEntry.FindPropertyRelative("color");

                biomeName.stringValue = biomeType;

                // Use existing color if available, otherwise assign a new color
                if (existingBiomeColors.TryGetValue(biomeType, out Color existingColor))
                {
                    biomeColor.colorValue = existingColor;
                }
                else
                {
                    // Assign a color from the rainbow palette based on the index
                    biomeColor.colorValue = rainbowColors[index % rainbowColors.Length];
                }

                index++;
            }

            serializedObject.ApplyModifiedProperties();

            EditorUtility.DisplayDialog("Success", $"Extracted {biomeTypes.Count} unique biome types from tile data.", "OK");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to extract biomes: {e.Message}", "OK");
            Debug.LogException(e);
        }
    }

    private void GenerateMeshes()
    {

        MapMeshGeneration mapGenerator = (MapMeshGeneration)target;
        mapGenerator.transform.localScale = Vector3.one;
        // Check if polygon data is assigned
        if (mapGenerator.combinedDataAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "Polygon Data Asset is not assigned. Please assign a TextAsset containing polygon data before generating meshes.", "OK");
            return;
        }

        // Validate the JSON format before proceeding
        if (!ValidateCombinedData(mapGenerator.combinedDataAsset))
        {
            return;
        }

        // Start recording undo
        Undo.RegisterFullObjectHierarchyUndo(mapGenerator.gameObject, "Generate Meshes");

        try
        {
            // Call the Load function
            mapGenerator.Load(mapGenerator.combinedDataAsset);

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
        for (var i = 0; i < mapGenerator.transform.childCount; i++)
        {
            mapGenerator.transform.GetChild(i).transform.localPosition = new Vector3(-0.5f, 0, -0.5f);
        }

    }
    private bool ValidateCombinedData(TextAsset combinedAsset)
    {
        try
        {
            if (string.IsNullOrEmpty(combinedAsset.text))
            {
                EditorUtility.DisplayDialog("Error", "Combined JSON asset is empty.", "OK");
                return false;
            }

            var data = JsonConvert.DeserializeObject<MapData.CombinedMapData>(combinedAsset.text);
            if (data == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to parse combined JSON data.", "OK");
                return false;
            }

            if (data.vertices == null || data.edges == null || data.tiles == null)
            {
                EditorUtility.DisplayDialog("Error", "Missing one or more required fields (vertices, edges, tiles).", "OK");
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("JSON Parse Error", $"Failed to validate combined JSON: {e.Message}", "OK");
            return false;
        }
    }

    private void ValidateJsonFormats()
    {
        MapMeshGeneration mapGen = (MapMeshGeneration)target;
        bool valid = ValidateCombinedData(mapGen.combinedDataAsset);

        string msg = valid ? "Combined JSON is valid ✓" : "Combined JSON is invalid ✗";
        EditorUtility.DisplayDialog("Validation Result", msg, "OK");
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
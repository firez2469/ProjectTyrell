using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

[CustomEditor(typeof(PolyGen))]
public class PolyGenEditor : Editor
{
    private string resourcesFolderPath = "PolyGen";
    private Material landMaterial;
    private Material waterMaterial;

    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();

        // Get the target
        PolyGen polyGen = (PolyGen)target;

        // Add space for separation
        EditorGUILayout.Space(10);

        // Add a "Generate" button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Generate", GUILayout.Height(30), GUILayout.Width(120)))
        {
            // Call the Generate method
            GeneratePolygons(polyGen);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // Add a section for mesh placement
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Mesh Placement Settings", EditorStyles.boldLabel);

        // Path within Resources folder
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Resources Path");
        resourcesFolderPath = EditorGUILayout.TextField(resourcesFolderPath);
        EditorGUILayout.EndHorizontal();

        // Materials
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Land Material");
        landMaterial = (Material)EditorGUILayout.ObjectField(landMaterial, typeof(Material), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Water Material");
        waterMaterial = (Material)EditorGUILayout.ObjectField(waterMaterial, typeof(Material), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Add Place button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Place Meshes", GUILayout.Height(30), GUILayout.Width(120)))
        {
            PlaceMeshesInScene(polyGen);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void GeneratePolygons(PolyGen polyGen)
    {
        // Call the diagram generation method using reflection (since it's private)
        System.Reflection.MethodInfo generateMethod = typeof(PolyGen).GetMethod("GenerateVoronoiDiagram",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (generateMethod != null)
        {
            generateMethod.Invoke(polyGen, null);

            // Call the classification method if a texture is assigned
            if (polyGen.GetType().GetField("mapTexture", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).GetValue(polyGen) != null)
            {
                System.Reflection.MethodInfo classifyMethod = typeof(PolyGen).GetMethod("ClassifyLandCells",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (classifyMethod != null)
                {
                    classifyMethod.Invoke(polyGen, null);
                }
            }

            // Create meshes for each polygon
            CreateMeshes(polyGen);
        }
        else
        {
            Debug.LogError("Could not find GenerateVoronoiDiagram method");
        }
    }

    private void CreateMeshes(PolyGen polyGen)
    {
        // Get the necessary data from PolyGen
        Vector2[] sites = (Vector2[])polyGen.GetType().GetField("sites",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(polyGen);

        List<List<int>> cellVertexIndices = (List<List<int>>)polyGen.GetType().GetField("cellVertexIndices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(polyGen);

        Vector2[] vertices = (Vector2[])polyGen.GetType().GetField("vertices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(polyGen);

        bool[] isLand = (bool[])polyGen.GetType().GetField("isLand",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(polyGen);

        if (sites == null || cellVertexIndices == null || vertices == null)
        {
            Debug.LogError("Voronoi diagram data is missing. Please generate the diagram first.");
            return;
        }

        // Create folders for meshes in Resources
        string basePath = "Assets/Resources";
        if (!string.IsNullOrEmpty(resourcesFolderPath))
        {
            basePath = Path.Combine(basePath, resourcesFolderPath);
        }

        string landFolderPath = Path.Combine(basePath, "Land");
        string waterFolderPath = Path.Combine(basePath, "Water");

        // Create directories if they don't exist
        if (!Directory.Exists(landFolderPath))
        {
            Directory.CreateDirectory(landFolderPath);
        }

        if (!Directory.Exists(waterFolderPath))
        {
            Directory.CreateDirectory(waterFolderPath);
        }

        // Save vertex data as a JSON file for future reference
        Vector3[] worldVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            worldVertices[i] = new Vector3(vertices[i].x, 0, vertices[i].y);
        }
        string vertexDataPath = Path.Combine(basePath, "VertexData.json");
        string vertexJson = JsonUtility.ToJson(new VertexData { vertices = worldVertices });
        File.WriteAllText(vertexDataPath, vertexJson);

        // Save site data as JSON for future reference
        Vector3[] worldSites = new Vector3[sites.Length];
        for (int i = 0; i < sites.Length; i++)
        {
            worldSites[i] = new Vector3(sites[i].x, 0, sites[i].y);
        }
        string siteDataPath = Path.Combine(basePath, "SiteData.json");
        string siteJson = JsonUtility.ToJson(new SiteData
        {
            sites = worldSites,
            isLand = isLand
        });
        File.WriteAllText(siteDataPath, siteJson);

        // Save cell data for future reference
        List<int[]> cellIndicesArray = new List<int[]>();
        foreach (var cell in cellVertexIndices)
        {
            cellIndicesArray.Add(cell.ToArray());
        }
        string cellDataPath = Path.Combine(basePath, "CellData.json");
        string cellJson = JsonUtility.ToJson(new CellData { cells = cellIndicesArray.ToArray() });
        File.WriteAllText(cellDataPath, cellJson);

        // AssetDatabase operations
        AssetDatabase.StartAssetEditing();

        try
        {
            // Process each cell
            for (int cellIndex = 0; cellIndex < cellVertexIndices.Count; cellIndex++)
            {
                if (cellIndex >= sites.Length || cellVertexIndices[cellIndex].Count < 3)
                    continue;

                bool isLandCell = isLand != null && cellIndex < isLand.Length ? isLand[cellIndex] : false;
                string folderPath = isLandCell ? landFolderPath : waterFolderPath;

                // Create mesh for this cell
                Mesh mesh = CreatePolygonMesh(cellVertexIndices[cellIndex], vertices, sites[cellIndex]);

                if (mesh != null)
                {
                    // Save the mesh asset
                    string meshPath = $"{folderPath}/Cell_{cellIndex}.asset";
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        // Get the relative path for Resources.Load
        string relativeLandPath = GetResourcesRelativePath(landFolderPath);
        string relativeWaterPath = GetResourcesRelativePath(waterFolderPath);

        Debug.Log($"Mesh generation complete. Meshes saved to Resources/{relativeLandPath} and Resources/{relativeWaterPath}");
    }

    private string GetResourcesRelativePath(string fullPath)
    {
        string resourcesPath = "Resources/";
        int index = fullPath.IndexOf(resourcesPath);
        if (index >= 0)
        {
            return fullPath.Substring(index + resourcesPath.Length);
        }
        return fullPath;
    }

    private Mesh CreatePolygonMesh(List<int> cellIndices, Vector2[] allVertices, Vector2 site)
    {
        if (cellIndices.Count < 3)
            return null;

        Mesh mesh = new Mesh();

        // Create the 3D vertices (using Y as up)
        Vector3[] meshVertices = new Vector3[cellIndices.Count + 1]; // +1 for center point

        // Center point is the site location
        meshVertices[0] = new Vector3(site.x, 0, site.y);

        // Perimeter vertices
        for (int i = 0; i < cellIndices.Count; i++)
        {
            int vertIndex = cellIndices[i];
            if (vertIndex < allVertices.Length)
            {
                Vector2 v = allVertices[vertIndex];
                meshVertices[i + 1] = new Vector3(v.x, 0, v.y);
            }
        }

        // Create triangles (fan triangulation from center)
        int[] triangles = new int[(cellIndices.Count) * 3];
        for (int i = 0; i < cellIndices.Count; i++)
        {
            triangles[i * 3] = 0; // Center
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % cellIndices.Count + 1;
        }

        // Set mesh data
        mesh.vertices = meshVertices;
        mesh.triangles = triangles;

        // Calculate UVs based on world position
        Vector2[] uvs = new Vector2[meshVertices.Length];
        for (int i = 0; i < meshVertices.Length; i++)
        {
            uvs[i] = new Vector2(meshVertices[i].x, meshVertices[i].z);
        }
        mesh.uv = uvs;

        // Calculate normals, bounds, etc.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void PlaceMeshesInScene(PolyGen polyGen)
    {
        // Check if the Resources path exists
        string relativePath = resourcesFolderPath;

        // Create parent game objects for organization
        GameObject landParent = new GameObject("Land_Tiles");
        GameObject waterParent = new GameObject("Water_Tiles");

        // Get data required for placement
        bool[] isLand = null;

        // Try to load site data from JSON
        TextAsset siteDataJson = Resources.Load<TextAsset>(Path.Combine(relativePath, "SiteData"));
        if (siteDataJson != null)
        {
            SiteData siteData = JsonUtility.FromJson<SiteData>(siteDataJson.text);
            isLand = siteData.isLand;
        }
        else
        {
            // Fall back to getting isLand from PolyGen
            isLand = (bool[])polyGen.GetType().GetField("isLand",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(polyGen);
        }

        // Log diagnostic information
        Debug.Log($"Attempting to load meshes from Resources/{relativePath}");
        if (isLand != null)
        {
            int landCount = 0;
            for (int i = 0; i < isLand.Length; i++)
            {
                if (isLand[i]) landCount++;
            }
            Debug.Log($"Land classification: {landCount} land tiles, {isLand.Length - landCount} water tiles");
        }

        // First try loading land meshes
        string landPath = Path.Combine(relativePath, "Land");
        bool landLoaded = LoadAndPlaceMeshes(landPath, landParent, true, isLand, landMaterial);

        // Then try loading water meshes
        string waterPath = Path.Combine(relativePath, "Water");
        bool waterLoaded = LoadAndPlaceMeshes(waterPath, waterParent, false, isLand, waterMaterial);

        // Report counts for diagnostics
        Debug.Log($"Placed {landParent.transform.childCount} land tiles and {waterParent.transform.childCount} water tiles");

        if (!landLoaded && !waterLoaded)
        {
            Debug.LogError($"No meshes found in Resources/{relativePath}/Land or Resources/{relativePath}/Water");
            DestroyImmediate(landParent);
            DestroyImmediate(waterParent);
            return;
        }

        // Clean up empty parents
        if (landParent.transform.childCount == 0)
        {
            DestroyImmediate(landParent);
        }

        if (waterParent.transform.childCount == 0)
        {
            DestroyImmediate(waterParent);
        }

        Debug.Log("Placed meshes in the scene");
    }

    private bool LoadAndPlaceMeshes(string resourcePath, GameObject parent, bool isLandType, bool[] isLandArray, Material material)
    {
        bool anyLoaded = false;

        // First try to load all meshes directly from the folder using LoadAll
        Object[] allMeshes = Resources.LoadAll(resourcePath, typeof(Mesh));

        if (allMeshes != null && allMeshes.Length > 0)
        {
            Debug.Log($"Found {allMeshes.Length} meshes in {resourcePath}");
            foreach (Object obj in allMeshes)
            {
                Mesh m = obj as Mesh;
                if (m != null)
                {
                    CreateTileGameObject(m, parent, isLandType, material);
                    anyLoaded = true;
                }
            }
            return anyLoaded;
        }

        // Fallback: Try loading meshes one by one with increasing index
        // This can help if Resources.LoadAll has issues with large numbers of assets
        int meshIndex = 0;
        int maxAttempts = 2000; // Set a reasonable upper limit to prevent infinite loops
        int consecutiveFailures = 0;

        while (meshIndex < maxAttempts)
        {
            string meshName = $"Cell_{meshIndex}";
            Mesh mesh = null;

            // Try different path formats that Unity might use
            string[] pathFormats = new string[] {
                Path.Combine(resourcePath, meshName),
                resourcePath + "/" + meshName,
                resourcePath + "." + meshName
            };

            foreach (string path in pathFormats)
            {
                mesh = Resources.Load<Mesh>(path);
                if (mesh != null) break;
            }

            if (mesh == null)
            {
                consecutiveFailures++;
                // If we've had 10 consecutive failures after finding at least one mesh, we're probably done
                if (anyLoaded && consecutiveFailures > 10)
                {
                    break;
                }
            }
            else
            {
                CreateTileGameObject(mesh, parent, isLandType, material);
                anyLoaded = true;
                consecutiveFailures = 0;
            }

            meshIndex++;
        }

        // Final fallback - look in the parent Resources folder for misplaced mesh assets
        if (!anyLoaded)
        {
            string parentPath = resourcePath.Substring(0, resourcePath.LastIndexOf('/'));
            Object[] parentMeshes = Resources.LoadAll(parentPath, typeof(Mesh));

            foreach (Object obj in parentMeshes)
            {
                Mesh m = obj as Mesh;
                if (m != null && m.name.StartsWith("Cell_"))
                {
                    CreateTileGameObject(m, parent, isLandType, material);
                    anyLoaded = true;
                }
            }
        }

        return anyLoaded;
    }

    private void CreateTileGameObject(Mesh mesh, GameObject parent, bool isLandType, Material material)
    {
        GameObject tileObject = new GameObject(mesh.name);
        tileObject.transform.parent = parent.transform;

        // Add mesh filter and renderer
        MeshFilter meshFilter = tileObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = tileObject.AddComponent<MeshRenderer>();

        // Use the provided material if available, otherwise create a basic one
        if (material != null)
        {
            meshRenderer.sharedMaterial = material;
        }
        else
        {
            Material newMaterial = new Material(Shader.Find("Standard"));
            newMaterial.color = isLandType ? Color.green : Color.blue;
            meshRenderer.sharedMaterial = newMaterial;
        }
    }
}

// Helper classes for JSON serialization
[System.Serializable]
public class VertexData
{
    public Vector3[] vertices;
}

[System.Serializable]
public class SiteData
{
    public Vector3[] sites;
    public bool[] isLand;
}

[System.Serializable]
public class CellData
{
    public int[][] cells;
}
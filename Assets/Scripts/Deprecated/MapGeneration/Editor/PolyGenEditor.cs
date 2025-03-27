using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PolyGen))]
public class PolyGenEditor : Editor
{
    private string resourcesFolderPath = "PolyGen";
    private Material landMaterial;
    private Material waterMaterial;

    // Added properties for edge highlighting
    private bool addEdgeHighlights = true;
    private Color landEdgeColor = Color.green;
    private Color waterEdgeColor = Color.blue;
    private Color disabledEdgeColor = Color.grey;
    private float edgeLineWidth = 0.05f;
    private float edgeHeight = 0.05f;

    // List to hold all created tiles for neighbor processing
    private List<Tile> tilesCreated = new List<Tile>();
    private float neighborDetectionRadius = 1.0f;

    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();

        // Add space for separation
        EditorGUILayout.Space(10);

        // Add a "Generate" button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Generate", GUILayout.Height(30), GUILayout.Width(120)))
        {
            // Call the Generate method
            PolyGen polyGen = (PolyGen)target;
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

        // Edge Highlight Settings
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Edge Highlight Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Add Edge Highlights");
        addEdgeHighlights = EditorGUILayout.Toggle(addEdgeHighlights);
        EditorGUILayout.EndHorizontal();

        if (addEdgeHighlights)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Land Edge Color");
            landEdgeColor = EditorGUILayout.ColorField(landEdgeColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Water Edge Color");
            waterEdgeColor = EditorGUILayout.ColorField(waterEdgeColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Disabled Edge Color");
            disabledEdgeColor = EditorGUILayout.ColorField(disabledEdgeColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Edge Line Width");
            edgeLineWidth = EditorGUILayout.Slider(edgeLineWidth, 0.01f, 0.2f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Edge Height");
            edgeHeight = EditorGUILayout.Slider(edgeHeight, 0.01f, 0.2f);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        // Add Place button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Place Meshes", GUILayout.Height(30), GUILayout.Width(120)))
        {
            PolyGen polyGen = (PolyGen)target;
            PlaceMeshesInScene(polyGen);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Neighbor Radius");
        neighborDetectionRadius = EditorGUILayout.FloatField(neighborDetectionRadius);
        EditorGUILayout.EndHorizontal();

        // Add the new Fix Polygon Gaps button section
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Fix Polygon Gaps", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Fix Polygon Gaps", GUILayout.Height(30), GUILayout.Width(150)))
        {
            EnsureSharedEdges();
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

            // Process vertices to ensure they align properly
            ProcessVoronoiVertices(polyGen);

            // Create meshes for each polygon
            CreateMeshes(polyGen);
        }
        else
        {
            Debug.LogError("Could not find GenerateVoronoiDiagram method");
        }
    }

    // New method to process Voronoi vertices before mesh generation
    private void ProcessVoronoiVertices(PolyGen polyGen)
    {
        // Get the vertices from PolyGen
        Vector2[] vertices = (Vector2[])polyGen.GetType().GetField("vertices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(polyGen);

        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogError("No vertices found to process");
            return;
        }

        // Get the cell vertex indices
        List<List<int>> cellVertexIndices = (List<List<int>>)polyGen.GetType().GetField("cellVertexIndices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(polyGen);

        if (cellVertexIndices == null)
        {
            Debug.LogError("No cell vertex indices found");
            return;
        }

        Debug.Log($"Processing {vertices.Length} vertices for {cellVertexIndices.Count} cells");

        // Create a grid for snapping vertices
        float snapFactor = 0.0001f;
        Dictionary<Vector2, Vector2> snappedVertices = new Dictionary<Vector2, Vector2>();
        Dictionary<int, int> vertexRemapping = new Dictionary<int, int>();

        // First pass: collect all vertices and snap them to a grid
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 v = vertices[i];
            Vector2 snappedV = new Vector2(
                Mathf.Round(v.x / snapFactor) * snapFactor,
                Mathf.Round(v.y / snapFactor) * snapFactor
            );

            // Store the snapped version
            if (!snappedVertices.ContainsKey(v))
            {
                snappedVertices.Add(v, snappedV);
            }
        }

        // Apply snapped values back to the original array
        for (int i = 0; i < vertices.Length; i++)
        {
            if (snappedVertices.TryGetValue(vertices[i], out Vector2 snappedV))
            {
                vertices[i] = snappedV;
            }
        }

        // Set the updated vertices back to PolyGen
        polyGen.GetType().GetField("vertices",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(polyGen, vertices);

        Debug.Log("Vertex processing complete");
    }
    // Add a helper method to clean up edges and ensure they connect
    private void CleanUpEdges(List<Tile> tiles)
    {
        if (tiles.Count <= 1) return;

        Debug.Log("Cleaning up edges to ensure proper connections...");

        // Map to store vertex positions and their corresponding tiles
        Dictionary<Vector3, List<Tile>> vertexToTiles = new Dictionary<Vector3, List<Tile>>();
        Dictionary<Vector3, Vector3> snappedVertexMap = new Dictionary<Vector3, Vector3>();

        float snapThreshold = 0.01f; // Threshold for considering vertices the same

        // First pass: collect all vertices from all tiles
        foreach (Tile tile in tiles)
        {
            MeshFilter mf = tile.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Vector3[] verts = mf.sharedMesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 worldVert = tile.transform.TransformPoint(verts[i]);

                // Skip center vertex (usually at index 0)
                if (i == 0) continue;

                // Check if this vertex is close to any existing one
                bool foundMatch = false;
                foreach (var key in vertexToTiles.Keys)
                {
                    if (Vector3.Distance(worldVert, key) < snapThreshold)
                    {
                        // Store mapping from this vertex to the snapped one
                        snappedVertexMap[worldVert] = key;

                        // Add this tile to the list for this vertex
                        vertexToTiles[key].Add(tile);
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    // Create new entry
                    vertexToTiles[worldVert] = new List<Tile> { tile };
                }
            }
        }

        // Second pass: update vertices to ensure they perfectly match
        foreach (Tile tile in tiles)
        {
            MeshFilter mf = tile.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Vector3[] verts = mf.sharedMesh.vertices;
            bool meshModified = false;

            for (int i = 1; i < verts.Length; i++) // Skip center vertex
            {
                Vector3 worldVert = tile.transform.TransformPoint(verts[i]);

                // Check if this vertex should be snapped
                if (snappedVertexMap.TryGetValue(worldVert, out Vector3 snappedVert))
                {
                    // Convert snapped world vertex back to local space
                    Vector3 localSnapped = tile.transform.InverseTransformPoint(snappedVert);

                    if (Vector3.Distance(verts[i], localSnapped) > 0.0001f)
                    {
                        verts[i] = localSnapped;
                        meshModified = true;
                    }
                }
            }

            // Apply changes if needed
            if (meshModified)
            {
                Mesh newMesh = Instantiate(mf.sharedMesh);
                newMesh.vertices = verts;
                newMesh.RecalculateBounds();
                mf.sharedMesh = newMesh;
            }
        }

        Debug.Log("Edge cleanup complete");
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
        string positionsPath = Path.Combine(basePath, "Positions");
        string edgesPath = Path.Combine(basePath, "Edges");

        // Create directories if they don't exist
        if (!Directory.Exists(landFolderPath))
        {
            Directory.CreateDirectory(landFolderPath);
        }

        if (!Directory.Exists(waterFolderPath))
        {
            Directory.CreateDirectory(waterFolderPath);
        }

        if (!Directory.Exists(positionsPath))
        {
            Directory.CreateDirectory(positionsPath);
        }

        if (!Directory.Exists(edgesPath))
        {
            Directory.CreateDirectory(edgesPath);
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

        // Save individual position data for each cell
        for (int cellIndex = 0; cellIndex < cellVertexIndices.Count; cellIndex++)
        {
            if (cellIndex >= sites.Length || cellVertexIndices[cellIndex].Count < 3)
                continue;

            // Create edge data for this cell
            List<Vector3[]> edgesList = new List<Vector3[]>();
            Vector2 center = sites[cellIndex];
            List<int> cellIndices = cellVertexIndices[cellIndex];

            if (cellIndices.Count >= 3)
            {
                // Create edge data from center to each perimeter vertex
                for (int i = 0; i < cellIndices.Count; i++)
                {
                    int vertIndex = cellIndices[i];
                    if (vertIndex < vertices.Length)
                    {
                        Vector2 v = vertices[vertIndex];
                        Vector3 edgeStart = new Vector3(center.x, 0, center.y);
                        Vector3 edgeEnd = new Vector3(v.x, 0, v.y);
                        edgesList.Add(new Vector3[] { edgeStart, edgeEnd });
                    }
                }

                // Also add edges along the perimeter
                for (int i = 0; i < cellIndices.Count; i++)
                {
                    int currentVertIndex = cellIndices[i];
                    int nextVertIndex = cellIndices[(i + 1) % cellIndices.Count];

                    if (currentVertIndex < vertices.Length && nextVertIndex < vertices.Length)
                    {
                        Vector2 currentV = vertices[currentVertIndex];
                        Vector2 nextV = vertices[nextVertIndex];

                        Vector3 edgeStart = new Vector3(currentV.x, 0, currentV.y);
                        Vector3 edgeEnd = new Vector3(nextV.x, 0, nextV.y);

                        edgesList.Add(new Vector3[] { edgeStart, edgeEnd });
                    }
                }
            }

            // Save edge data - but only if we have edges
            if (edgesList.Count > 0)
            {
                EdgeData edgeData = new EdgeData { edges = edgesList.ToArray() };
                string edgeDataPath = Path.Combine(edgesPath, $"Edges_{cellIndex}.json");
                string edgeJson = JsonUtility.ToJson(edgeData);
                File.WriteAllText(edgeDataPath, edgeJson);

                // Debug info
                Debug.Log($"Generated {edgesList.Count} edges for cell {cellIndex}");
            }
            else
            {
                Debug.LogWarning($"No edges generated for cell {cellIndex}");
            }

            CellPosition cellPosition = new CellPosition
            {
                position = new Vector3(sites[cellIndex].x, 0, sites[cellIndex].y),
                isLand = isLand != null && cellIndex < isLand.Length ? isLand[cellIndex] : false
            };

            string positionPath = Path.Combine(positionsPath, $"Position_{cellIndex}.json");
            string positionJson = JsonUtility.ToJson(cellPosition);
            File.WriteAllText(positionPath, positionJson);
        }

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

                // Create mesh for this cell (centered)
                Mesh mesh = CreateCenteredPolygonMesh(cellVertexIndices[cellIndex], vertices, sites[cellIndex]);

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

    // Modify this method in your PolyGenEditor.cs
    private Mesh CreateCenteredPolygonMesh(List<int> cellIndices, Vector2[] allVertices, Vector2 site)
    {
        if (cellIndices.Count < 3)
            return null;

        Mesh mesh = new Mesh();

        // Calculate center of the cell
        Vector2 center = site;

        // Create the 3D vertices (using Y as up)
        Vector3[] meshVertices = new Vector3[cellIndices.Count + 1]; // +1 for center point

        // Center point is at (0,0,0) now
        meshVertices[0] = Vector3.zero;

        // Perimeter vertices (translated to be centered at origin)
        for (int i = 0; i < cellIndices.Count; i++)
        {
            int vertIndex = cellIndices[i];
            if (vertIndex < allVertices.Length)
            {
                Vector2 v = allVertices[vertIndex];
                // Subtract center to make mesh centered at origin
                // Important: We're using a small epsilon to snap vertices to a grid
                // This helps ensure adjacent polygons share exact vertex positions
                float snapFactor = 0.0001f; // Adjust based on your scale
                float xSnapped = Mathf.Round((v.x - center.x) / snapFactor) * snapFactor;
                float ySnapped = Mathf.Round((v.y - center.y) / snapFactor) * snapFactor;
                meshVertices[i + 1] = new Vector3(xSnapped, 0, ySnapped);
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

        // Calculate UVs based on relative position to keep texturing consistent
        Vector2[] uvs = new Vector2[meshVertices.Length];
        for (int i = 0; i < meshVertices.Length; i++)
        {
            // Add center back to get original world position for UV calculation
            uvs[i] = new Vector2(meshVertices[i].x + center.x, meshVertices[i].z + center.y);
        }
        mesh.uv = uvs;

        // Calculate normals, bounds, etc.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void ProcessTileNeighbors()
    {
        // First make sure we have enough tiles to process
        if (tilesCreated.Count <= 1)
        {
            Debug.LogWarning("Not enough tiles to process neighbors");
            return;
        }

        Debug.Log($"Processing neighbors for {tilesCreated.Count} tiles based on shared edges");

        // Cleanup Check before run
        for (int i = 0; i < tilesCreated.Count; i++)
        {
            Tile currentTile = tilesCreated[i];

            if (currentTile.Id == null || currentTile.Id == "")
            {
                currentTile.Id = currentTile.gameObject.name;
                currentTile.Name = currentTile.gameObject.name;
            }
            currentTile.neighbors = new List<string>();
        }

        // Debug: Print a sample tile's vertices
        if (tilesCreated.Count > 0)
        {
            Tile sampleTile = tilesCreated[0];
            MeshFilter meshFilter = sampleTile.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Vector3[] verts = meshFilter.sharedMesh.vertices;
                Debug.Log($"Sample tile {sampleTile.name} has {verts.Length} vertices");
                for (int i = 0; i < Mathf.Min(5, verts.Length); i++)
                {
                    Vector3 worldVert = sampleTile.transform.TransformPoint(verts[i]);
                    Debug.Log($"  Vertex {i}: Local={verts[i]}, World={worldVert}");
                }
            }
        }

        // Use a different approach - check for edge overlap instead of vertex sharing
        for (int i = 0; i < tilesCreated.Count; i++)
        {
            Tile currentTile = tilesCreated[i];
            MeshFilter currentMeshFilter = currentTile.GetComponent<MeshFilter>();

            if (currentMeshFilter == null || currentMeshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"Tile {currentTile.name} has no mesh!");
                continue;
            }

            Vector3[] currentVerts = currentMeshFilter.sharedMesh.vertices;

            // Get perimeter edges (skip center vertex which is at index 0)
            List<Edge> currentEdges = new List<Edge>();
            for (int v = 1; v < currentVerts.Length; v++)
            {
                int nextV = (v - 1 + currentVerts.Length) % currentVerts.Length;
                if (nextV == 0) nextV = currentVerts.Length - 1; // Skip center

                Vector3 worldV1 = currentTile.transform.TransformPoint(currentVerts[v]);
                Vector3 worldV2 = currentTile.transform.TransformPoint(currentVerts[nextV]);

                // Skip if the vertices are too close (likely duplicates)
                if (Vector3.Distance(worldV1, worldV2) > 0.01f)
                {
                    currentEdges.Add(new Edge(worldV1, worldV2));
                }
            }

            Debug.Log($"Tile {currentTile.name} has {currentEdges.Count} edges");

            // Check each other tile for overlapping edges
            for (int j = 0; j < tilesCreated.Count; j++)
            {
                if (i == j) continue; // Skip self

                Tile otherTile = tilesCreated[j];
                MeshFilter otherMeshFilter = otherTile.GetComponent<MeshFilter>();

                if (otherMeshFilter == null || otherMeshFilter.sharedMesh == null)
                    continue;

                Vector3[] otherVerts = otherMeshFilter.sharedMesh.vertices;

                // Get perimeter edges (skip center vertex which is at index 0)
                List<Edge> otherEdges = new List<Edge>();
                for (int v = 1; v < otherVerts.Length; v++)
                {
                    int nextV = (v - 1 + otherVerts.Length) % otherVerts.Length;
                    if (nextV == 0) nextV = otherVerts.Length - 1; // Skip center

                    Vector3 worldV1 = otherTile.transform.TransformPoint(otherVerts[v]);
                    Vector3 worldV2 = otherTile.transform.TransformPoint(otherVerts[nextV]);

                    // Skip if the vertices are too close (likely duplicates)
                    if (Vector3.Distance(worldV1, worldV2) > 0.01f)
                    {
                        otherEdges.Add(new Edge(worldV1, worldV2));
                    }
                }

                // Check if any edges overlap
                bool areNeighbors = false;
                foreach (Edge currentEdge in currentEdges)
                {
                    foreach (Edge otherEdge in otherEdges)
                    {
                        if (EdgesOverlap(currentEdge, otherEdge, 0.1f))
                        {
                            areNeighbors = true;
                            Debug.Log($"Found overlapping edge between {currentTile.name} and {otherTile.name}");
                            break;
                        }
                    }
                    if (areNeighbors) break;
                }

                if (areNeighbors)
                {
                    currentTile.neighbors.Add(otherTile.Id);
                }
            }
        }

        // Log neighbor statistics
        int totalNeighbors = 0;
        int maxNeighbors = 0;
        int tilesWithNoNeighbors = 0;

        foreach (Tile tile in tilesCreated)
        {
            int neighborCount = tile.neighbors.Count;
            totalNeighbors += neighborCount;
            maxNeighbors = Mathf.Max(maxNeighbors, neighborCount);

            if (neighborCount == 0)
            {
                tilesWithNoNeighbors++;
                Debug.LogWarning($"Tile {tile.name} has no neighbors!");
            }
        }

        float avgNeighbors = tilesCreated.Count > 0 ? (float)totalNeighbors / tilesCreated.Count : 0;
        Debug.Log($"Neighbor statistics: Avg: {avgNeighbors:F1}, Max: {maxNeighbors}, Tiles with no neighbors: {tilesWithNoNeighbors}");
    }

    // Helper class to represent an edge
    private class Edge
    {
        public Vector3 v1;
        public Vector3 v2;

        public Edge(Vector3 vertex1, Vector3 vertex2)
        {
            v1 = vertex1;
            v2 = vertex2;
        }
    }

    // Update this helper method in your PolyGenEditor.cs
    private bool EdgesOverlap(Edge edge1, Edge edge2, float tolerance)
    {
        // Increase tolerance for better edge detection
        float edgeTolerance = tolerance;

        // Check if the edges are approximately the same (in either direction)
        bool sameDirectOrder = (Vector3.Distance(edge1.v1, edge2.v1) < edgeTolerance &&
                               Vector3.Distance(edge1.v2, edge2.v2) < edgeTolerance);

        bool reverseOrder = (Vector3.Distance(edge1.v1, edge2.v2) < edgeTolerance &&
                            Vector3.Distance(edge1.v2, edge2.v1) < edgeTolerance);

        // Also check if the edges are partially overlapping
        // This helps with cases where vertex snapping might not be perfect
        if (!sameDirectOrder && !reverseOrder)
        {
            // Calculate edge vectors
            Vector3 dir1 = (edge1.v2 - edge1.v1).normalized;
            Vector3 dir2 = (edge2.v2 - edge2.v1).normalized;

            // If edges are roughly parallel
            if (Vector3.Dot(dir1, dir2) > 0.99f || Vector3.Dot(dir1, dir2) < -0.99f)
            {
                // Project endpoints of edge2 onto edge1
                float proj11 = Vector3.Dot(edge2.v1 - edge1.v1, dir1);
                float proj12 = Vector3.Dot(edge2.v2 - edge1.v1, dir1);

                // Length of edge1
                float len1 = Vector3.Distance(edge1.v1, edge1.v2);

                // Check if projections overlap with edge1
                bool overlap = (proj11 >= 0 && proj11 <= len1) ||
                               (proj12 >= 0 && proj12 <= len1) ||
                               (proj11 <= 0 && proj12 >= len1);

                if (overlap)
                {
                    // Check distance between edges
                    Vector3 perp = Vector3.Cross(dir1, Vector3.up).normalized;
                    float dist = Mathf.Abs(Vector3.Dot(edge2.v1 - edge1.v1, perp));

                    return dist < edgeTolerance;
                }
            }
        }

        return sameDirectOrder || reverseOrder;
    }

    // Add this method to process all edges in the scene to ensure they're connected
    private void SnapAllVerticesToGrid()
    {
        Debug.Log("Snapping all vertices to grid to ensure edge connectivity...");

        float snapFactor = 0.0001f; // Same as in CreateCenteredPolygonMesh
        Dictionary<Vector3, Vector3> snappedVerticesMap = new Dictionary<Vector3, Vector3>();

        // First pass - collect all vertices and create a mapping to snapped positions
        foreach (Tile tile in tilesCreated)
        {
            MeshFilter meshFilter = tile.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;

            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                // Transform to world space
                Vector3 worldPos = tile.transform.TransformPoint(vertices[i]);

                // Snap to grid
                Vector3 snappedPos = new Vector3(
                    Mathf.Round(worldPos.x / snapFactor) * snapFactor,
                    worldPos.y,
                    Mathf.Round(worldPos.z / snapFactor) * snapFactor
                );

                // Store in dictionary
                if (!snappedVerticesMap.ContainsKey(worldPos))
                {
                    snappedVerticesMap.Add(worldPos, snappedPos);
                }
            }
        }

        // Second pass - apply snapped vertices to all meshes
        foreach (Tile tile in tilesCreated)
        {
            MeshFilter meshFilter = tile.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;

            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            bool modified = false;

            // Update vertices based on the snapped positions
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = tile.transform.TransformPoint(vertices[i]);

                if (snappedVerticesMap.TryGetValue(worldPos, out Vector3 snappedPos))
                {
                    // Transform back to local space
                    Vector3 localSnapped = tile.transform.InverseTransformPoint(snappedPos);

                    // Only update if it's different
                    if (Vector3.Distance(vertices[i], localSnapped) > 0.00001f)
                    {
                        vertices[i] = localSnapped;
                        modified = true;
                    }
                }
            }

            // Apply changes if needed
            if (modified)
            {
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
            }
        }

        Debug.Log("Vertex snapping complete");
    }
    // Add this method to the PolyGenEditor class
    private void EnsureSharedEdges()
    {
        Debug.Log("Ensuring shared edges between tiles...");

        // First, perform vertex snapping to ensure vertices are aligned
        SnapAllVerticesToGrid();

        // Then increase tolerance for neighbor detection
        neighborDetectionRadius = 1.5f; // Increase from default 1.0

        // Process neighbors with increased tolerance
        ProcessTileNeighbors();

        Debug.Log("Edge connectivity process complete");
    }

    // Add a button to use this function in your OnInspectorGUI method
    private void AddEnsureSharedEdgesButton()
    {
        // Add a section for fixing gaps
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Fix Polygon Gaps", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Fix Polygon Gaps", GUILayout.Height(30), GUILayout.Width(150)))
        {
            EnsureSharedEdges();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void PlaceMeshesInScene(PolyGen polyGen)
    {
        // Clear any previously stored tiles
        tilesCreated.Clear();

        // Check if the Resources path exists
        string relativePath = resourcesFolderPath;
        string positionsPath = Path.Combine(relativePath, "Positions");
        string edgesPath = Path.Combine(relativePath, "Edges");

        // Create parent game objects for organization
        GameObject landParent = new GameObject("Land_Tiles");
        GameObject waterParent = new GameObject("Water_Tiles");

        // Try to load all meshes from Land and Water folders
        string landPath = Path.Combine(relativePath, "Land");
        string waterPath = Path.Combine(relativePath, "Water");

        // Load and place meshes with positions
        bool landLoaded = LoadAndPlaceMeshesWithPositions(landPath, positionsPath, edgesPath, landParent, true, landMaterial);
        bool waterLoaded = LoadAndPlaceMeshesWithPositions(waterPath, positionsPath, edgesPath, waterParent, false, waterMaterial);

        // Process neighbors for all tiles
        if (tilesCreated.Count > 0)
        {
            ProcessTileNeighbors();
        }

        // Report counts for diagnostics
        Debug.Log($"Placed {landParent.transform.childCount} land tiles and {waterParent.transform.childCount} water tiles");
        Debug.Log($"Total tiles with Tile component: {tilesCreated.Count}");

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

    private bool LoadAndPlaceMeshesWithPositions(string resourcePath, string positionsPath, string edgesPath, GameObject parent, bool isLandType, Material material)
    {
        bool anyLoaded = false;

        // First try to load all meshes directly from the folder using LoadAll
        Object[] allMeshes = Resources.LoadAll(resourcePath, typeof(Mesh));

        if (allMeshes != null && allMeshes.Length > 0)
        {
            Debug.Log($"Found {allMeshes.Length} meshes in {resourcePath}");
            foreach (Object obj in allMeshes)
            {
                Mesh mesh = obj as Mesh;
                if (mesh != null)
                {
                    // Extract cell index from the mesh name
                    string meshName = mesh.name;
                    if (meshName.StartsWith("Cell_"))
                    {
                        string indexStr = meshName.Substring(5); // Skip "Cell_"
                        if (int.TryParse(indexStr, out int cellIndex))
                        {
                            // Load position data for this cell
                            TextAsset positionJson = Resources.Load<TextAsset>(Path.Combine(positionsPath, $"Position_{cellIndex}"));
                            TextAsset edgeJson = Resources.Load<TextAsset>(Path.Combine(edgesPath, $"Edges_{cellIndex}"));

                            if (positionJson != null)
                            {
                                CellPosition cellPosition = JsonUtility.FromJson<CellPosition>(positionJson.text);

                                // Create game object with the proper position and edges
                                EdgeData edgeData = null;
                                if (edgeJson != null)
                                {
                                    edgeData = JsonUtility.FromJson<EdgeData>(edgeJson.text);
                                }

                                CreateTileGameObjectAtPosition(mesh, parent, cellPosition.position,
                                    cellPosition.isLand ? landMaterial : waterMaterial, edgeData, cellPosition.isLand);

                                anyLoaded = true;
                            }
                            else
                            {
                                // Fallback if position data not found
                                CreateTileGameObject(mesh, parent, isLandType, material);
                                anyLoaded = true;
                            }
                        }
                        else
                        {
                            // Fallback if index parsing fails
                            CreateTileGameObject(mesh, parent, isLandType, material);
                            anyLoaded = true;
                        }
                    }
                    else
                    {
                        // Fallback for meshes without expected naming convention
                        CreateTileGameObject(mesh, parent, isLandType, material);
                        anyLoaded = true;
                    }
                }
            }
            return anyLoaded;
        }

        // Fallback approach (similar to before but with position data)
        int meshIndex = 0;
        int maxAttempts = 2000;
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
                if (anyLoaded && consecutiveFailures > 10)
                {
                    break;
                }
            }
            else
            {
                // Load position data for this cell
                TextAsset positionJson = Resources.Load<TextAsset>(Path.Combine(positionsPath, $"Position_{meshIndex}"));
                TextAsset edgeJson = Resources.Load<TextAsset>(Path.Combine(edgesPath, $"Edges_{meshIndex}"));

                if (positionJson != null)
                {
                    CellPosition cellPosition = JsonUtility.FromJson<CellPosition>(positionJson.text);

                    // Load edge data if available
                    EdgeData edgeData = null;
                    if (edgeJson != null)
                    {
                        edgeData = JsonUtility.FromJson<EdgeData>(edgeJson.text);
                    }

                    // Create game object with the proper position and edges
                    CreateTileGameObjectAtPosition(mesh, parent, cellPosition.position,
                        cellPosition.isLand ? landMaterial : waterMaterial, edgeData, cellPosition.isLand);

                    anyLoaded = true;
                }
                else
                {
                    // Fallback if position data not found
                    CreateTileGameObject(mesh, parent, isLandType, material);
                    anyLoaded = true;
                }

                consecutiveFailures = 0;
            }

            meshIndex++;
        }

        return anyLoaded;
    }

    private void CreateTileGameObject(Mesh mesh, GameObject parent, bool isLandType, Material material)
    {
        GameObject tileObject = new GameObject(mesh.name);
        tileObject.transform.parent = parent.transform;

        // Add TileComponent to Tile
        Tile t = tileObject.AddComponent<Tile>();
        t.Id = mesh.name;
        t.Name = mesh.name + (isLandType? "_Province":"_Sea");
        t.Description = "";
        t.isLand = isLandType;
        t.disabledColor = this.disabledEdgeColor;


        t.neighbors = new List<string>();
        this.tilesCreated.Add(t);

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

        // Add Rigidbody and make Kinematic
        var rb = tileObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.automaticCenterOfMass = false;

        // Add Collider
        var col = tileObject.AddComponent<MeshCollider>();
        col.convex = false;
    }

    private void CreateTileGameObjectAtPosition(Mesh mesh, GameObject parent, Vector3 position, Material material, EdgeData edgeData, bool isLand)
    {
        GameObject tileObject = new GameObject(mesh.name);
        tileObject.transform.parent = parent.transform;

        // Set the position of the tile
        tileObject.transform.position = position;

        // Add the Tile Component
        Tile tileComponent = tileObject.AddComponent<Tile>();
        tileComponent.Name = mesh.name + (isLand ? "_Province" : "_Sea");
        tileComponent.Id = tileObject.name;
        tileComponent.neighbors = new List<string>();
        tileComponent.isLand = isLand;
        tileComponent.disabledColor = disabledEdgeColor;
        this.tilesCreated.Add(tileComponent);

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
            newMaterial.color = isLand ? Color.green : Color.blue;
            meshRenderer.sharedMaterial = newMaterial;
        }
        // Add Rigidbody and make Kinematic
        var rb = tileObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.automaticCenterOfMass = false;

        // Add Collider
        var col = tileObject.AddComponent<MeshCollider>();
        col.convex = false;

        // Add edge highlights if enabled
        if (addEdgeHighlights)
        {
            Color edgeColor = isLand ? landEdgeColor : waterEdgeColor;

            // Create a single line renderer for the entire polygon outline
            GameObject lineObj = new GameObject("PolygonOutline");
            lineObj.transform.parent = tileObject.transform;
            lineObj.transform.localPosition = Vector3.zero;
            lineObj.transform.localPosition -= Vector3.up * 0.07f;

            // Get mesh vertices
            Vector3[] meshVertices = mesh.vertices;
            if (meshVertices != null && meshVertices.Length > 1)
            {
                // Create a single LineRenderer for the perimeter
                LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false; // Use local space for mesh vertices
                lineRenderer.startWidth = edgeLineWidth;
                lineRenderer.endWidth = edgeLineWidth;

                // In the centered mesh, vertex 0 is the center, vertices 1+ are the perimeter
                int perimeterVertexCount = meshVertices.Length - 1;

                // Set position count (+1 to close the loop)
                lineRenderer.positionCount = perimeterVertexCount + 1;

                // Set positions - build the outline by connecting perimeter vertices
                for (int i = 0; i < perimeterVertexCount; i++)
                {
                    // Add height offset and get the actual perimeter vertex (index i+1 since vertex 0 is center)
                    Vector3 vertPos = meshVertices[i + 1];
                    Vector3 posWithOffset = new Vector3(vertPos.x, edgeHeight, vertPos.z);
                    lineRenderer.SetPosition(i, posWithOffset);
                }

                // Close the loop by connecting back to the first perimeter vertex
                Vector3 firstVertPos = meshVertices[1];
                Vector3 firstPosWithOffset = new Vector3(firstVertPos.x, edgeHeight, firstVertPos.z);
                lineRenderer.SetPosition(perimeterVertexCount, firstPosWithOffset);

                // Set material properties
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = edgeColor;
                lineRenderer.endColor = edgeColor;

                // Make sure the line doesn't fade out at joints
                lineRenderer.numCapVertices = 4;
                lineRenderer.numCornerVertices = 4;

                Debug.Log($"Created polygon outline for {tileObject.name} with {perimeterVertexCount} edges");
            }
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

[System.Serializable]
public class EdgeData
{
    public Vector3[][] edges;

    // Unity's JsonUtility requires a default constructor
    public EdgeData()
    {
        edges = new Vector3[0][];
    }
}

[System.Serializable]
public class CellPosition
{
    public Vector3 position;
    public bool isLand;
}
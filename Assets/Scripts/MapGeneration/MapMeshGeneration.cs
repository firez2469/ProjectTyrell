using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using Newtonsoft.Json;

// Define these in a namespace or static class to ensure accessibility
namespace MapData
{
    [Serializable]
    public class PolygonData
    {
        [JsonProperty("edges")]
        public List<List<int>> edges;

        [JsonProperty("vertices")]
        public List<List<float>> vertices;

        [JsonProperty("polygons")]
        public List<List<int>> polygons;
    }

    [Serializable]
    public class TileData
    {
        [JsonProperty("name")]
        public string name;

        [JsonProperty("edges")]
        public List<int> edges;

        [JsonProperty("type")]
        public string type;

        [JsonProperty("biome")]
        public string biome;

        [JsonProperty("neighbors")]
        public List<string> neighbors;
    }
}

public class MapMeshGeneration : MonoBehaviour
{
    [SerializeField] private Material landMaterial;
    [SerializeField] private Material seaMaterial;
    [SerializeField] private bool regenerateMeshes = false;
    [SerializeField] public TextAsset polygonDataAsset;
    [SerializeField] public TextAsset tileDataAsset;
    [SerializeField] public float lineWidth = 0.1f;
    [SerializeField] public Color worldLineColor = Color.gray;
    [SerializeField] public Color highlightLandColor = Color.green;
    [SerializeField] public Color highlightSeaColor = Color.blue;
    [SerializeField] public Color landLineColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] public Color seaLineColor = new Color(0.2f, 0.2f, 0.8f);

    // Optional - Dictionary mapping biome names to colors for visualization
    [SerializeField] public BiomeColorMapping[] biomeColors;

    // Class to hold biome color mapping in inspector
    [Serializable]
    public class BiomeColorMapping
    {
        public string biomeName;
        public Color color = Color.white;
    }

    // Static reference to the highlight line renderer
    public static LineRenderer highlightLineRenderer;
    private LineRenderer worldLineRenderer;

    private Dictionary<string, MeshTile> tiles = new Dictionary<string, MeshTile>();
    private Dictionary<int, Vector3> vertexMap = new Dictionary<int, Vector3>();
    private Dictionary<string, Color> biomeColorMap = new Dictionary<string, Color>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Initialize biome color mapping
        InitializeBiomeColors();
    }

    private void InitializeBiomeColors()
    {
        biomeColorMap.Clear();

        if (biomeColors != null)
        {
            foreach (var mapping in biomeColors)
            {
                if (!string.IsNullOrEmpty(mapping.biomeName))
                {
                    biomeColorMap[mapping.biomeName] = mapping.color;
                }
            }
        }

        // Add default color for unknown biomes
        if (!biomeColorMap.ContainsKey("Unknown"))
        {
            biomeColorMap["Unknown"] = Color.gray;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Load(TextAsset polygonDataAsset)
    {
        if (polygonDataAsset == null)
        {
            Debug.LogError("Polygon data asset is null!");
            return;
        }

        try
        {
            // Initialize biome color mapping
            InitializeBiomeColors();

            // Log the first part of the JSON to debug
            Debug.Log($"Parsing polygon JSON data (first 100 chars): {polygonDataAsset.text.Substring(0, Mathf.Min(100, polygonDataAsset.text.Length))}...");

            // Parse the polygon JSON data using Newtonsoft.Json
            MapData.PolygonData polygonData = JsonConvert.DeserializeObject<MapData.PolygonData>(polygonDataAsset.text);

            // Check if polygonData is null or its properties are null
            if (polygonData == null)
            {
                Debug.LogError("Failed to parse polygon data - returned null object");
                return;
            }

            if (polygonData.vertices == null)
            {
                Debug.LogError("Polygon data has null vertices list");
                return;
            }

            if (polygonData.edges == null)
            {
                Debug.LogError("Polygon data has null edges list");
                return;
            }

            if (polygonData.polygons == null)
            {
                Debug.LogError("Polygon data has null polygons list");
                return;
            }

            Debug.Log($"Successfully parsed polygon data with {polygonData.vertices.Count} vertices, {polygonData.edges.Count} edges, and {polygonData.polygons.Count} polygons");

            // Use the tile data asset if provided, otherwise try to load from Resources
            List<MapData.TileData> tileDataList = new List<MapData.TileData>();

            if (tileDataAsset != null)
            {
                try
                {
                    Debug.Log($"Parsing tile data JSON (first 100 chars): {tileDataAsset.text.Substring(0, Mathf.Min(100, tileDataAsset.text.Length))}...");

                    // Parse the tile data using Newtonsoft.Json
                    tileDataList = JsonConvert.DeserializeObject<List<MapData.TileData>>(tileDataAsset.text);

                    if (tileDataList == null)
                    {
                        Debug.LogError("Failed to parse tile data - returned null list");
                        tileDataList = new List<MapData.TileData>();
                    }
                    else
                    {
                        // Log biome information if available
                        int biomesFound = 0;
                        foreach (var tile in tileDataList)
                        {
                            if (!string.IsNullOrEmpty(tile.biome))
                            {
                                biomesFound++;
                            }
                        }
                        Debug.Log($"Loaded {tileDataList.Count} tiles from tile data JSON with {biomesFound} containing biome information");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing tile data JSON: {e.Message}");
                    // If parsing fails, we'll just generate tiles from polygon data without type information
                }
            }
            else
            {
                Debug.LogWarning("Tile data JSON not found, tiles will default to Land type and Unknown biome");
            }

            // Convert vertices from 2D to 3D
            ConvertVertices(polygonData.vertices);

            // Create mesh tiles
            CreateMeshTiles(polygonData, tileDataList);

            // Save meshes to Resources folder
            SaveMeshes();
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception in Load method: {e.Message}\nStack trace: {e.StackTrace}");
        }
    }

    private void ConvertVertices(List<List<float>> vertices2D)
    {
        vertexMap.Clear();

        // Check if vertices2D is null
        if (vertices2D == null)
        {
            Debug.LogError("vertices2D list is null!");
            return;
        }

        for (int i = 0; i < vertices2D.Count; i++)
        {
            var vertex2D = vertices2D[i];

            // Check if this specific vertex entry is null
            if (vertex2D == null)
            {
                Debug.LogWarning($"Vertex at index {i} is null, skipping");
                continue;
            }

            if (vertex2D.Count >= 2)
            {
                // Convert 2D vertex to 3D (z=0)
                Vector3 vertex3D = new Vector3(vertex2D[0], 0, vertex2D[1]);
                vertexMap[i] = vertex3D;
            }
            else
            {
                Debug.LogWarning($"Vertex at index {i} doesn't have enough coordinates");
            }
        }

        Debug.Log($"Converted {vertexMap.Count} vertices to 3D");
    }

    private void CreateMeshTiles(MapData.PolygonData polygonData, List<MapData.TileData> tileDataList)
    {
        tiles.Clear();

        Dictionary<string, MapData.TileData> tileDataMap = new Dictionary<string, MapData.TileData>();
        foreach (var tileData in tileDataList)
        {
            if (!string.IsNullOrEmpty(tileData.name))
            {
                tileDataMap[tileData.name] = tileData;
            }
        }

        // For each polygon, create a mesh tile
        for (int i = 0; i < polygonData.polygons.Count; i++)
        {
            List<int> edgeIndices = polygonData.polygons[i];
            string tileName = $"tile{i}";

            // Create mesh tile
            MeshTile tile = new MeshTile();
            tile.name = tileName;
            tile.id = i.ToString();

            // Set tile type, biome, and neighbors based on tile data if available
            if (tileDataMap.TryGetValue(tileName, out MapData.TileData tileData))
            {
                tile.type = tileData.type.ToLower() == "sea" ? MeshTileType.Sea : MeshTileType.Land;
                tile.biome = !string.IsNullOrEmpty(tileData.biome) ? tileData.biome : "Unknown";
                tile.neighbors = tileData.neighbors ?? new List<string>();
                tile.tileEdges = tileData.edges ?? new List<int>();
            }
            else
            {
                // Default to Land and Unknown biome if no type information is available
                tile.type = MeshTileType.Land;
                tile.biome = "Unknown";
                tile.neighbors = new List<string>();
                tile.tileEdges = new List<int>();
            }

            // Generate mesh for this tile
            GenerateTileMesh(tile, edgeIndices, polygonData.edges);

            // Add to dictionary
            tiles[tileName] = tile;
        }

        Debug.Log($"Created {tiles.Count} mesh tiles");
    }

    private void GenerateTileMesh(MeshTile tile, List<int> edgeIndices, List<List<int>> allEdges)
    {
        // Collect all unique vertices in this polygon
        HashSet<int> vertexIndices = new HashSet<int>();
        foreach (int edgeIndex in edgeIndices)
        {
            if (edgeIndex >= 0 && edgeIndex < allEdges.Count)
            {
                var edge = allEdges[edgeIndex];
                if (edge.Count >= 2)
                {
                    vertexIndices.Add(edge[0]);
                    vertexIndices.Add(edge[1]);
                }
            }
        }

        // Convert set to list for indexing
        List<int> uniqueVertexIndices = new List<int>(vertexIndices);

        // Compute center of polygon
        Vector3 center = Vector3.zero;
        foreach (int index in uniqueVertexIndices)
        {
            if (vertexMap.TryGetValue(index, out Vector3 vertex))
            {
                center += vertex;
            }
        }
        center /= uniqueVertexIndices.Count;

        // Set tile position to center (for 2D positioning)
        tile.position = new Vector2(center.x, center.z);

        // Mesh vertices
        List<Vector3> meshVertices = new List<Vector3>();

        // Add center vertex
        meshVertices.Add(new Vector3(0, 0, 0));  // Center at local (0,0,0)

        // Add perimeter vertices, sorted by angle from center
        List<KeyValuePair<float, Vector3>> sortedVertices = new List<KeyValuePair<float, Vector3>>();

        foreach (int index in uniqueVertexIndices)
        {
            if (vertexMap.TryGetValue(index, out Vector3 vertex))
            {
                // Convert to local space
                Vector3 localVertex = vertex - center;

                // Calculate angle for sorting (XZ plane)
                float angle = Mathf.Atan2(localVertex.z, localVertex.x);

                sortedVertices.Add(new KeyValuePair<float, Vector3>(angle, localVertex));
            }
        }

        // Sort vertices by angle
        sortedVertices.Sort((a, b) => a.Key.CompareTo(b.Key));

        // Add sorted vertices to mesh
        foreach (var pair in sortedVertices)
        {
            meshVertices.Add(pair.Value);
        }

        // Create triangles (fan triangulation from center)
        List<int> triangles = new List<int>();
        for (int i = 1; i < meshVertices.Count; i++)
        {
            triangles.Add(0);  // Center vertex
            triangles.Add(i);
            triangles.Add(i == meshVertices.Count - 1 ? 1 : i + 1);
        }

        // Create a mesh
        Mesh mesh = new Mesh();
        mesh.name = tile.name;
        mesh.vertices = meshVertices.ToArray();
        mesh.triangles = triangles.ToArray();

        // Create UVs (simple mapping based on normalized position)
        Vector2[] uvs = new Vector2[meshVertices.Count];

        // Find bounds for UV mapping
        Vector3 min = meshVertices[0];
        Vector3 max = meshVertices[0];

        foreach (Vector3 v in meshVertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        // Calculate UVs based on normalized position within bounds
        for (int i = 0; i < meshVertices.Count; i++)
        {
            Vector3 v = meshVertices[i];
            float u = (v.x - min.x) / (max.x - min.x);
            float w = (v.z - min.z) / (max.z - min.z);
            uvs[i] = new Vector2(u, w);
        }

        mesh.uv = uvs;

        // Recalculate normals and bounds
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Set the mesh for the tile
        tile.tileMesh = mesh;
    }

    private void SaveMeshes()
    {
        string folderPath = "Assets/Resources/PolyGen/Meshes";

        // Create directory if it doesn't exist
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            Debug.Log($"Created directory: {folderPath}");
        }

        // Save each mesh
        int savedCount = 0;
        foreach (var tile in tiles.Values)
        {
            if (tile.tileMesh != null)
            {
                string assetPath = $"{folderPath}/{tile.name}.asset";

                // Check if file exists and we're not regenerating
                if (File.Exists(assetPath) && !regenerateMeshes)
                {
                    continue;
                }

                // Save mesh as asset
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.CreateAsset(tile.tileMesh, assetPath);
                savedCount++;
#else
                Debug.LogWarning("Mesh saving only works in the Unity Editor");
#endif
            }
        }

#if UNITY_EDITOR
        // Refresh asset database
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif

        Debug.Log($"Saved {savedCount} meshes to {folderPath}");

        // Create game objects for visualization
        CreateTileGameObjects();
    }

    private void CreateTileGameObjects()
    {
        // Create a parent object to hold all tiles
        Transform parentTransform = new GameObject("TileMap").transform;
        parentTransform.SetParent(transform);

        // Create a single world line renderer that traces all tiles
        GameObject worldLineObj = new GameObject("WorldOutline");
        worldLineObj.transform.SetParent(transform);
        worldLineObj.transform.localPosition = Vector3.zero;

        worldLineRenderer = worldLineObj.AddComponent<LineRenderer>();
        worldLineRenderer.useWorldSpace = false;
        worldLineRenderer.loop = false;
        worldLineRenderer.widthMultiplier = lineWidth;
        worldLineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // Configure world line renderer
        Gradient worldGradient = new Gradient();
        GradientColorKey[] worldColorKeys = new GradientColorKey[1];
        worldColorKeys[0] = new GradientColorKey(worldLineColor, 0f);

        GradientAlphaKey[] worldAlphaKeys = new GradientAlphaKey[1];
        worldAlphaKeys[0] = new GradientAlphaKey(1f, 0f);

        worldGradient.SetKeys(worldColorKeys, worldAlphaKeys);
        worldLineRenderer.colorGradient = worldGradient;

        // Create the highlight line renderer (for highlighting individual tiles)
        GameObject highlightLineObj = new GameObject("HighlightOutline");
        highlightLineObj.transform.SetParent(transform);
        highlightLineObj.transform.localPosition = Vector3.zero;

        highlightLineRenderer = highlightLineObj.AddComponent<LineRenderer>();
        highlightLineRenderer.useWorldSpace = false;
        highlightLineRenderer.loop = true;
        highlightLineRenderer.widthMultiplier = lineWidth * 2; // Make highlight line thicker
        highlightLineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // Initially set the highlight line renderer to have no points
        highlightLineRenderer.positionCount = 0;
        highlightLineRenderer.gameObject.SetActive(false);

        // Collect all tile outline vertices for the world line renderer
        List<Vector3> allOutlinePoints = new List<Vector3>();

        // Process all tiles
        foreach (var tile in tiles.Values)
        {
            GameObject tileObject = new GameObject(tile.name);
            tileObject.transform.SetParent(parentTransform);

            // Position at tile center
            tileObject.transform.position = new Vector3(tile.position.x, 0, tile.position.y);

            // Add mesh filter and renderer
            MeshFilter meshFilter = tileObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = tile.tileMesh;

            MeshRenderer meshRenderer = tileObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = tile.type == MeshTileType.Land ? landMaterial : seaMaterial;

            MeshCollider collider = tileObject.AddComponent<MeshCollider>();
            Rigidbody rb = tileObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // Store outline points in world space for this tile
            Vector3[] verts = tile.tileMesh.vertices;
            List<Vector3> tileOutlinePoints = new List<Vector3>();

            // Skip the center vertex (index 0) and use only perimeter vertices
            for (int i = 1; i < verts.Length; i++)
            {
                // Convert to world space
                Vector3 worldPos = tileObject.transform.TransformPoint(verts[i]);
                worldPos.y = 0;
                tileOutlinePoints.Add(worldPos);
            }

            // Add first point again to close the shape
            if (tileOutlinePoints.Count > 0)
            {
                tileOutlinePoints.Add(tileOutlinePoints[0]);
            }

            // Now add a null point (we'll use this as a separator between tiles)
            // Using Vector3.up * 1000 as a marker point that won't be visible
            allOutlinePoints.AddRange(tileOutlinePoints);
            allOutlinePoints.Add(Vector3.up * 1000);

            // Store the outline points in the tile for later use
            tile.outlinePoints = tileOutlinePoints.ToArray();

            // Add tile component
            Tile tileComponent = tileObject.AddComponent<Tile>();
            tileComponent.Name = tile.name;
            tileComponent.Description = tile.description;
            tileComponent.Id = tile.id;
            tileComponent.biome = tile.biome; // Add biome information
            tileComponent.neighbors = tile.neighbors;
            tileComponent.isLand = MeshTileType.Land == tile.type;
            tileComponent.type = (MeshTileType.Land == tile.type ? Tile.TileType.Land : Tile.TileType.Sea);
            tileComponent.outlinePoints = tile.outlinePoints;
            print("Added " + tile.outlinePoints.Length + " points to " + tile.name + " with biome " + tile.biome);
        }

        // Set all points for the world line renderer
        worldLineRenderer.positionCount = allOutlinePoints.Count;
        worldLineRenderer.SetPositions(allOutlinePoints.ToArray());

        Debug.Log($"Created {tiles.Count} tile game objects and configured world line renderer");
    }

    [Serializable]
    public class MeshTile
    {
        public string name;
        public string id;
        public string description;
        public MeshTileType type;
        public string biome; // Added biome field
        public List<string> neighbors;
        public List<int> tileEdges;
        public Mesh tileMesh;
        public Vector2 position;
        public Vector3[] outlinePoints; // Store outline points
    }

    public enum MeshTileType
    {
        Land, Sea
    }
    public Dictionary<string, MeshTile> GetTiles()
    {
        return tiles;
    }
}
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;

public class MeshRepairUtility : EditorWindow
{
    private GameObject landParent;
    private GameObject waterParent;
    private float snapThreshold = 0.01f;
    private bool processLineRenderers = true;

    [MenuItem("Tools/PolyGen/Mesh Repair Utility")]
    public static void ShowWindow()
    {
        GetWindow<MeshRepairUtility>("Mesh Repair Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("Voronoi Mesh Repair Tool", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("This tool helps fix gaps between Voronoi polygons");

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Land Tiles Parent");
        landParent = (GameObject)EditorGUILayout.ObjectField(landParent, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Water Tiles Parent");
        waterParent = (GameObject)EditorGUILayout.ObjectField(waterParent, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Snap Threshold");
        snapThreshold = EditorGUILayout.Slider(snapThreshold, 0.001f, 0.1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Process LineRenderers");
        processLineRenderers = EditorGUILayout.Toggle(processLineRenderers);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Find All Land/Water Automatically", GUILayout.Height(30)))
        {
            FindTileParents();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Repair Mesh Gaps", GUILayout.Height(40), GUILayout.Width(200)))
        {
            RepairMeshGaps();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(15);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Update Tile Neighbors", GUILayout.Height(30), GUILayout.Width(180)))
        {
            UpdateTileNeighbors();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void FindTileParents()
    {
        landParent = GameObject.Find("Land_Tiles");
        waterParent = GameObject.Find("Water_Tiles");

        if (landParent == null)
        {
            landParent = GameObject.Find("Land");
        }

        if (waterParent == null)
        {
            waterParent = GameObject.Find("Water");
        }

        if (landParent == null && waterParent == null)
        {
            EditorUtility.DisplayDialog("Not Found", "Could not find Land_Tiles or Water_Tiles objects in the scene.", "OK");
        }
    }

    private void RepairMeshGaps()
    {
        if (landParent == null && waterParent == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select at least one parent object containing tiles.", "OK");
            return;
        }

        // Collect all tiles
        List<GameObject> allTiles = new List<GameObject>();

        if (landParent != null)
        {
            for (int i = 0; i < landParent.transform.childCount; i++)
            {
                allTiles.Add(landParent.transform.GetChild(i).gameObject);
            }
        }

        if (waterParent != null)
        {
            for (int i = 0; i < waterParent.transform.childCount; i++)
            {
                allTiles.Add(waterParent.transform.GetChild(i).gameObject);
            }
        }

        if (allTiles.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No tiles found in the selected parent objects.", "OK");
            return;
        }

        EditorUtility.DisplayProgressBar("Repairing Meshes", "Collecting vertex data...", 0f);

        try
        {
            // Start recording Undo
            Undo.RegisterCompleteObjectUndo(allTiles.ToArray(), "Repair Mesh Gaps");

            // Repair the meshes
            RepairMeshes(allTiles);

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // Helper class to store vertex information
    private class VertexInfo
    {
        public GameObject Tile;
        public MeshFilter MeshFilter;
        public Vector3 LocalPosition;
        public int VertexIndex;
    }

    private void RepairMeshes(List<GameObject> tiles)
    {
        Debug.Log($"Repairing {tiles.Count} tiles");

        // Step 1: Map of all vertices to their world positions
        Dictionary<Vector3, List<VertexInfo>> vertexMap = new Dictionary<Vector3, List<VertexInfo>>();

        // Keep track of which meshes we need to update
        HashSet<MeshFilter> meshesToUpdate = new HashSet<MeshFilter>();

        // Gather all vertices
        for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
        {
            GameObject tile = tiles[tileIndex];
            MeshFilter mf = tile.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            EditorUtility.DisplayProgressBar("Collecting Vertices",
                $"Processing tile {tileIndex + 1}/{tiles.Count}",
                (float)tileIndex / tiles.Count);

            Vector3[] verts = mf.sharedMesh.vertices;
            for (int i = 1; i < verts.Length; i++) // Skip center (0)
            {
                Vector3 worldPos = tile.transform.TransformPoint(verts[i]);

                // Find any close vertices already in the map
                bool foundMatch = false;
                foreach (Vector3 key in vertexMap.Keys.ToList())
                {
                    if (Vector3.Distance(worldPos, key) <= snapThreshold)
                    {
                        // Add this vertex to the existing group
                        vertexMap[key].Add(new VertexInfo
                        {
                            Tile = tile,
                            MeshFilter = mf,
                            LocalPosition = verts[i],
                            VertexIndex = i
                        });
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    // Create a new entry
                    vertexMap[worldPos] = new List<VertexInfo> {
                        new VertexInfo {
                            Tile = tile,
                            MeshFilter = mf,
                            LocalPosition = verts[i],
                            VertexIndex = i
                        }
                    };
                }
            }
        }

        // Step 2: Identify vertices that need to be snapped (more than one tile sharing a position)
        int vertexGroups = 0;
        int verticesAffected = 0;

        foreach (var group in vertexMap.Values)
        {
            if (group.Count > 1)
            {
                vertexGroups++;
                verticesAffected += group.Count;

                // Calculate average position
                Vector3 avgWorldPos = Vector3.zero;
                foreach (var info in group)
                {
                    avgWorldPos += info.Tile.transform.TransformPoint(info.LocalPosition);
                }
                avgWorldPos /= group.Count;

                // Update all vertices in this group to use the same world position
                foreach (var info in group)
                {
                    // Convert average world position back to local space for this tile
                    Vector3 newLocalPos = info.Tile.transform.InverseTransformPoint(avgWorldPos);

                    // Get mesh and update the vertex position
                    MeshFilter mf = info.MeshFilter;

                    // We need to create a new instance of the mesh to modify it
                    if (!meshesToUpdate.Contains(mf))
                    {
                        // Clone the mesh if this is the first time modifying it
                        mf.sharedMesh = Object.Instantiate(mf.sharedMesh);
                        meshesToUpdate.Add(mf);
                    }

                    // Get the current vertices
                    Vector3[] verts = mf.sharedMesh.vertices;

                    // Update the vertex position
                    verts[info.VertexIndex] = newLocalPos;

                    // Apply the changes
                    mf.sharedMesh.vertices = verts;
                }
            }
        }

        // Step 3: Recalculate bounds and normals for all modified meshes
        foreach (var mf in meshesToUpdate)
        {
            mf.sharedMesh.RecalculateBounds();
            mf.sharedMesh.RecalculateNormals();
        }

        // Step 4: If requested, update LineRenderers to match the new mesh vertices
        if (processLineRenderers)
        {
            UpdateLineRenderers(tiles, meshesToUpdate);
        }

        // Report statistics
        Debug.Log($"Repair complete. Modified {meshesToUpdate.Count} meshes, aligned {vertexGroups} vertex groups, affecting {verticesAffected} vertices.");

        if (meshesToUpdate.Count > 0)
        {
            EditorUtility.DisplayDialog("Repair Complete",
                $"Modified {meshesToUpdate.Count} meshes\nAligned {vertexGroups} vertex groups\nAffected {verticesAffected} vertices",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Repair Complete",
                "No gaps found that needed to be repaired. All vertices are already properly aligned.",
                "OK");
        }
    }

    private void UpdateLineRenderers(List<GameObject> tiles, HashSet<MeshFilter> modifiedMeshes)
    {
        int lineRenderersUpdated = 0;

        EditorUtility.DisplayProgressBar("Updating Outlines", "Updating LineRenderers to match modified meshes...", 0f);

        for (int i = 0; i < tiles.Count; i++)
        {
            GameObject tile = tiles[i];
            MeshFilter mf = tile.GetComponent<MeshFilter>();

            // Only process tiles with modified meshes
            if (mf == null || !modifiedMeshes.Contains(mf)) continue;

            // Find LineRenderer in children (assuming it's in a child GameObject named "PolygonOutline")
            LineRenderer[] lineRenderers = tile.GetComponentsInChildren<LineRenderer>();

            foreach (LineRenderer lineRenderer in lineRenderers)
            {
                if (lineRenderer != null)
                {
                    // Get mesh vertices
                    Vector3[] meshVertices = mf.sharedMesh.vertices;

                    // Skip if mesh doesn't have enough vertices
                    if (meshVertices.Length <= 1) continue;

                    // First vertex is usually the center, so we start from index 1
                    int perimeterVertexCount = meshVertices.Length - 1;

                    // Make sure the LineRenderer has the right number of positions
                    // We add one extra position to close the loop
                    if (lineRenderer.positionCount != perimeterVertexCount + 1)
                    {
                        lineRenderer.positionCount = perimeterVertexCount + 1;
                    }

                    // Get the current height offset of the LineRenderer
                    float heightOffset = 0;
                    if (lineRenderer.positionCount > 0)
                    {
                        // Get local position of the first point
                        Vector3 firstPos = lineRenderer.GetPosition(0);
                        // Assuming Y is up, use its Y component as the height offset
                        heightOffset = firstPos.y;
                    }

                    // Update positions based on the new mesh vertices
                    for (int v = 0; v < perimeterVertexCount; v++)
                    {
                        Vector3 vertPos = meshVertices[v + 1]; // +1 to skip center
                        Vector3 posWithOffset = new Vector3(vertPos.x, heightOffset, vertPos.z);
                        lineRenderer.SetPosition(v, posWithOffset);
                    }

                    // Close the loop by connecting back to the first perimeter vertex
                    Vector3 firstVertPos = meshVertices[1];
                    Vector3 firstPosWithOffset = new Vector3(firstVertPos.x, heightOffset, firstVertPos.z);
                    lineRenderer.SetPosition(perimeterVertexCount, firstPosWithOffset);

                    lineRenderersUpdated++;
                }
            }

            EditorUtility.DisplayProgressBar("Updating Outlines",
                $"Processing tile {i + 1}/{modifiedMeshes.Count}",
                (float)i / modifiedMeshes.Count);
        }

        Debug.Log($"Updated {lineRenderersUpdated} LineRenderers to match modified meshes");
    }

    private void UpdateTileNeighbors()
    {
        if (landParent == null && waterParent == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select at least one parent object containing tiles.", "OK");
            return;
        }

        // Collect all tiles
        List<Tile> tiles = new List<Tile>();

        if (landParent != null)
        {
            tiles.AddRange(landParent.GetComponentsInChildren<Tile>());
        }

        if (waterParent != null)
        {
            tiles.AddRange(waterParent.GetComponentsInChildren<Tile>());
        }

        if (tiles.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No Tile components found in the selected parent objects.", "OK");
            return;
        }

        // Start recording Undo
        Undo.RegisterCompleteObjectUndo(tiles.ToArray(), "Update Tile Neighbors");

        ProcessTileNeighbors(tiles);

        // Mark scene as dirty
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private void ProcessTileNeighbors(List<Tile> tiles)
    {
        // First make sure we have enough tiles to process
        if (tiles.Count <= 1)
        {
            Debug.LogWarning("Not enough tiles to process neighbors");
            return;
        }

        Debug.Log($"Processing neighbors for {tiles.Count} tiles based on shared edges");

        // Cleanup Check before run
        for (int i = 0; i < tiles.Count; i++)
        {
            Tile currentTile = tiles[i];

            if (string.IsNullOrEmpty(currentTile.Id))
            {
                currentTile.Id = currentTile.gameObject.name;
                currentTile.Name = currentTile.gameObject.name;
            }
            currentTile.neighbors = new List<string>();
        }

        // Use a different approach - check for edge overlap
        for (int i = 0; i < tiles.Count; i++)
        {
            Tile currentTile = tiles[i];
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
                int nextV = v % (currentVerts.Length - 1) + 1; // Skip center (0) and wrap around perimeter

                Vector3 worldV1 = currentTile.transform.TransformPoint(currentVerts[v]);
                Vector3 worldV2 = currentTile.transform.TransformPoint(currentVerts[nextV]);

                // Skip if the vertices are too close (likely duplicates)
                if (Vector3.Distance(worldV1, worldV2) > 0.01f)
                {
                    currentEdges.Add(new Edge(worldV1, worldV2));
                }
            }

            // Check each other tile for overlapping edges
            for (int j = 0; j < tiles.Count; j++)
            {
                if (i == j) continue; // Skip self

                Tile otherTile = tiles[j];
                MeshFilter otherMeshFilter = otherTile.GetComponent<MeshFilter>();

                if (otherMeshFilter == null || otherMeshFilter.sharedMesh == null)
                    continue;

                Vector3[] otherVerts = otherMeshFilter.sharedMesh.vertices;

                // Get perimeter edges (skip center vertex which is at index 0)
                List<Edge> otherEdges = new List<Edge>();
                for (int v = 1; v < otherVerts.Length; v++)
                {
                    int nextV = v % (otherVerts.Length - 1) + 1; // Skip center (0) and wrap around perimeter

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
                        if (EdgesOverlap(currentEdge, otherEdge, snapThreshold))
                        {
                            areNeighbors = true;
                            break;
                        }
                    }
                    if (areNeighbors) break;
                }

                if (areNeighbors && !currentTile.neighbors.Contains(otherTile.Id))
                {
                    currentTile.neighbors.Add(otherTile.Id);
                }
            }

            EditorUtility.DisplayProgressBar("Processing Neighbors",
                $"Processing tile {i + 1}/{tiles.Count}",
                (float)i / tiles.Count);
        }

        // Log neighbor statistics
        int totalNeighbors = 0;
        int maxNeighbors = 0;
        int tilesWithNoNeighbors = 0;

        foreach (Tile tile in tiles)
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

        float avgNeighbors = tiles.Count > 0 ? (float)totalNeighbors / tiles.Count : 0;
        Debug.Log($"Neighbor statistics: Avg: {avgNeighbors:F1}, Max: {maxNeighbors}, Tiles with no neighbors: {tilesWithNoNeighbors}");

        EditorUtility.DisplayDialog("Neighbor Processing Complete",
            $"Processed {tiles.Count} tiles\nAverage neighbors per tile: {avgNeighbors:F1}\nTiles with no neighbors: {tilesWithNoNeighbors}",
            "OK");
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

    // Helper method to check if two edges overlap
    private bool EdgesOverlap(Edge edge1, Edge edge2, float tolerance)
    {
        // Check if the edges are approximately the same (in either direction)
        bool sameDirectOrder = (Vector3.Distance(edge1.v1, edge2.v1) < tolerance &&
                              Vector3.Distance(edge1.v2, edge2.v2) < tolerance);

        bool reverseOrder = (Vector3.Distance(edge1.v1, edge2.v2) < tolerance &&
                           Vector3.Distance(edge1.v2, edge2.v1) < tolerance);

        // Also check if the edges are partially overlapping
        if (!sameDirectOrder && !reverseOrder)
        {
            // Calculate edge vectors
            Vector3 dir1 = (edge1.v2 - edge1.v1).normalized;
            Vector3 dir2 = (edge2.v2 - edge2.v1).normalized;

            // If edges are roughly parallel
            if (Vector3.Dot(dir1, dir2) > 0.9f || Vector3.Dot(dir1, dir2) < -0.9f)
            {
                // Project endpoints of edge2 onto edge1
                float proj11 = Vector3.Dot(edge2.v1 - edge1.v1, dir1);
                float proj12 = Vector3.Dot(edge2.v2 - edge1.v1, dir1);

                // Length of edge1
                float len1 = Vector3.Distance(edge1.v1, edge1.v2);

                // Check if projections overlap with edge1
                bool overlap = (proj11 >= -tolerance && proj11 <= len1 + tolerance) ||
                              (proj12 >= -tolerance && proj12 <= len1 + tolerance) ||
                              (proj11 <= -tolerance && proj12 >= len1 + tolerance);

                if (overlap)
                {
                    // Check distance between edges
                    Vector3 perp = Vector3.Cross(dir1, Vector3.up).normalized;
                    float dist = Mathf.Abs(Vector3.Dot(edge2.v1 - edge1.v1, perp));

                    return dist < tolerance;
                }
            }
        }

        return sameDirectOrder || reverseOrder;
    }
}
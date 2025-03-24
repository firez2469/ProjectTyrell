using System.Collections.Generic;
using UnityEngine;

public class PolyGen : MonoBehaviour
{
    [SerializeField]
    private Vector2 leftBound;
    [SerializeField]
    private Vector2 rightBound;
    [SerializeField]
    private int points = 100;
    [SerializeField]
    private float pointRadius = 0.5f;
    [SerializeField]
    private bool showPoints = true;
    [SerializeField]
    private bool jitterPoints = true;
    [SerializeField]
    private float jitterAmount = 0.5f;

    [Header("Density Control")]
    [SerializeField]
    private Texture2D densityTexture; // Texture to control point density (white = high density)
    [SerializeField]
    private float densityMultiplier = 5f; // Overall multiplier for the density effect
    [SerializeField]
    private float minPointDistance = 0.5f; // Minimum distance between points
    [SerializeField]
    private float maxPointDistance = 10f; // Maximum distance between points

    [Header("Debug Visualization")]
    [SerializeField]
    private bool showDensityHeatmap = false; // Show density influence as colored gizmos
    [SerializeField]
    private int heatmapResolution = 20; // Grid resolution for the heatmap
    [SerializeField]
    private float heatmapHeight = 0.05f; // Height of the heatmap above terrain

    [Header("Texture Settings")]
    [SerializeField]
    private Texture2D mapTexture;
    [SerializeField]
    private float landThreshold = 50f / 255f; // Red value threshold for land detection
    [SerializeField]
    private Color landColor = Color.grey;
    [SerializeField]
    private Color waterColor = Color.blue;
    [SerializeField]
    private bool showLandClassification = true;

    // Sites (centroids)
    private Vector2[] sites;

    // Voronoi cell data
    private List<List<int>> cellVertexIndices;
    private Vector2[] vertices;

    // Land classification
    private bool[] isLand;

    void Start()
    {
        GenerateVoronoiDiagram();
        if (mapTexture != null)
        {
            ClassifyLandCells();
        }
    }

    private void GenerateSites()
    {
        sites = new Vector2[points];

        float width = rightBound.x - leftBound.x;
        float height = rightBound.y - leftBound.y;

        if (densityTexture != null)
        {
            // Use texture-based density control
            GenerateDensityControlledSites();
        }
        else
        {
            // Original site generation if no density texture is specified
            GenerateUniformSites();
        }
    }

    private void GenerateUniformSites()
    {
        float width = rightBound.x - leftBound.x;
        float height = rightBound.y - leftBound.y;

        // Calculate grid dimensions for initial point distribution
        float cellSize = Mathf.Sqrt((width * height) / points);
        int cols = Mathf.FloorToInt(width / cellSize);
        int rows = Mathf.FloorToInt(height / cellSize);

        // Adjust cell size to fit the desired number of points
        if (cols * rows < points)
        {
            float scaleFactor = Mathf.Sqrt((float)(cols * rows) / points);
            cellSize *= scaleFactor;
            cols = Mathf.FloorToInt(width / cellSize);
            rows = Mathf.FloorToInt(height / cellSize);
        }

        List<Vector2> tempSites = new List<Vector2>();

        // Place points in a grid with slight jitter for more natural distribution
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float posX = leftBound.x + (x + 0.5f) * cellSize;
                float posY = leftBound.y + (y + 0.5f) * cellSize;

                if (jitterPoints)
                {
                    posX += Random.Range(-jitterAmount, jitterAmount) * cellSize;
                    posY += Random.Range(-jitterAmount, jitterAmount) * cellSize;
                }

                // Keep point within bounds
                posX = Mathf.Clamp(posX, leftBound.x + 0.01f, rightBound.x - 0.01f);
                posY = Mathf.Clamp(posY, leftBound.y + 0.01f, rightBound.y - 0.01f);

                tempSites.Add(new Vector2(posX, posY));

                if (tempSites.Count >= points)
                    break;
            }

            if (tempSites.Count >= points)
                break;
        }

        // If we need more points, add random ones
        while (tempSites.Count < points)
        {
            float posX = Random.Range(leftBound.x, rightBound.x);
            float posY = Random.Range(leftBound.y, rightBound.y);
            tempSites.Add(new Vector2(posX, posY));
        }

        // Take only the needed number of points
        for (int i = 0; i < points && i < tempSites.Count; i++)
        {
            sites[i] = tempSites[i];
        }
    }

    private void GenerateDensityControlledSites()
    {
        // Use blue noise (Poisson disk sampling) with variable radius based on density texture
        List<Vector2> tempSites = GeneratePoissonDiskSampling();

        // If we have too many points, only take the number requested
        if (tempSites.Count > points)
        {
            // Prioritize points in higher density areas
            tempSites.Sort((a, b) => {
                float densityA = GetPointDensityFromTexture(a);
                float densityB = GetPointDensityFromTexture(b);
                return densityB.CompareTo(densityA); // Higher density first
            });

            tempSites = tempSites.GetRange(0, points);
        }
        // If we have too few points, add random ones
        else while (tempSites.Count < points)
            {
                float posX = Random.Range(leftBound.x, rightBound.x);
                float posY = Random.Range(leftBound.y, rightBound.y);
                tempSites.Add(new Vector2(posX, posY));
            }

        // Apply the sites
        for (int i = 0; i < points && i < tempSites.Count; i++)
        {
            sites[i] = tempSites[i];
        }
    }

    private List<Vector2> GeneratePoissonDiskSampling()
    {
        List<Vector2> resultPoints = new List<Vector2>();
        float width = rightBound.x - leftBound.x;
        float height = rightBound.y - leftBound.y;

        // Create a grid for spatial acceleration
        float cellSize = minPointDistance / Mathf.Sqrt(2);
        int cols = Mathf.CeilToInt(width / cellSize);
        int rows = Mathf.CeilToInt(height / cellSize);

        // Background grid to track point placements
        int[,] grid = new int[cols, rows];
        List<Vector2> activeList = new List<Vector2>();

        // Try to start with a high density point
        Vector2 firstPoint = FindHighDensityStartPoint();

        resultPoints.Add(firstPoint);
        activeList.Add(firstPoint);

        // Add first point to grid
        int x = Mathf.FloorToInt((firstPoint.x - leftBound.x) / cellSize);
        int y = Mathf.FloorToInt((firstPoint.y - leftBound.y) / cellSize);
        if (x >= 0 && x < cols && y >= 0 && y < rows)
        {
            grid[x, y] = 1;
        }

        // Process active points to find new valid points
        while (activeList.Count > 0 && resultPoints.Count < points * 2) // Try to generate more points than needed
        {
            int randomIndex = Random.Range(0, activeList.Count);
            Vector2 point = activeList[randomIndex];

            bool foundValidPoint = false;

            // Get sampling radius for this location based on texture density
            float radius = GetSamplingRadius(point);

            // Try to find a new valid point around the current point
            for (int i = 0; i < 30; i++) // Try 30 times
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(radius, 2f * radius);

                Vector2 newPoint = new Vector2(
                    point.x + distance * Mathf.Cos(angle),
                    point.y + distance * Mathf.Sin(angle)
                );

                // Check if the point is within bounds
                if (newPoint.x < leftBound.x || newPoint.x > rightBound.x ||
                    newPoint.y < leftBound.y || newPoint.y > rightBound.y)
                {
                    continue;
                }

                // Check grid cell of new point
                int newX = Mathf.FloorToInt((newPoint.x - leftBound.x) / cellSize);
                int newY = Mathf.FloorToInt((newPoint.y - leftBound.y) / cellSize);

                if (newX < 0 || newX >= cols || newY < 0 || newY >= rows || grid[newX, newY] > 0)
                {
                    continue;
                }

                // Check if too close to other points
                bool tooClose = false;
                float newPointRadius = GetSamplingRadius(newPoint);

                // Check nearby cells
                int cellRadius = Mathf.CeilToInt(maxPointDistance / cellSize);

                for (int gridX = Mathf.Max(0, newX - cellRadius); gridX <= Mathf.Min(cols - 1, newX + cellRadius); gridX++)
                {
                    for (int gridY = Mathf.Max(0, newY - cellRadius); gridY <= Mathf.Min(rows - 1, newY + cellRadius); gridY++)
                    {
                        if (grid[gridX, gridY] > 0)
                        {
                            int pointIndex = grid[gridX, gridY] - 1;
                            if (pointIndex < resultPoints.Count)
                            {
                                float dist = Vector2.Distance(newPoint, resultPoints[pointIndex]);
                                if (dist < newPointRadius)
                                {
                                    tooClose = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (tooClose) break;
                }

                if (!tooClose)
                {
                    // Add point to lists and grid
                    resultPoints.Add(newPoint);
                    activeList.Add(newPoint);
                    grid[newX, newY] = resultPoints.Count;
                    foundValidPoint = true;
                    break;
                }
            }

            // If no valid point found, remove the point from active list
            if (!foundValidPoint)
            {
                activeList.RemoveAt(randomIndex);
            }
        }

        return resultPoints;
    }

    private Vector2 FindHighDensityStartPoint()
    {
        // Try to find a good starting point with high density
        int samples = 20;
        Vector2 bestPoint = Vector2.zero;
        float highestDensity = 0f;

        for (int i = 0; i < samples; i++)
        {
            float posX = Random.Range(leftBound.x, rightBound.x);
            float posY = Random.Range(leftBound.y, rightBound.y);
            Vector2 testPoint = new Vector2(posX, posY);

            float density = GetPointDensityFromTexture(testPoint);
            if (density > highestDensity)
            {
                highestDensity = density;
                bestPoint = testPoint;
            }
        }

        // If we didn't find a high density point, use a random one
        if (highestDensity <= 0.1f)
        {
            bestPoint = new Vector2(
                Random.Range(leftBound.x, rightBound.x),
                Random.Range(leftBound.y, rightBound.y)
            );
        }

        return bestPoint;
    }

    private float GetSamplingRadius(Vector2 point)
    {
        float density = GetPointDensityFromTexture(point);

        // Map density [0,1] to radius [maxPointDistance, minPointDistance]
        // Higher density (closer to 1) = smaller radius = denser points
        float radius = Mathf.Lerp(maxPointDistance, minPointDistance, density);

        return radius;
    }

    private float GetPointDensityFromTexture(Vector2 point)
    {
        if (densityTexture == null)
        {
            return 0f; // No texture means uniform density
        }

        // Convert world point to texture UV
        float normalizedX = Mathf.InverseLerp(leftBound.x, rightBound.x, point.x);
        float normalizedY = Mathf.InverseLerp(leftBound.y, rightBound.y, point.y);

        // Sample the texture
        Color pixelColor = densityTexture.GetPixelBilinear(normalizedX, normalizedY);

        // Get the grayscale value (white = high density)
        float density = (pixelColor.r + pixelColor.g + pixelColor.b) / 3f;

        // Apply the density multiplier
        density *= densityMultiplier;

        // Clamp between 0 and 1
        return Mathf.Clamp01(density);
    }

    private void ComputeVoronoiDiagram()
    {
        // This is a simplified approach to generate Voronoi-like cells
        // For a full implementation, a proper Voronoi library would be better

        // Create a large grid of sample points
        int gridResolution = Mathf.Max(100, points * 4);
        float width = rightBound.x - leftBound.x;
        float height = rightBound.y - leftBound.y;
        float dx = width / gridResolution;
        float dy = height / gridResolution;

        // For each site, find its cell boundaries
        cellVertexIndices = new List<List<int>>();
        Dictionary<string, int> vertexLookup = new Dictionary<string, int>();
        List<Vector2> uniqueVertices = new List<Vector2>();

        // For each site, create a polygon by finding neighboring points
        for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
        {
            Vector2 site = sites[siteIndex];

            // Find approximate cell boundary points by sampling around the site
            List<Vector2> boundaryPoints = new List<Vector2>();

            // Sample in different directions around the site
            for (int angle = 0; angle < 16; angle++)
            {
                float theta = angle * Mathf.PI / 8f;
                Vector2 dir = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)).normalized;

                // Start close to the site and move outward until we find a point closer to another site
                float dist = 0.5f * dx;
                float maxDist = Mathf.Max(width, height) * 2;
                Vector2 boundaryPoint = Vector2.zero;
                bool foundBoundary = false;

                while (dist < maxDist)
                {
                    Vector2 testPoint = site + dir * dist;

                    // Skip if outside bounds
                    if (testPoint.x < leftBound.x || testPoint.x > rightBound.x ||
                        testPoint.y < leftBound.y || testPoint.y > rightBound.y)
                    {
                        break;
                    }

                    // Check if this point is closer to another site
                    float distToSite = Vector2.Distance(testPoint, site);
                    bool closerToAnotherSite = false;

                    foreach (Vector2 otherSite in sites)
                    {
                        if (otherSite != site && Vector2.Distance(testPoint, otherSite) < distToSite)
                        {
                            closerToAnotherSite = true;
                            break;
                        }
                    }

                    if (closerToAnotherSite)
                    {
                        // Found a boundary point
                        boundaryPoint = testPoint;
                        foundBoundary = true;
                        break;
                    }

                    dist += dx;
                }

                if (foundBoundary)
                {
                    boundaryPoints.Add(boundaryPoint);
                }
            }

            // Sort boundary points by angle around the site
            boundaryPoints.Sort((a, b) => {
                float angleA = Mathf.Atan2(a.y - site.y, a.x - site.x);
                float angleB = Mathf.Atan2(b.y - site.y, b.x - site.x);
                return angleA.CompareTo(angleB);
            });

            // Create the polygon for this cell
            if (boundaryPoints.Count >= 3)
            {
                List<int> cellIndices = new List<int>();

                foreach (Vector2 point in boundaryPoints)
                {
                    // Round to avoid floating-point issues
                    float x = Mathf.Round(point.x * 10000) / 10000;
                    float y = Mathf.Round(point.y * 10000) / 10000;

                    string key = $"{x},{y}";

                    if (!vertexLookup.TryGetValue(key, out int vertexIndex))
                    {
                        // New unique vertex
                        vertexIndex = uniqueVertices.Count;
                        uniqueVertices.Add(new Vector2(x, y));
                        vertexLookup.Add(key, vertexIndex);
                    }

                    cellIndices.Add(vertexIndex);
                }

                cellVertexIndices.Add(cellIndices);
            }
        }

        // Set the vertices array
        vertices = uniqueVertices.ToArray();
    }

    private void GenerateVoronoiDiagram()
    {
        // Step 1: Generate points (sites) within bounds
        GenerateSites();

        // Step 2: Create Voronoi diagram
        ComputeVoronoiDiagram();
    }

    private void ClassifyLandCells()
    {
        if (sites == null || mapTexture == null)
            return;

        isLand = new bool[sites.Length];

        for (int i = 0; i < sites.Length; i++)
        {
            // Convert site position to texture UV coordinates (percentage across each axis)
            float normalizedX = Mathf.InverseLerp(leftBound.x, rightBound.x, sites[i].x);
            float normalizedY = Mathf.InverseLerp(leftBound.y, rightBound.y, sites[i].y);

            // Sample the texture at the calculated UV coordinates
            int pixelX = Mathf.FloorToInt(normalizedX * mapTexture.width);
            int pixelY = Mathf.FloorToInt(normalizedY * mapTexture.height);

            // Get the color at the sampled position
            Color pixelColor = mapTexture.GetPixel(pixelX, pixelY);

            // Classify as land if red value is less than threshold
            isLand[i] = pixelColor.r > landThreshold;
        }
    }

    // Save the land classification data to be serialized
    public void SaveLandData()
    {
        if (isLand == null)
            return;

        // Create a new texture to store land/water classification
        int texWidth = 512;
        int texHeight = 512;
        Texture2D landDataTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGB24, false);

        // Initialize all pixels to water
        Color[] pixels = new Color[texWidth * texHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = waterColor;
        }

        // For each land cell, mark its area in the texture
        for (int i = 0; i < sites.Length; i++)
        {
            if (isLand[i] && cellVertexIndices.Count > i && cellVertexIndices[i].Count >= 3)
            {
                // Convert site position to texture pixel coordinates
                float normalizedX = Mathf.InverseLerp(leftBound.x, rightBound.x, sites[i].x);
                float normalizedY = Mathf.InverseLerp(leftBound.y, rightBound.y, sites[i].y);

                int pixelX = Mathf.FloorToInt(normalizedX * texWidth);
                int pixelY = Mathf.FloorToInt(normalizedY * texHeight);

                // Mark this pixel as land
                if (pixelX >= 0 && pixelX < texWidth && pixelY >= 0 && pixelY < texHeight)
                {
                    pixels[pixelY * texWidth + pixelX] = landColor;
                }
            }
        }

        landDataTexture.SetPixels(pixels);
        landDataTexture.Apply();

        // Save the texture to a file
        byte[] bytes = landDataTexture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/LandData.png", bytes);
        Debug.Log("Land data saved to " + Application.dataPath + "/LandData.png");
    }

    private void OnDrawGizmos()
    {
        // Draw density heatmap if requested
        if (showDensityHeatmap && densityTexture != null)
        {
            DrawDensityHeatmap();
        }

        // Skip if no Voronoi data
        if (sites == null || vertices == null || cellVertexIndices == null)
            return;

        // Draw the cells
        for (int cellIndex = 0; cellIndex < cellVertexIndices.Count; cellIndex++)
        {
            var cell = cellVertexIndices[cellIndex];

            // Set color based on land classification if available
            if (isLand != null && showLandClassification && cellIndex < isLand.Length)
            {
                Gizmos.color = isLand[cellIndex] ? landColor : waterColor;
            }
            else
            {
                Gizmos.color = Color.gray;
            }

            // Draw cell edges
            for (int i = 0; i < cell.Count; i++)
            {
                int current = cell[i];
                int next = cell[(i + 1) % cell.Count];

                if (current < vertices.Length && next < vertices.Length)
                {
                    Vector3 start = new Vector3(vertices[current].x, 0, vertices[current].y);
                    Vector3 end = new Vector3(vertices[next].x, 0, vertices[next].y);
                    Gizmos.DrawLine(start, end);
                }
            }
        }

        // Draw the sites (centroids)
        if (showPoints && sites != null)
        {
            Gizmos.color = Color.red;
            foreach (var site in sites)
            {
                Gizmos.DrawSphere(new Vector3(site.x, 0, site.y), pointRadius);
            }
        }
    }

    private void DrawDensityHeatmap()
    {
        // Calculate cells to display heatmap
        float width = rightBound.x - leftBound.x;
        float height = rightBound.y - leftBound.y;
        float cellWidth = width / heatmapResolution;
        float cellHeight = height / heatmapResolution;

        // Draw grid of cubes with color based on density from texture
        for (int x = 0; x < heatmapResolution; x++)
        {
            for (int y = 0; y < heatmapResolution; y++)
            {
                float posX = leftBound.x + (x + 0.5f) * cellWidth;
                float posY = leftBound.y + (y + 0.5f) * cellHeight;
                Vector2 pos = new Vector2(posX, posY);

                // Get density at this position
                float density = GetPointDensityFromTexture(pos);

                // Convert density to color (red = high density, blue = low density)
                Color cellColor = Color.Lerp(Color.blue, Color.red, density);
                cellColor.a = 0.6f; // Semi-transparent

                // Draw cube with color indicating density
                Gizmos.color = cellColor;

                // Calculate size based on density (higher density = larger cube)
                float sizeMultiplier = Mathf.Lerp(0.3f, 1.0f, density);
                float cubeSize = Mathf.Min(cellWidth, cellHeight) * 0.8f * sizeMultiplier;

                // Draw the cube
                Vector3 cubePosition = new Vector3(posX, heatmapHeight, posY);
                Gizmos.DrawCube(cubePosition, new Vector3(cubeSize, cubeSize * 0.2f, cubeSize));

                // Draw a vertical line from high density points to illustrate "height"
                if (density > 0.6f)
                {
                    float lineHeight = density * 0.5f;
                    Gizmos.DrawLine(
                        cubePosition,
                        new Vector3(cubePosition.x, cubePosition.y + lineHeight, cubePosition.z)
                    );
                }
            }
        }

        // Draw a legend for the heatmap
        DrawHeatmapLegend();
    }

    private void DrawHeatmapLegend()
    {
        // Define legend position (bottom right corner)
        float legendWidth = 0.5f;
        float legendHeight = 1.5f;
        Vector3 legendStart = new Vector3(rightBound.x - 0.6f, heatmapHeight, leftBound.y + 0.6f);

        // Draw legend background
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        Gizmos.DrawCube(legendStart + new Vector3(0, 0, legendHeight / 2), new Vector3(legendWidth + 0.1f, 0.05f, legendHeight + 0.1f));

        // Draw gradient bar
        int steps = 10;
        float stepSize = legendHeight / steps;

        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / (steps - 1); // 0 to 1
            Color color = Color.Lerp(Color.blue, Color.red, t);
            color.a = 0.8f;

            Gizmos.color = color;
            Vector3 stepPos = legendStart + new Vector3(0, 0.01f, i * stepSize);
            Gizmos.DrawCube(stepPos, new Vector3(legendWidth, 0.02f, stepSize * 0.9f));
        }

        // Draw labels
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(legendStart + new Vector3(0, 0.05f, 0), "Low Density");
        UnityEditor.Handles.Label(legendStart + new Vector3(0, 0.05f, legendHeight), "High Density");
    }

    // Editor menu option to save land data
    [ContextMenu("Save Land Data")]
    public void SaveLandDataMenu()
    {
        SaveLandData();
    }
}
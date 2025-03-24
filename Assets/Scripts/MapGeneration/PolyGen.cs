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
        // Step 1: Generate random points (sites) within bounds
        GenerateSites();

        // Step 2: Create Voronoi diagram using the Fortune's algorithm simulation
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
        // Skip if no data
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

    // Editor menu option to save land data
    [ContextMenu("Save Land Data")]
    public void SaveLandDataMenu()
    {
        SaveLandData();
    }
}
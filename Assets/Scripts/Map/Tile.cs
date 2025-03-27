using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public string Name;
    public string Description;
    public string Id;
    public List<string> neighbors;
    public bool isLand;
    public Color disabledColor;

    // Store the outline points in world space
    [HideInInspector]
    public Vector3[] outlinePoints;

    // Reference to this tile's line renderer
    [HideInInspector]
    public LineRenderer tileLineRenderer;

    private bool isHighlighted = false;

    private void Start()
    {
        // Get the outline points from the corresponding MeshTile if not already set
        if (outlinePoints == null || outlinePoints.Length == 0)
        {
            MapMeshGeneration mapGenerator = FindFirstObjectByType<MapMeshGeneration>();
            if (mapGenerator != null)
            {
                foreach (var tile in mapGenerator.GetTiles())
                {
                    if (tile.Value.name == Name)
                    {
                        outlinePoints = tile.Value.outlinePoints;

                        // If we didn't get a LineRenderer from the mesh generation, create one now
                        if (tileLineRenderer == null)
                        {
                            SetupLineRenderer(mapGenerator);
                        }
                        break;
                    }
                }
            }
        }
    }

    private void SetupLineRenderer(MapMeshGeneration mapGenerator)
    {
        // Add LineRenderer if it doesn't exist
        if (tileLineRenderer == null)
        {
            // Create a child GameObject for the LineRenderer
            GameObject lineRendererObj = new GameObject("TileOutline");
            lineRendererObj.transform.SetParent(transform);
            lineRendererObj.transform.localPosition = Vector3.zero;
            lineRendererObj.transform.localRotation = Quaternion.identity;

            // Add LineRenderer to the child object
            tileLineRenderer = lineRendererObj.AddComponent<LineRenderer>();
            tileLineRenderer.useWorldSpace = false; // Use local space
            tileLineRenderer.loop = true;
            tileLineRenderer.widthMultiplier = mapGenerator.lineWidth * 0.75f;
            tileLineRenderer.material = new Material(Shader.Find("Sprites/Default"));

            // Set positions if we have outline points
            if (outlinePoints != null && outlinePoints.Length > 0)
            {
                // Convert world space points to local space relative to the tile
                List<Vector3> localOutlinePoints = new List<Vector3>();
                foreach (Vector3 worldPoint in outlinePoints)
                {
                    localOutlinePoints.Add(transform.InverseTransformPoint(worldPoint));
                }

                tileLineRenderer.positionCount = localOutlinePoints.Count;
                tileLineRenderer.SetPositions(localOutlinePoints.ToArray());

                // Set color based on tile type
                Color tileLineColor = isLand ? mapGenerator.landLineColor : mapGenerator.seaLineColor;
                Gradient tileGradient = new Gradient();
                GradientColorKey[] tileColorKeys = new GradientColorKey[1];
                tileColorKeys[0] = new GradientColorKey(tileLineColor, 0f);

                GradientAlphaKey[] tileAlphaKeys = new GradientAlphaKey[1];
                tileAlphaKeys[0] = new GradientAlphaKey(1f, 0f);

                tileGradient.SetKeys(tileColorKeys, tileAlphaKeys);
                tileLineRenderer.colorGradient = tileGradient;
            }
        }
    }

    public void Show()
    {
        if (MapMeshGeneration.highlightLineRenderer != null && outlinePoints != null && outlinePoints.Length > 0)
        {
            // Set the highlight line renderer to outline this tile
            MapMeshGeneration.highlightLineRenderer.positionCount = outlinePoints.Length;
            MapMeshGeneration.highlightLineRenderer.SetPositions(outlinePoints);

            // Set the appropriate color based on tile type
            Gradient gradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[1];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[1];

            // Get reference to the map generator to access color settings
            MapMeshGeneration mapGenerator = FindFirstObjectByType<MapMeshGeneration>();
            if (mapGenerator != null)
            {
                Color highlightColor = isLand ? mapGenerator.highlightLandColor : mapGenerator.highlightSeaColor;
                colorKeys[0] = new GradientColorKey(highlightColor, 0f);
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                gradient.SetKeys(colorKeys, alphaKeys);
                MapMeshGeneration.highlightLineRenderer.colorGradient = gradient;
            }

            MapMeshGeneration.highlightLineRenderer.gameObject.SetActive(true);
            isHighlighted = true;

            // Make the tile's own line renderer temporarily invisible while highlighted
            if (tileLineRenderer != null)
            {
                tileLineRenderer.enabled = false;
            }
        }
    }

    public void Hide()
    {
        if (MapMeshGeneration.highlightLineRenderer != null && isHighlighted)
        {
            // Deactivate the highlight line renderer
            MapMeshGeneration.highlightLineRenderer.positionCount = 0;
            MapMeshGeneration.highlightLineRenderer.gameObject.SetActive(false);
            isHighlighted = false;

            // Make the tile's own line renderer visible again
            if (tileLineRenderer != null)
            {
                tileLineRenderer.enabled = true;
            }
        }
    }

    public void Thick()
    {
        if (MapMeshGeneration.highlightLineRenderer != null && isHighlighted)
        {
            MapMeshGeneration.highlightLineRenderer.widthMultiplier *= 5f;
        }
    }

    public void Thin()
    {
        if (MapMeshGeneration.highlightLineRenderer != null && isHighlighted)
        {
            MapMeshGeneration.highlightLineRenderer.widthMultiplier /= 5f;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public string Name;
    public string Description;
    public string Id;
    public List<string> neighbors;
    public bool isLand;
    public TileType type;
    public Color disabledColor;
    public Vector3[] outlinePoints;
    public string biome;
    public TileStats stats;
    public int controllerId = -1;

    public enum TileType { Land, Sea, City, Coast, River, Hills, Mountains }

    // Reference to this tile's line renderer
    [HideInInspector]
    public LineRenderer tileLineRenderer;

    private bool isHighlighted = false;

    private void Start()
    {

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
    }

    public void Hide()
    {
      
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
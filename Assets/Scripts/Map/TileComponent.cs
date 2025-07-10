// Helper component to attach to each tile GameObject
using UnityEngine;

public class TileComponent : MonoBehaviour
{
    public string tileId;
    public string tileName;
    public string tileDescription;
    public MapMeshGeneration.MeshTileType tileType;
    public string[] neighborIds;
    public int[] tileEdges;

    public void Initialize(MapMeshGeneration.MeshTile tile)
    {
        tileId = tile.id;
        tileName = tile.name;
        tileDescription = tile.description;
        tileType = tile.type;
        neighborIds = tile.neighbors.ToArray();
        tileEdges = tile.tileEdges.ToArray();
    }
}
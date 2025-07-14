using DBModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MapDBInitializer : MonoBehaviour
{
    [SerializeField]
    private Transform tileMapParent;
    [SerializeField]
    private bool isInitialized = false;

    public static bool InstantLoad = false;
    public static int GameId = -1;

    [SerializeField]
    private int gidOverride = -1;

    [SerializeField]
    private string wildernessName = "Wilderness";
    [SerializeField]
    private string wildernessDescription = "The wilds, unexplored and vast. Open for any to claim...";
   
    void Start()
    {
        if (gidOverride != -1)
        {
            GameId = gidOverride;
        }
    }

    private async Task RunInitializationAsync(int gameId = 0)
    {
        Debug.Log("Starting DB tile initialization...");

        // Step 0: Initilaize Wilderness Nation before further map preparations.
        int wildernessId = DatabaseControl.CreateNewNation(this.wildernessName,this.wildernessDescription, gameId);
        print($"Nation {this.wildernessName} was created with id: {wildernessId}");

        // Step 1: Collect Unity-dependent data on main thread
        List<DBTile> dbTiles = new List<DBTile>();
        int childCount = tileMapParent.childCount;
        Debug.Log("Children: " + childCount);

        for (int i = 0; i < childCount; i++)
        {
            Transform child = tileMapParent.GetChild(i);
            Tile t = child.GetComponent<Tile>();

            DBTile tile = new DBTile
            {
                name = t.Name,
                dbId = int.Parse(t.Id),
                gameId = gameId,
                description = t.Description,
                type = t.type.ToString(),
                neighborGameIds = new List<int>(),
                owner = wildernessId
            };

            // Convert string neighbor IDs to ints
            foreach (string neighborIdStr in t.neighbors)
            {
                if (int.TryParse(neighborIdStr, out int neighborId))
                {
                    tile.neighborGameIds.Add(neighborId);
                }
            }

            dbTiles.Add(tile);
        }
        print($"Completed tile preparation... starting DB insertion of {dbTiles.Count}");
        // Step 2: Run DB insertions in background thread
        await Task.Run(() =>
        {   

            print($"Performing insertion for {dbTiles.Count}");
            for (int i = 0; i < dbTiles.Count; i++)
            {
                DatabaseControl.InsertTile(dbTiles[i]);
                print($"Insertion {i} complete!");
            }
            print("Completed insertion");
        });

        Debug.Log("DB initialization complete.");
    }

    void Update()
    {
        if (!isInitialized && DatabaseControl.isConnected && MapDBInitializer.InstantLoad)
        {
            _ = RunInitializationAsync(GameId); // fire-and-forget
            isInitialized = true;
        }
    }
}

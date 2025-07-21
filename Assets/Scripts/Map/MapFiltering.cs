using NUnit.Framework.Constraints;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Switch;

public class MapFiltering : MonoBehaviour
{
    public enum MapFilter { None, Nation, Population, Biome }

    public MapFilter filter;
    private MapFilter pFilter;

    [SerializeField]
    private Transform tileParent;

    private Tile[] tiles;


    [SerializeField]
    private int colorSeed = 0;

    [SerializeField]
    private Gradient PopulationGradient;
    [SerializeField]
    private float scalar = 100f;

    private Dictionary<Tile.TileType, Color> biomeColors;
    [SerializeField]
    private Color emptyColor;

    private Dictionary<int, Color> nationColors;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        biomeColors = new Dictionary<Tile.TileType, Color>();
        nationColors = new Dictionary<int, Color>();

        biomeColors.Add(Tile.TileType.Land, Color.green);
        biomeColors.Add(Tile.TileType.Sea, Color.blue);
        tiles = tileParent.GetComponentsInChildren<Tile>();
        Random.InitState(colorSeed);
    }

    private void EmptyMap()
    {
        foreach(Tile t in tiles)
        {
            t.SetColor(emptyColor);
        }
    }

    private void ColorPopulation()
    {
        foreach(Tile t in tiles)
        {
            float population = t.stats.population;
            float fraction = population / PopulationGrowth.WorldPopulation;

            fraction *= scalar;
            fraction = Mathf.Min(fraction, 1);

            Color c = PopulationGradient.Evaluate(fraction);

            if (t.type == Tile.TileType.Sea)
            {
                t.SetColor(emptyColor);
            }
            else
            {
                t.SetColor(c);
            }
           
            
        }
    }

    private void BiomeColor()
    {
        foreach(Tile t in tiles)
        {
            t.SetColor(this.biomeColors[t.type]);
        }
    }
    
    private void NationColor()
    {
        foreach(Tile tile in tiles)
        {
            int nationId = tile.controllerId;
            if (!nationColors.ContainsKey(nationId))
            {
                float r = Random.Range(0, 1f);
                float g = Random.Range(0, 1f);
                float b = Mathf.Max(0f, Mathf.Min(1-(r+g),1.0f));
                print($"Assigned Colors {r}, {g}, {b}");
                nationColors.Add(nationId, new Color(r,g,b));
            }
            if(tile.type == Tile.TileType.Sea)
            {
                tile.SetColor(emptyColor);
            }
            else
            {
                tile.SetColor(nationColors[nationId]);
            }
            
        }
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        
        switch (filter) {
            case MapFilter.None:
                if(pFilter!= filter)
                {
                    pFilter = filter;
                    EmptyMap();
                }
                break;
            case MapFilter.Population:
                ColorPopulation();
                break;
            case MapFilter.Biome:
                if (pFilter != filter)
                {
                    BiomeColor();
                    pFilter = filter;
                }
                break;
            case MapFilter.Nation:
                NationColor();
                pFilter = filter;
                break;
            default:
                break;
           
        }

    }
}

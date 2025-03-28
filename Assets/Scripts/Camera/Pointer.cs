using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Pointer : MonoBehaviour
{
    private Tile[] tiles;
    private Tile activeTile;
    [SerializeField]
    private LineRenderer selector;
    public static Tile Selected;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        this.tiles = FindObjectsByType<Tile>(FindObjectsSortMode.InstanceID);
        Selected = null;
    }

   

    // Update is called once per frame
    void Update()
    {
        
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100))
            {
                Tile t;
                if ((t = hit.collider.gameObject.GetComponent<Tile>()) != null)
                {
                    if (activeTile != null)
                    {
                        activeTile.Hide();
                    }

                    activeTile = t;
                    print(t.Name);
                    t.Show();
                    Assign(t);
                    ProvinceUI.Open();
                    
                }
            }
            else
            {
                if(activeTile != null)
                {
                    activeTile.Hide();
                    Selected = null;
                    ProvinceUI.Close();
                }
               
                activeTile = null;
            }
            
        }
    }

    private void Assign(Tile tile)
    {
        
        Vector3[] outline= tile.outlinePoints;
        //selector.transform.position = tile.transform.position;
        selector.transform.localScale = new Vector3(-12, 1, 7);
        selector.positionCount = outline.Length;
        selector.SetPositions(outline);
        selector.startColor = (tile.isLand? Color.green : Color.blue);
        selector.endColor = (tile.isLand ? Color.green : Color.blue);
        Selected = tile;
    }
}

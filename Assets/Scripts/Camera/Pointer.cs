using System.Collections.Generic;
using UnityEngine;

public class Pointer : MonoBehaviour
{
    private Tile[] tiles;
    private Tile activeTile;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        this.tiles = FindObjectsByType<Tile>(FindObjectsSortMode.InstanceID);

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
                }
            }
            else
            {
                if(activeTile != null)
                {
                    activeTile.Hide();
                }
               
                activeTile = null;
            }
            
        }
       
            
    }
}

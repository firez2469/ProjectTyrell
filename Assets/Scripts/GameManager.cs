using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static MapFilter activeMapFilters;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public enum MapFilter { None=0, NationsUI =1}
}
using UnityEngine;

public class PopulationGrowth : MonoBehaviour
{
    [SerializeField]
    private float defualtPopGrowthSpeed = 1.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SimulationMaster.Instance.OnDayComplete.AddListener(GrowAll);
    }

    private void GrowAll()
    {
        DatabaseControl.MultiplyColumnValues("tiles", "population", defualtPopGrowthSpeed, MapDBInitializer.GameId);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

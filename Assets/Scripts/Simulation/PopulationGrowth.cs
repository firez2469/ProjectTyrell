using UnityEngine;

public class PopulationGrowth : MonoBehaviour
{
    [SerializeField]
    private float defualtPopGrowthSpeed = 1.5f;

    public int TotalPopulation { get; private set; }
    public static int WorldPopulation { get { return instance.TotalPopulation; } }

    private static PopulationGrowth instance;


    private void Awake()
    {
        instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SimulationMaster.Instance.OnDayComplete.AddListener(GrowAll);
    }

    private void GrowAll()
    {
        DatabaseControl.MultiplyColumnValues("tiles", "population", defualtPopGrowthSpeed, MapDBInitializer.GameId);
        TotalPopulation = DatabaseControl.GetSumOfColumn(MapDBInitializer.GameId, "population");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

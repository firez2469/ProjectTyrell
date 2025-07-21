using System.Collections.Generic;
using UnityEngine;

public class PopulationGrowth : MonoBehaviour
{
    [SerializeField]
    private float defualtPopGrowthSpeed = 1.001f;
    [SerializeField]
    private float popGrowthVariance = 0.001f;

    public int TotalPopulation { get; private set; }
    public static int WorldPopulation { get { return instance.TotalPopulation; } }

    private static PopulationGrowth instance;
    [SerializeField]
    private int randomSeed = 42;

    private int tickCount = 0;



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
        int gameId = MapDBInitializer.GameId;

        // Seed random for reproducibility (based on seed + tick count)
        UnityEngine.Random.InitState(randomSeed + tickCount);

        var nationIds = DatabaseControl.GetNationIdsByGameId(gameId);
        var growthRates = new Dictionary<int, double>();

        foreach (int nationId in nationIds)
        {
            float multiplier = defualtPopGrowthSpeed + UnityEngine.Random.Range(-popGrowthVariance, popGrowthVariance);
            growthRates[nationId] = multiplier;
        }

        DatabaseControl.UpdateNationIncrements("tiles", "population", growthRates, gameId);

        TotalPopulation = DatabaseControl.GetSumOfColumn(gameId, "population");
        Debug.Log($"[Growth] World population now: {TotalPopulation:N0}");

        tickCount++; // advance the tick
    }




    // Update is called once per frame
    void Update()
    {
        
    }
}

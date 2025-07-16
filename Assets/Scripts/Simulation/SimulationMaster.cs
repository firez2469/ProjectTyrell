using UnityEngine;
using UnityEngine.Events;

public class SimulationMaster : MonoBehaviour
{

    public static SimulationMaster Instance { get; private set; }
    [Tooltip("The number of in game days per real world second.")]
    [SerializeField]
    private float daysPerSecond = 1f;
  

    [SerializeField]
    public int Day { get; private set; }
    public int PDay { get; private set; }
    private float day = 0f;
    private float pDay = 0f;

    public bool isPaused {  get; private set; }

    [SerializeField]
    public UnityEvent OnDayComplete;

    private void Awake()
    {
        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isPaused = false;
        
    }

    public void Pause()
    {
        isPaused = true;
    }
    public void UnPause()
    {
        isPaused = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isPaused)
        {
            pDay = day;
            day += daysPerSecond * Time.deltaTime;
        }
        
        Day = Mathf.RoundToInt(day);
        if (PDay != Day)
        {
            PDay= Day;
            this.OnDayComplete.Invoke();
        }
    }
}

using DBModels;
using UnityEngine;

/// <summary>
/// The script for handling information about map selected Nations.
/// </summary>
public class NationUI : MonoBehaviour
{
    private static NationUI instance;
    private DBNation selected;
    private int selectedId;

    private void Awake()
    {
        instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void _Open(int nationId, int gameId)
    {
        selectedId = nationId;
        selected = DatabaseControl.GetNationById(nationId,gameId);

        // Open UI

        // Fill title, description and more information.
    }

    public static void Open(int nationId,int gameId)
    {
        instance._Open(nationId, gameId);
    }
}

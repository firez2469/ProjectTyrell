using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField]
    private Button newGameButton;
    [SerializeField]
    private Button loadGameButton;
    [SerializeField]
    private Button settingsButton;
    [SerializeField]
    private Button exitGameButton;

    [Header("Panels")]
    [SerializeField]
    private Image mainMenuPanel;
    [SerializeField]
    private Image newGamePanel;
    [SerializeField]
    private Image loadGamePanel;
    [SerializeField]
    private Image settingsPanel;

    [Header("Return Options")]
    [SerializeField]
    private Button returnFromNewGame;
    [SerializeField]
    private Button returnFromLoadGame;
    [SerializeField]
    private Button returnFromSettings;

    [Header("New Game Panel")]
    [SerializeField]
    private TMP_InputField gameNameInput;
    [SerializeField]
    private Button createGameButton;

    [Header("Load Game Panel")]
    [SerializeField]
    private GameObject gameItem0;

    private List<GameObject> gameItems;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainMenuPanel.gameObject.SetActive(true);
        newGamePanel.gameObject.SetActive(false);
        loadGamePanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(false);

        newGameButton.onClick.AddListener(NewGamePanelOpen);
        loadGameButton.onClick.AddListener(LoadGamePanelOpen);
        settingsButton.onClick.AddListener(SettingsPanelOpen);
        exitGameButton.onClick.AddListener(ExitGame);

        gameItems = new List<GameObject>() { gameItem0 };

        returnFromNewGame.onClick.AddListener(ReturnFromNewGame);
        returnFromLoadGame.onClick.AddListener(ReturnFromLoadGame);
        returnFromSettings.onClick.AddListener(ReturnFromSettings);

        createGameButton.onClick.AddListener(CreateGame);
    }

    private void NewGamePanelOpen()
    {
        this.mainMenuPanel.gameObject.SetActive(false);
        this.newGamePanel.gameObject.SetActive(true);
    }

    private void LoadGamePanelOpen()
    {
        this.mainMenuPanel.gameObject.SetActive(false);
        this.loadGamePanel.gameObject.SetActive(true);

        DBModels.DB_Game[] games = DatabaseControl.GetListOfGames();
        int i = 0;
        foreach(var game in games)
        {
            if (i < gameItems.Count)
            {
                gameItems[i].GetComponentInChildren<TMP_Text>().text = game.name;
            }
            else
            {
                GameObject newItem = GameObject.Instantiate(gameItem0,gameItem0.transform.parent);
                RectTransform niTransform = newItem.GetComponent<RectTransform>();
                // Offset the item by height times index
                newItem.transform.position += Vector3.down * i * (niTransform.rect.yMax - niTransform.rect.yMin);
                newItem.GetComponentInChildren<TMP_Text>().text = game.name;
                gameItems.Add(newItem);
            }
            i++;
        }
        int leftoverStartIndex = Mathf.Max(i, 1);
        int leftoverCount = gameItems.Count - leftoverStartIndex;

        if (leftoverCount > 0)
        {
            List<GameObject> leftOvers = this.gameItems.GetRange(leftoverStartIndex, leftoverCount);
            foreach (var game in leftOvers)
            {
                Destroy(game);
            }
            this.gameItems.RemoveRange(leftoverStartIndex, leftoverCount);
        }
    }

    private void SettingsPanelOpen()
    {
        this.mainMenuPanel.gameObject.SetActive(false);
        this.settingsPanel.gameObject.SetActive(true);
    }
    private void ExitGame()
    {
        Application.Quit();
    }

    private void ReturnFromNewGame()
    {
        this.newGamePanel.gameObject.SetActive(false);
        this.mainMenuPanel.gameObject.SetActive(true);
    }

    private void ReturnFromLoadGame()
    {
        this.loadGamePanel.gameObject.SetActive(false);
        this.mainMenuPanel.gameObject.SetActive(true);
    }
    private void ReturnFromSettings()
    {
        this.settingsPanel.gameObject.SetActive(false);
        this.mainMenuPanel.gameObject.SetActive(true);
    }

    private void CreateGame()
    {
        string name = this.gameNameInput.text;
        if (name.Length > 0)
        {
            DatabaseControl.CreateNewGame(name, "basic");

            SceneManager.LoadScene(1);
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

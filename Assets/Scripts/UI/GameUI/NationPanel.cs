using UnityEngine;
using UnityEngine.UI;

public class NationPanel : MonoBehaviour
{
    [SerializeField]
    private RectTransform nationPanel;
    [SerializeField]
    private Button flagBtn;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        nationPanel.gameObject.SetActive(false);
        flagBtn.onClick.AddListener(OnFlagClick);
    }

    private void OnFlagClick()
    {
        nationPanel.gameObject.SetActive(true);
        nationPanel.GetComponent<Animator>().SetBool("IsIn", true);
    }

    private void OnExit()
    {
        nationPanel.GetComponent<Animator>().SetBool("IsIn", false);
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            OnExit();
        }
    }
}

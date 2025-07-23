using NUnit.Framework;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ProvinceUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text title;
    [SerializeField]
    private TMP_Text description;
    [SerializeField]
    private Image image;
    [SerializeField]
    private ProvinceArt[] artMaps;
    [SerializeField]
    private RectTransform provinceUI;
    
    private static ProvinceUI instance;

    private int activeTab = 0;

    [SerializeField]
    private Button[] provinceTabs;

    [SerializeField]
    private Image[] tabs;
    [SerializeField]
    private AProvinceMenu[] tabMenus;

    [SerializeField]
    private Color selectedColorEffect;
    private Color tabColor;

    private bool isUpdating = false;

    private _ProvinceMenuProvinceInformation activeInfo;



    // Start is called once
    void Start()
    {
        instance = this;
        _Close();

        for(int i =0; i < provinceTabs.Length; i++)
        {
            int _i = i + 0;
            provinceTabs[i].onClick.AddListener(() =>
            {
                activeTab = _i; 
                tabs[_i].gameObject.SetActive(true);
                for (int j = 0; j < tabs.Length; j++)
                {
                    tabs[j].gameObject.SetActive(j == _i);
                }
                if (activeInfo != null)
                {
                    tabMenus[activeTab].Load(activeInfo);
                }
            }
            );

           

        }
        tabColor = provinceTabs[0].GetComponent<Image>().color;

        
    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i < provinceTabs.Length; i++)
        {
            if (i == activeTab)
            {
                provinceTabs[i].GetComponent<Image>().color = selectedColorEffect;
            }
            else
            {
                ColorBlock clr = provinceTabs[i].colors;
                provinceTabs[i].GetComponent<Image>().color = tabColor;
            }

        }

    }

    public void _Open()
    {
        provinceUI.gameObject.SetActive(true);
        Tile active = Pointer.Selected;
        if(active!= null)
        {
            var tile = DatabaseControl.GetTileById(int.Parse(active.Id), MapDBInitializer.GameId);
            activeInfo = new _ProvinceMenuProvinceInformation(active.name, active.type.ToString(), active.Description, ProvinceArt.Where(this.artMaps, active.type), tile) ;
            this.tabMenus[activeTab].Load(activeInfo);
        }
        foreach(var tab in this.provinceTabs)
        {
            tab.gameObject.SetActive(true);
        }
        for(int i=0; i < tabs.Length; i++)
        {
            tabs[i].gameObject.SetActive(i==activeTab);
        }
        
    }
    public IEnumerator _IterativeOpen()
    {
        isUpdating = true;
        while (isUpdating)
        {
            yield return new WaitForSeconds(1);
            _Open();
            
        }
    }
    private void IterativeOpen()
    {
        StartCoroutine(_IterativeOpen());
    }

    public static void Open()
    {
        instance._Open();
        if (!instance.isUpdating)
        {
            print("Updating...");
            instance.IterativeOpen();
        }
    }
    public void _Close()
    {
        print(provinceUI);
        provinceUI.gameObject.SetActive(false);
        foreach (var tab in this.provinceTabs)
        {
            tab.gameObject.SetActive(false);
        }
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].gameObject.SetActive(false);
        }
        StopCoroutine(_IterativeOpen());
        isUpdating = false;
    }
    public static void Close()
    {
        instance._Close();
    }


    [System.Serializable]
    public class ProvinceArt
    {
        public Tile.TileType type;
        public Sprite image;

        public static Sprite Where(ProvinceArt[] mappings, Tile.TileType type)
        {
            for(int i =0; i < mappings.Length; i++)
            {
                if (mappings[i].type == type)
                {
                    return mappings[i].image;
                }
            }
            return null;
        }
    }
}

using NUnit.Framework;
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

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;
        _Close();
    }

    // Update is called once per frame
    void Update()
    {
           
    }

    public void _Open()
    {
        provinceUI.gameObject.SetActive(true);
        Tile active = Pointer.Selected;
        if(active!= null)
        {
            this.title.text = active.Name;
            this.description.text = active.Description;
            string type = (active.isLand ? "land" : "sea");
            this.image.sprite = ProvinceArt.Where(this.artMaps, type);
        }
        
    }

    public static void Open()
    {
        instance._Open();
    }
    public void _Close()
    {
        print(provinceUI);
        provinceUI.gameObject.SetActive(false);
    }
    public static void Close()
    {
        instance._Close();
    }


    [System.Serializable]
    public class ProvinceArt
    {
        public string type;
        public Sprite image;

        public static Sprite Where(ProvinceArt[] mappings, string type)
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

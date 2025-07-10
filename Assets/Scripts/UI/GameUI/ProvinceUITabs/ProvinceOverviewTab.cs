using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProvinceOverviewTab : AProvinceMenu
{
    [SerializeField]
    private Image image;

    [SerializeField]
    private TMP_Text title;
    [SerializeField]
    private TMP_Text description;
    [SerializeField]
    private TMP_Text type;

    public override void Load(_ProvinceMenuProvinceInformation information)
    {
        this.title.text = information.ProvinceName;
        this.type.text = information.ProvinceType;
        this.description.text = information.ProvinceDescription;
    }

}

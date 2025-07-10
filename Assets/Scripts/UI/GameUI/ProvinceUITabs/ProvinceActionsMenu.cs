using TMPro;
using UnityEngine;

public class ProvinceActionsMenu : AProvinceMenu
{
    [SerializeField]
    private TMP_Text title;
    [SerializeField]
    private TMP_Text type;
    public override void Load(_ProvinceMenuProvinceInformation information)
    {
        this.title.text = information.ProvinceName;
        this.type.text = information.ProvinceType;
    }


}

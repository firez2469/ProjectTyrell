using TMPro;
using UnityEngine;

public class ProvinceResourceMenuTab : AProvinceMenu
{
    [SerializeField]
    private TMP_Text title;
    [SerializeField]
    private TMP_Text type;

    [SerializeField]
    private TMP_Text informationText;

    public override void Load(_ProvinceMenuProvinceInformation information)
    {
        print("Updating province information...");
        this.title.text = information.ProvinceName;
        this.type.text = information.ProvinceType;

        this.informationText.text =
            $"<b>Population:</b> {information.info.population.ToString("N0")}\n" +
            $"<b>Industry:</b> {information.info.infrastructureRating}\n" +
            $"<b>Stability:</b> {information.info.stability}%\n" +
            $"<b>Factories:</b> {information.info.factories}";

    }
}

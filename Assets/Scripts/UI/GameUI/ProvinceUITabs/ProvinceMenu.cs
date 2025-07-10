using UnityEngine;


public abstract class AProvinceMenu : MonoBehaviour
{
    public abstract void Load(_ProvinceMenuProvinceInformation information);
}

public class ProvinceMenu : AProvinceMenu
{
    public override void Load(_ProvinceMenuProvinceInformation information)
    {
        throw new System.NotImplementedException();
    }
}

public class _ProvinceMenuProvinceInformation
{
    public string ProvinceName { get; private set; }

    public string ProvinceType { get; private set; }
    public string ProvinceDescription { get; private set; }    
    public Sprite ProvinceIcon { get; private set; }
    

    public _ProvinceMenuProvinceInformation(string name, string type, string description, Sprite icon)
    {
        ProvinceName = name;
        ProvinceType = type;
        ProvinceDescription = description;
        ProvinceIcon = icon;
    }
}
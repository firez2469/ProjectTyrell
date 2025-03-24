using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public string Name;
    public string Description;
    public string Id;
    public List<string> neighbors;
    public bool isLand;

    private LineRenderer outline;

    
    private Gradient enabledGradient;
    private Gradient disabledGradient;

    public Color disabledColor;
    
    private void Start()
    {
        outline = gameObject.GetComponentInChildren<LineRenderer>();
        //outline.enabled = false;

        disabledGradient = new Gradient();
        GradientColorKey[] keys = disabledGradient.colorKeys;
        GradientAlphaKey[] alphaKeys = disabledGradient.alphaKeys;
        for(int i =0; i < keys.Length; i++)
        {
            
            keys[i].color = disabledColor;
            alphaKeys[i].alpha = disabledColor.a;
        }
        disabledGradient.SetKeys(keys, alphaKeys);
        enabledGradient = outline.colorGradient;
        outline.colorGradient = disabledGradient;
        
    }

    public void Show()
    {
        //outline.enabled = true;
        outline.colorGradient = enabledGradient;
        outline.sortingOrder += 1;
        
    }
    public void Hide()
    {
        //outline.enabled = false;
        outline.colorGradient = disabledGradient;
        outline.sortingOrder -= 1;
        
    }

    private void Thick()
    {
        for (int i = 0; i < outline.widthCurve.length; i++)
        {
            Keyframe k = outline.widthCurve[i];
            k.value *= 5f;
            
            outline.widthCurve.MoveKey(i, k);
        }
    }
    private void Thin()
    {
        for (int i = 0; i < outline.widthCurve.length; i++)
        {
            Keyframe k = outline.widthCurve[i];
            k.value /= 5f;
            outline.widthCurve.MoveKey(i, k);
        }
    }

}

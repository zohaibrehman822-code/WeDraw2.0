using UnityEngine;
using UnityEngine.UI;

public class Mode3ColorButton : MonoBehaviour
{
    public Mode3Manager mode3manager;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            mode3manager.SetBrushColor(GetComponent<Image>().color);
        });
    }
}

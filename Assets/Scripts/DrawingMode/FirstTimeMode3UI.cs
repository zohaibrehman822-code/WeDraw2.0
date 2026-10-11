using UnityEngine;
using UnityEngine.EventSystems;

public class FirstTimeMode3UI : MonoBehaviour
{
    [Header("UI Panel")]
    public GameObject tutorialPanel; // Drag your UI Image panel here

    private string prefKey = "PlayingMode3FirstTime";

    void Start()
    {
        int isFirstTime = PlayerPrefs.GetInt(prefKey, 0);

        if (isFirstTime == 0)
        {
            tutorialPanel.SetActive(true);
        }
        else
        {
            tutorialPanel.SetActive(false);
        }
    }

    // Detect click/touch anywhere on panel
    public void OnPointerClick(PointerEventData eventData)
    {
        CloseTutorialofMode3();
    }

    public void CloseTutorialofMode3()
    {
        tutorialPanel.SetActive(false);

        PlayerPrefs.SetInt(prefKey, 1);
        PlayerPrefs.Save();

        Debug.Log("Tutorial closed and saved ✅");
    }
}

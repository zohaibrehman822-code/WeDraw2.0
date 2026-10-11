using UnityEngine;
using UnityEngine.EventSystems;

public class FirstTimeImageFillUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Panel")]
    public GameObject tutorialPanel; // Drag your UI Image panel here

    private string prefKey = "PlayingImageFillFirstTime";

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
        CloseTutorialofImageFill();
    }

    public void CloseTutorialofImageFill()
    {
        tutorialPanel.SetActive(false);

        PlayerPrefs.SetInt(prefKey, 1);
        PlayerPrefs.Save();

        Debug.Log("Tutorial closed and saved ✅");
    }
}
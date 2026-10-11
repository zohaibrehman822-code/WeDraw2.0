using UnityEngine;
using UnityEngine.EventSystems;

public class TutorialPanelController : MonoBehaviour, IPointerClickHandler
{
    [Header("Tutorial Images")]
    public GameObject[] tutorialImages;

    private int currentIndex = 0;
    private string prefKey = "PlayingDrawingFirstTime";

    void OnEnable()
    {
        currentIndex = 0;
        ShowImage(currentIndex);
    }

    void ShowImage(int index)
    {
        for (int i = 0; i < tutorialImages.Length; i++)
        {
            tutorialImages[i].SetActive(i == index);
        }
    }

    // 👉 Tap on panel
    public void OnPointerClick(PointerEventData eventData)
    {
        HandleNext();
    }

    void HandleNext()
    {
        if (currentIndex < tutorialImages.Length - 1)
        {
            currentIndex++;
            ShowImage(currentIndex);
        }
        else
        {
            CloseTutorial();
        }
    }

    void CloseTutorial()
    {
        gameObject.SetActive(false);

        PlayerPrefs.SetInt(prefKey, 1);
        PlayerPrefs.Save();

        Debug.Log("Tutorial finished and saved ✅");
    }
}
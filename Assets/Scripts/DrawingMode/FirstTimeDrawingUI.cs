using UnityEngine;
using UnityEngine.EventSystems;

public class FirstTimeDrawingUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Panel")]
    public GameObject tutorialPanel;

    [Header("Tutorial Images")]
    public GameObject[] tutorialImages;

    [Header("Buttons")]
    public GameObject nextButton;
    public GameObject closeButton;

    private int currentIndex = 0;
    private string prefKey = "PlayingDrawingFirstTime";

    void Start()
    {
        int isFirstTime = PlayerPrefs.GetInt(prefKey, 0);

        if (isFirstTime == 0)
        {
            tutorialPanel.SetActive(true);
            currentIndex = 0;

            ShowImage(currentIndex);

            nextButton.SetActive(true);
            closeButton.SetActive(false);
        }
        else
        {
            tutorialPanel.SetActive(false);
        }
    }

    void ShowImage(int index)
    {
        for (int i = 0; i < tutorialImages.Length; i++)
        {
            tutorialImages[i].SetActive(i == index);
        }

        if (index == tutorialImages.Length - 1)
        {
            nextButton.SetActive(false);
            closeButton.SetActive(true);
        }
        else
        {
            nextButton.SetActive(true);
            closeButton.SetActive(false);
        }
    }

    // ✅ Button click
    public void NextStep()
    {
        HandleNext();
    }

    // ✅ Screen click (PC + Mobile)
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Clicked");
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
            CloseTutorial(); // ✅ auto close on last
        }
    }

    public void CloseTutorial()
    {
        tutorialPanel.SetActive(false);

        PlayerPrefs.SetInt(prefKey, 1);
        PlayerPrefs.Save();

        Debug.Log("Tutorial finished and saved ✅");
    }
}
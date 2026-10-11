using UnityEngine;

public class FirstTimeDrawingChecker : MonoBehaviour
{
    public GameObject tutorialPanel;

    private string prefKey = "PlayingDrawingFirstTime";

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
}
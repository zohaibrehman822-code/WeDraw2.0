using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject LoadingPanel;
    public GameObject MainMenuPanel;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Game Manager Started");
        LoadingPanel.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void LoadMainMenu()
    {
        Debug.Log("Loading Main Menu");
        // Here you would typically load the main menu scene
        // For example, using UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        LoadingPanel.SetActive(false);
        MainMenuPanel.SetActive(true);
    }
}

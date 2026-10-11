using UnityEngine;

public class LevelSelection : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Level Selection Script Started");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SelectLevel1()
    {
        PlayerPrefs.SetInt("SelectedLevel", 1);
        PlayerPrefs.Save(); // Optional but good practice
    }

    public void SelectLevel2()
    {
        PlayerPrefs.SetInt("SelectedLevel", 2);
        PlayerPrefs.Save(); // Optional but good practice
    }

    public void SelectLevel3()
    {
        PlayerPrefs.SetInt("SelectedLevel", 3);
        PlayerPrefs.Save(); // Optional but good practice
    }

    public void SelectLevel4()
    {
        PlayerPrefs.SetInt("SelectedLevel", 4);
        PlayerPrefs.Save(); // Optional but good practice
    }

    public void SelectLevel5()
    {
        PlayerPrefs.SetInt("SelectedLevel", 5);
        PlayerPrefs.Save(); // Optional but good practice
    }
}

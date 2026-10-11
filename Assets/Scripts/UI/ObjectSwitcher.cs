using UnityEngine;

public class ObjectSwitcher : MonoBehaviour
{
    [Header("Drag your objects here")]
    public GameObject[] objects;

    private int currentIndex = 0;

    void Start()
    {
        // Make sure only the first object is active at start
        UpdateActiveObject();
    }

    public void NextObject()
    {
        currentIndex++;
        if (currentIndex >= objects.Length)
            currentIndex = 0; // Wrap around to first
        UpdateActiveObject();
    }

    public void PreviousObject()
    {
        currentIndex--;
        if (currentIndex < 0)
            currentIndex = objects.Length - 1; // Wrap around to last
        UpdateActiveObject();
    }

    private void UpdateActiveObject()
    {
        for (int i = 0; i < objects.Length; i++)
        {
            objects[i].SetActive(i == currentIndex);
        }
    }
}

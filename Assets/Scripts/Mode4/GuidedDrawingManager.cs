using UnityEngine;

/// <summary>
/// Attach to a Manager object. Drag your drawing PREFABS from the Project window
/// (Letter-A, Letter-B, Digit-1 ...) into the array. OpenDrawing(index) creates the
/// selected one under the Container and closes the previous one.
/// Every problem is printed in the Console, so nothing fails silently.
/// </summary>
public class GuidedDrawingManager : MonoBehaviour
{
    [Header("Drawings")]
    [Tooltip("Drag the PREFABS from the Project window here. The order is the index (0 = first).")]
    [SerializeField] GameObject[] drawingPrefabs;

    [Tooltip("Where the drawings are created, for example an empty 'DrawingContainer' (stretched) inside your panel. If empty, the Canvas is used.")]
    [SerializeField] RectTransform container;

    [Tooltip("Drawing to open when the game starts. -1 = open none.")]
    [SerializeField] int openOnStart = -1;

    [Header("Performance")]
    [Tooltip("Create every drawing at start (hidden). Slower start, but opening is instant afterwards.")]
    [SerializeField] bool createAllAtStart = false;

    [Tooltip("Destroy a drawing when it is closed, to free memory. Off = it is kept and reused, which is smoother.")]
    [SerializeField] bool destroyWhenClosed = false;

    [Header("Debug")]
    [SerializeField] bool logSteps = true;

    GameObject[] instances;
    bool initialized;
    int currentIndex = -1;

    public int CurrentIndex => currentIndex;
    public int Count => drawingPrefabs != null ? drawingPrefabs.Length : 0;

    void Awake()
    {
        EnsureInit();
    }

    void Start()
    {
        if (createAllAtStart) CreateAll();

        if (openOnStart >= 0) OpenDrawing(openOnStart);
        else CloseCurrent();
    }

    // Safe to call at any time, even if this object was switched off until now.
    void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        instances = new GameObject[Count];

        if (container == null)
        {
            container = transform as RectTransform;
            if (container == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    container = canvas.transform as RectTransform;
                    Debug.LogWarning($"GuidedDrawingManager: Container is empty, so the Canvas '{canvas.name}' is used. " +
                                     "Drag an empty UI object from your panel into Container to control where drawings appear.", this);
                }
            }
        }

        if (container == null)
            Debug.LogError("GuidedDrawingManager: no Container and no Canvas found. Drawings cannot be created.", this);
    }

    /// <summary>Opens the drawing at this index (0 = first prefab) and closes the previous one.</summary>
    public void OpenDrawing(int index)
    {
        EnsureInit();

        if (drawingPrefabs == null || drawingPrefabs.Length == 0)
        {
            Debug.LogError("GuidedDrawingManager: the Drawing Prefabs array is empty. Drag your prefabs from the Project window into it.", this);
            return;
        }

        if (index < 0 || index >= drawingPrefabs.Length)
        {
            Debug.LogError($"GuidedDrawingManager: index {index} is out of range (0 to {drawingPrefabs.Length - 1}).", this);
            return;
        }

        if (container == null)
        {
            Debug.LogError("GuidedDrawingManager: there is no Container to create the drawing in.", this);
            return;
        }

        // Close the current one first. Opening the same index again restarts it.
        CloseCurrent();

        GameObject go = GetOrCreate(index);
        if (go == null) return;

        go.transform.SetParent(container, false);
        go.SetActive(true);
        currentIndex = index;

        if (logSteps)
            Debug.Log($"GuidedDrawingManager: opened '{go.name}' (index {index}) inside '{container.name}'.", go);
    }

    /// <summary>Opens the next drawing. After the last one it goes back to the first.</summary>
    public void OpenNext()
    {
        if (Count == 0) return;
        OpenDrawing((currentIndex + 1) % Count);
    }

    /// <summary>Closes the open drawing.</summary>
    public void CloseAll()
    {
        EnsureInit();
        CloseCurrent();
    }

    // ---------- internals ----------

    void CloseCurrent()
    {
        if (currentIndex >= 0 && instances != null && currentIndex < instances.Length && instances[currentIndex] != null)
        {
            if (destroyWhenClosed)
            {
                Destroy(instances[currentIndex]);
                instances[currentIndex] = null;
            }
            else
            {
                instances[currentIndex].SetActive(false);
            }
        }
        currentIndex = -1;
    }

    GameObject GetOrCreate(int index)
    {
        if (instances[index] != null) return instances[index];

        GameObject prefab = drawingPrefabs[index];
        if (prefab == null)
        {
            Debug.LogError($"GuidedDrawingManager: slot {index} in Drawing Prefabs is empty.", this);
            return null;
        }

        GameObject go = Instantiate(prefab, container, false);
        go.name = prefab.name;
        instances[index] = go;
        return go;
    }

    // Creates every drawing under an inactive holder, so nothing switches on or draws yet.
    void CreateAll()
    {
        if (container == null) return;

        var holder = new GameObject("DrawingPool", typeof(RectTransform));
        holder.transform.SetParent(container, false);
        holder.SetActive(false);

        for (int i = 0; i < Count; i++)
        {
            if (drawingPrefabs[i] == null || instances[i] != null) continue;
            GameObject go = Instantiate(drawingPrefabs[i], holder.transform, false);
            go.name = drawingPrefabs[i].name;
            instances[i] = go;
        }
    }
}
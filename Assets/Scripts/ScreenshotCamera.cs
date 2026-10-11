using UnityEngine;
using System.IO;

public class ScreenshotCamera : MonoBehaviour
{
    public Camera screenshotCam;
    public string fileName = "DrawingScreenshot.png";

    public void CaptureScreenshot()
    {
        if (screenshotCam == null) return;

        // Render the camera to a RenderTexture
        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        screenshotCam.targetTexture = rt;

        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenshotCam.Render();

        RenderTexture.active = rt;
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply();

        screenshotCam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        // Save to persistent path
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllBytes(path, screenshot.EncodeToPNG());

        Debug.Log("Screenshot saved at: " + path);
    }
}

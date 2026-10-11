using UnityEngine;
using UnityEngine.UI;

public class DrawingTarget : MonoBehaviour
{
    public RawImage drawingArea;
    public PolygonCollider2D drawingBoundaryCollider;

    public RawImage GetDrawingArea() => drawingArea;
    public PolygonCollider2D GetCollider() => drawingBoundaryCollider;
}

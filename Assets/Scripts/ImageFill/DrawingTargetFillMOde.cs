using UnityEngine;
using UnityEngine.UI;
public class DrawingTargetFillMOde : MonoBehaviour
{
    [Header("Shared Drawing Texture")]
    public RawImage drawingArea;
    [Header("Region Colliders")]
    public PolygonCollider2D[] drawingBoundaryColliders;
    public RawImage GetDrawingArea() => drawingArea;
    public PolygonCollider2D[] GetAllColliders() => drawingBoundaryColliders;
}

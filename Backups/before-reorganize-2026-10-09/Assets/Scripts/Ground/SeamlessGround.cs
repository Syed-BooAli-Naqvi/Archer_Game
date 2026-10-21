using UnityEngine;

/// <summary>
/// Keeps a Tiled Sprite Renderer seamless no matter how you scale the GameObject.
///
/// Problem: Sprite Renderer's "Tiled" draw mode repeats the sprite to fill its
/// Size (Width/Height) fields, which live in LOCAL space. If you then scale the
/// Transform, that scale multiplies on top of Size and stretches every tile,
/// breaking the seamless look.
///
/// Fix: this script detects any change to the Transform's local scale (from
/// dragging the scale gizmo, animation, another script, etc.), folds that scale
/// into the Sprite Renderer's Size instead, and resets localScale back to
/// (1,1,1). The visual size stays identical, but the tiling density never
/// changes, so it stays seamless.
///
/// Works both in Edit Mode (dragging handles in the Scene view) and at Runtime
/// (e.g. procedurally resizing ground pieces).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class SeamlessTiledSprite : MonoBehaviour
{
    [Tooltip("If the SpriteRenderer's Draw Mode is not already Tiled, force it to Tiled on enable.")]
    [SerializeField] private bool forceTiledDrawMode = true;

    [Tooltip("Minimum size (in world units) the sprite is allowed to shrink to, per axis.")]
    [SerializeField] private Vector2 minSize = new Vector2(0.01f, 0.01f);

    private SpriteRenderer _spriteRenderer;
    private Vector3 _lastScale = Vector3.one;

    private void OnEnable()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (forceTiledDrawMode && _spriteRenderer.drawMode != SpriteDrawMode.Tiled)
        {
            _spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        }

        // If the object already has a non-1 scale when this component is added
        // (e.g. added to an existing stretched object), fold that in immediately.
        AbsorbCurrentScale();
    }

    private void Update()
    {
        // Detect any scale change since last frame (Inspector drag, animation,
        // another script calling transform.localScale = ..., etc.)
        if (transform.localScale != _lastScale)
        {
            AbsorbCurrentScale();
        }
    }

    /// <summary>
    /// Folds the current localScale into the SpriteRenderer's tiled Size,
    /// then resets localScale to (1,1,1) so the visible result is identical
    /// but the tiling itself is never stretched.
    /// </summary>
    private void AbsorbCurrentScale()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        Vector3 scale = transform.localScale;

        // Avoid divide-by-zero / degenerate scale.
        if (Mathf.Approximately(scale.x, 0f)) scale.x = 0.0001f;
        if (Mathf.Approximately(scale.y, 0f)) scale.y = 0.0001f;

        Vector2 currentSize = _spriteRenderer.size;

        // Multiply the existing size by however much the scale changed,
        // using absolute value so a negative (flip) scale doesn't negate size.
        Vector2 newSize = new Vector2(
            currentSize.x * Mathf.Abs(scale.x),
            currentSize.y * Mathf.Abs(scale.y)
        );

        newSize.x = Mathf.Max(newSize.x, minSize.x);
        newSize.y = Mathf.Max(newSize.y, minSize.y);

        _spriteRenderer.size = newSize;

        // Preserve flip direction via sign, but strip out the magnitude.
        transform.localScale = new Vector3(
            Mathf.Sign(scale.x == 0f ? 1f : scale.x),
            Mathf.Sign(scale.y == 0f ? 1f : scale.y),
            1f
        );

        _lastScale = transform.localScale;
    }

    /// <summary>
    /// Call this from other scripts to resize seamlessly instead of touching
    /// transform.localScale directly. worldSize is in world units (width, height).
    /// </summary>
    public void SetWorldSize(Vector2 worldSize)
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        worldSize.x = Mathf.Max(Mathf.Abs(worldSize.x), minSize.x);
        worldSize.y = Mathf.Max(Mathf.Abs(worldSize.y), minSize.y);

        _spriteRenderer.size = worldSize;
        transform.localScale = new Vector3(transform.localScale.x >= 0f ? 1f : -1f,
                                            transform.localScale.y >= 0f ? 1f : -1f,
                                            1f);
        _lastScale = transform.localScale;
    }
}
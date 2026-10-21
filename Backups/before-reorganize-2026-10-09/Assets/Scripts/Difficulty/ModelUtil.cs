using UnityEngine;

/// <summary>Helpers for runtime-built pickups: load a model from Resources at a set size, and a plain white sprite.</summary>
public static class ModelUtil
{
    private static Sprite whiteSprite;

    /// <summary>
    /// Instantiates Resources/'path' inside a new holder so that it is 'height' tall and its base sits at the
    /// holder's origin. Returns the holder (or null if the model is missing).
    /// </summary>
    public static Transform InstantiateWithHeight(string path, float height, string name)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
            return null;

        Transform holder = new GameObject(name).transform;
        GameObject model = Object.Instantiate(prefab, holder);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return holder;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        float scale = height / Mathf.Max(0.0001f, bounds.size.y);
        model.transform.localScale *= scale;
        model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
        return holder;
    }

    /// <summary>A 4x4 white sprite (for simple coloured bars drawn with SpriteRenderers).</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D tex = new Texture2D(4, 4);
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }

            return whiteSprite;
        }
    }
}

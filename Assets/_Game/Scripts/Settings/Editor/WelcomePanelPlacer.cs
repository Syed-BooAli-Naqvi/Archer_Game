using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the "Welcome, Archer!" panel into the open scene so it can be edited there like any other UI.
/// Runs once by itself when the gameplay scene is open; after that use Tools > Archery > Place Welcome Panel In Scene.
/// </summary>
[InitializeOnLoad]
public static class WelcomePanelPlacer
{
    private const string GameplayScene = "My Archery Gameplay";
    private const string AutoPlacedKey = "ArcheryGame.WelcomePanelAutoPlaced";

    static WelcomePanelPlacer()
    {
        EditorApplication.delayCall += AutoPlaceOnce;
    }

    private static void AutoPlaceOnce()
    {
        if (EditorPrefs.GetBool(AutoPlacedKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != GameplayScene)
            return;

        EditorPrefs.SetBool(AutoPlacedKey, true);
        if (Object.FindFirstObjectByType<WelcomePanel>(FindObjectsInactive.Include) != null)
            return;

        bool hadUnsavedChanges = scene.isDirty;
        Place();

        // Only save when that can't sweep up someone's unsaved work.
        if (!hadUnsavedChanges)
            EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Archery/Place Welcome Panel In Scene")]
    public static void PlaceFromMenu()
    {
        WelcomePanel existing = Object.FindFirstObjectByType<WelcomePanel>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("Welcome Panel is already in the scene (selected). Delete it first to build a fresh one.");
            return;
        }

        Place();
    }

    private static void Place()
    {
        DifficultySettings settings = DifficultySettings.Current;
        TMP_FontAsset font = settings != null ? settings.damageNumberFont : null;
        Sprite frameSprite = settings != null ? settings.welcomeFrameSprite : null;
        Sprite buttonSprite = settings != null ? settings.welcomeButtonSprite : null;

        GameObject root = new GameObject("Welcome Panel", typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        Undo.RegisterCreatedObjectUndo(root, "Place Welcome Panel");

        // Own overlay canvas on top of everything, portrait layout.
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();
        CanvasGroup group = root.AddComponent<CanvasGroup>();

        // Dim background (also blocks taps on the menu behind it)
        Image dim = NewImage("Dim", root.transform, null, new Color(0f, 0f, 0f, 0.7f));
        Stretch(dim.rectTransform);

        // Window
        Image frame = NewImage("Window", dim.transform, frameSprite, Color.white);
        RectTransform window = frame.rectTransform;
        window.sizeDelta = new Vector2(860f, 1000f);
        if (frameSprite == null)
            frame.color = new Color(0.12f, 0.16f, 0.3f, 1f);

        NewText("Title", window, font, "WELCOME, ARCHER!", 78f, new Color(1f, 0.85f, 0.3f), new Vector2(0f, 380f), new Vector2(800f, 120f));

        string howTo =
            "<b>DRAG</b> left and right to move\n\n" +
            "Shoot <color=#5fc8ff><b>BLUE GATES</b></color> for arrow powers\n\n" +
            "<color=#ff6060><b>RED GATES</b></color> take powers away\n\n" +
            "Smash the <color=#ffcf40><b>2X BARREL</b></color> for a double volley\n\n" +
            "Defeat the <b>BOSS</b> to win!";

        TMP_Text body = NewText("How To Play", window, font, howTo, 44f, Color.white, new Vector2(0f, 60f), new Vector2(740f, 480f));
        body.lineSpacing = -10f;

        TMP_Text gift = NewText("Gift", window, font, "Starter gift: <color=#ffcf40>100 COINS</color>", 52f, Color.white, new Vector2(0f, -235f), new Vector2(760f, 80f));

        // Button
        Image buttonImage = NewImage("Lets Go Button", window, buttonSprite, Color.white);
        if (buttonSprite == null)
            buttonImage.color = new Color(0.3f, 0.8f, 0.3f, 1f);

        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.sizeDelta = new Vector2(440f, 150f);
        buttonRect.anchoredPosition = new Vector2(0f, -370f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        TMP_Text label = NewText("Label", buttonRect, font, "COLLECT", 64f, Color.white, new Vector2(0f, 6f), new Vector2(440f, 150f));

        root.AddComponent<WelcomePanel>().Wire(group, window, button, gift, label);

        // Hidden until the game shows it.
        root.SetActive(false);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Welcome Panel placed in the scene (inactive until the game shows it).");
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        if (sprite != null && sprite.border != Vector4.zero)
            image.type = Image.Type.Sliced;
        return image;
    }

    private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, Vector2 position, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        RectTransform rt = label.rectTransform;
        rt.sizeDelta = box;
        rt.anchoredPosition = position;
        return label;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools/Create 2X Booster Button: adds the 2X booster button (disabled) under Canvas/Gameplay in the
/// gameplay scene and saves the scene. Safe to run again: it does nothing if the button already exists.
/// </summary>
public static class BoosterButtonCreator
{
    const string ScenePath = "Assets/_Game/Scenes/My Archery Gameplay.unity";

    [MenuItem("Tools/Create 2X Booster Button")]
    static void Menu()
    {
        string result = Create();
        Debug.Log("[BoosterButtonCreator] " + result);
    }

    static string Create()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;

        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            MainMenuUIHandler ui = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ui = root.GetComponentInChildren<MainMenuUIHandler>(true);
                if (ui != null) break;
            }

            if (ui == null || ui.gameplayObj == null)
                return "FAILED: MainMenuUIHandler / gameplayObj not found in " + ScenePath;

            Transform gameplay = ui.gameplayObj.transform;

            BoosterButton existing = gameplay.GetComponentInChildren<BoosterButton>(true);
            if (existing != null)
                return "Already exists: " + GetPath(existing.transform);

            DifficultySettings settings = Resources.Load<DifficultySettings>("DifficultySettings");
            TMP_FontAsset font = settings != null ? settings.damageNumberFont : null;

            GameObject go = new GameObject("2X Booster Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoosterButton));
            Undo.RegisterCreatedObjectUndo(go, "Create 2X Booster Button");
            go.layer = gameplay.gameObject.layer;
            go.transform.SetParent(gameplay, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-150f, 200f);   // bottom-right, below the archer
            rect.sizeDelta = new Vector2(190f, 229f);

            Image image = go.GetComponent<Image>();
            image.sprite = settings != null ? settings.boosterButtonSprite : null;
            image.preserveAspect = true;
            if (image.sprite == null)
                image.color = new Color(0.2f, 0.55f, 1f);

            go.GetComponent<Button>().targetGraphic = image;

            Label(rect, "2X", 92f, new Vector2(0f, 14f), font, Color.white);
            Label(rect, "ARROWS", 34f, new Vector2(0f, -52f), font, Color.white);
            TMP_Text badge = Label(rect, "x2", 40f, new Vector2(70f, 90f), font, new Color(1f, 0.9f, 0.3f));
            badge.name = "Charge Badge";
            badge.gameObject.SetActive(false);

            go.GetComponent<BoosterButton>().chargeBadge = badge;
            go.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Created: " + GetPath(go.transform) + " (disabled), scene saved";
        }
        finally
        {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static TMP_Text Label(RectTransform parent, string text, float size, Vector2 position, TMP_FontAsset font, Color color)
    {
        GameObject go = new GameObject(text == "x2" ? "Charge Badge" : text + " Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 110f);
        rect.anchoredPosition = position;
        return label;
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}

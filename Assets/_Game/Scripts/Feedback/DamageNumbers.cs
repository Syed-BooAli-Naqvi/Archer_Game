using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Floating "-50" style damage numbers that pop, rise and fade. Pooled; creates itself on first use.
/// </summary>
public class DamageNumbers : MonoBehaviour
{
    public enum Style { Normal, Fire, Frost, Boss }

    // Top/bottom gradient colours per style (game-style yellow-orange like mobile shooter ads).
    private static readonly Color[][] Gradients =
    {
        new[] { new Color(1f, 0.97f, 0.45f), new Color(1f, 0.55f, 0.08f) },   // Normal
        new[] { new Color(1f, 0.75f, 0.25f), new Color(0.95f, 0.22f, 0.05f) }, // Fire
        new[] { new Color(0.85f, 1f, 1f), new Color(0.2f, 0.65f, 1f) },       // Frost
        new[] { new Color(1f, 1f, 0.55f), new Color(1f, 0.45f, 0f) },         // Boss
    };

    private class Entry
    {
        public TextMeshPro text;
        public Vector3 start;
        public float age;
        public float size;
        public bool active;
        public int key;
        public float total;
    }

    private static DamageNumbers instance;

    private readonly List<Entry> entries = new List<Entry>();

    private const float Life = 0.7f;
    private const float Rise = 1.3f;
    private const int MaxEntries = 60;

    private const float MergeWindow = 0.25f;

    /// <param name="key">Hits on the same target (key) within a quarter second add up into one number.</param>
    public static void Show(Vector3 position, float amount, Style style, float size = 1f, int key = 0)
    {
        if (!DifficultySettings.Current.DamageNumbersActive || amount <= 0f)
            return;

        if (instance == null)
            instance = new GameObject("Damage Numbers").AddComponent<DamageNumbers>();

        if (key != 0)
        {
            Entry recent = instance.entries.Find(e => e.active && e.key == key && e.age < MergeWindow);

            if (recent != null)
            {
                recent.total += amount;
                recent.text.text = "-" + Mathf.RoundToInt(recent.total);
                ApplyStyle(recent.text, style);
                recent.age = 0f;
                return;
            }
        }

        instance.Spawn(position, amount, style, size, key);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private static void ApplyStyle(TextMeshPro text, Style style)
    {
        Color[] g = Gradients[(int)style];
        text.color = Color.white;
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(g[0], g[0], g[1], g[1]);
    }

    /// <summary>Floating text such as "+2 ARROWS" (same look as damage numbers, not merged).</summary>
    public static void ShowText(Vector3 position, string message, Style style, float size = 1.2f)
    {
        if (!DifficultySettings.Current.enabled)
            return;

        if (instance == null)
            instance = new GameObject("Damage Numbers").AddComponent<DamageNumbers>();

        instance.Spawn(position, 0f, style, size, 0).text.text = message;
    }

    private Entry Spawn(Vector3 position, float amount, Style style, float size, int key)
    {
        Entry entry = entries.Find(e => !e.active);

        if (entry == null)
        {
            if (entries.Count >= MaxEntries)
            {
                // Reuse the oldest one.
                entry = entries[0];
                entries.RemoveAt(0);
                entries.Add(entry);
            }
            else
            {
                entry = new Entry { text = CreateText() };
                entries.Add(entry);
            }
        }

        entry.active = true;
        entry.age = 0f;
        entry.size = size;
        entry.key = key;
        entry.total = amount;
        entry.start = position + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0f, 0.3f), 0f);
        entry.text.gameObject.SetActive(true);
        entry.text.text = "-" + Mathf.RoundToInt(amount);
        ApplyStyle(entry.text, style);
        entry.text.alpha = 1f;
        entry.text.transform.position = entry.start;
        return entry;
    }

    private TextMeshPro CreateText()
    {
        GameObject go = new GameObject("Damage Number");
        go.transform.SetParent(transform, false);

        TextMeshPro text = go.AddComponent<TextMeshPro>();
        TMP_FontAsset font = DifficultySettings.Current.damageNumberFont;
        if (font != null)
            text.font = font;

        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 3.6f;
        text.characterSpacing = -4f;
        text.rectTransform.sizeDelta = new Vector2(6f, 2f);
        text.outlineWidth = 0.32f;
        text.outlineColor = new Color32(60, 22, 4, 255);   // dark brown outline reads better than black on grass
        text.sortingOrder = 100;
        return text;
    }

    private void Update()
    {
        Camera cam = Camera.main;

        foreach (Entry entry in entries)
        {
            if (!entry.active)
                continue;

            entry.age += Time.deltaTime;
            float t = entry.age / Life;

            if (t >= 1f)
            {
                entry.active = false;
                entry.text.gameObject.SetActive(false);
                continue;
            }

            float rise = 1f - (1f - t) * (1f - t);
            entry.text.transform.position = entry.start + Vector3.up * Rise * rise;

            float pop = t < 0.12f ? Mathf.Lerp(0.4f, 1.35f, t / 0.12f)
                      : t < 0.25f ? Mathf.Lerp(1.35f, 1f, (t - 0.12f) / 0.13f) : 1f;
            entry.text.transform.localScale = Vector3.one * pop * entry.size;
            entry.text.alpha = t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;

            if (cam != null)
                entry.text.transform.rotation = cam.transform.rotation;
        }
    }
}

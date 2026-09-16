using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Soundheim;

// Uses Valheim's tooltip behavior and font with a consistent, plain-text panel.
internal sealed class AudioDeviceTooltips(GameObject prefab, UITooltip selected)
{
    internal static AudioDeviceTooltips? Create(TMP_Dropdown dropdown, Transform parent)
    {
        TMP_Text style = dropdown.captionText;
        if (style == null) return null;
        // Loaded native tooltip prefabs differ between the menu and a world.
        // Own the hierarchy that UITooltip fills and AudioTooltipSize measures.
        var prefab = new GameObject("Audio device tooltip template", typeof(RectTransform));
        prefab.SetActive(false);
        prefab.transform.SetParent(parent, false);
        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(prefab.transform, false);
        var panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0, 1);
        panel.anchoredPosition = new Vector2(16, -16);
        panel.sizeDelta = new Vector2(400, 0);
        var background = panelObject.GetComponent<Image>();
        background.color = new Color(0, 0, 0, 0.9f);
        background.raycastTarget = false;
        var layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 4;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panelObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text topic = CreateLabel(panel, style, "Topic", style.color, TextAlignmentOptions.Center);
        TMP_Text text = CreateLabel(panel, style, "Text", Color.white, TextAlignmentOptions.Left);
        prefab.AddComponent<AudioTooltipSize>().Initialize(panel, layout, topic, text);
        UITooltip selected = Attach(dropdown.gameObject, prefab, style.text);
        return new AudioDeviceTooltips(prefab, selected);
    }

    private static TMP_Text CreateLabel(Transform parent, TMP_Text style, string name, Color color, TextAlignmentOptions alignment)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSharedMaterial = style.fontSharedMaterial;
        text.fontSize = style.fontSizeMax;
        text.color = color;
        text.alignment = alignment;
        text.richText = false;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    internal void SetSelection(string name)
    {
        if (selected.enabled) selected.Set("Output device", name);
        else selected.m_text = name;
    }

    internal void SetExpanded(TMP_Dropdown dropdown, bool expanded)
    {
        selected.enabled = !expanded;
        if (!expanded) return;
        // TMP creates new Toggle instances each time the menu opens. Their text
        // retains the full option name even when rendering ends with an ellipsis.
        foreach (Toggle item in dropdown.GetComponentsInChildren<Toggle>())
        {
            TMP_Text label = item.GetComponentInChildren<TMP_Text>();
            if (label != null) Attach(item.gameObject, prefab, label.text);
        }
    }

    private static UITooltip Attach(GameObject target, GameObject prefab, string name)
    {
        UITooltip tooltip = target.GetComponent<UITooltip>() ?? target.AddComponent<UITooltip>();
        tooltip.m_tooltipPrefab = prefab;
        tooltip.m_topic = "Output device";
        tooltip.m_text = name;
        return tooltip;
    }
}

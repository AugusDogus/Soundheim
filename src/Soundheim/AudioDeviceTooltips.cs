using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Soundheim;

// Uses Valheim's tooltip behavior and artwork, with plain text for device names.
internal sealed class AudioDeviceTooltips(GameObject prefab, UITooltip selected)
{
    internal static AudioDeviceTooltips? Create(TMP_Dropdown dropdown, Transform parent)
    {
        foreach (UITooltip source in Resources.FindObjectsOfTypeAll<UITooltip>())
        {
            if (source.m_tooltipPrefab == null) continue;
            GameObject prefab = Object.Instantiate(source.m_tooltipPrefab, parent);
            prefab.name = "Audio device tooltip template";
            prefab.SetActive(false);
            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true)) text.richText = false;
            UITooltip selected = Attach(dropdown.gameObject, prefab, dropdown.captionText?.text ?? "");
            return new AudioDeviceTooltips(prefab, selected);
        }
        return null;
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

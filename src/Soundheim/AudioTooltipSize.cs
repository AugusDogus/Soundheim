using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Soundheim;

// UITooltip fills its text before showing the panel. Serialized references are
// remapped to the visible clone, so measurements never modify the template.
internal sealed class AudioTooltipSize : MonoBehaviour
{
    [SerializeField] private RectTransform? panel;
    [SerializeField] private VerticalLayoutGroup? layout;
    [SerializeField] private TMP_Text[] labels = [];

    internal void Initialize(RectTransform panel, VerticalLayoutGroup layout, params TMP_Text[] labels)
    {
        this.panel = panel;
        this.layout = layout;
        this.labels = labels;
    }

    private void OnEnable()
    {
        if (panel == null || layout == null) return;
        float width = 0;
        foreach (TMP_Text text in labels)
        {
            float preferred = text.GetPreferredValues(text.text, Mathf.Infinity, Mathf.Infinity).x;
            width = Mathf.Max(width, preferred);
        }
        width = Mathf.Min(400, Mathf.Ceil(width) + layout.padding.horizontal);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Soundheim;

// UITooltip fills its text before showing the panel. Size this mod's cloned
// tooltip on activation, preserving native padding and the maximum wrap width.
internal sealed class AudioTooltipSize : MonoBehaviour
{
    private float maximumWidth;

    private void OnEnable()
    {
        if (transform.childCount == 0 || transform.GetChild(0) is not RectTransform panel) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        if (maximumWidth == 0) maximumWidth = panel.rect.width;

        float width = 0;
        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>())
        {
            float padding = Mathf.Max(0, panel.rect.width - text.rectTransform.rect.width);
            float preferred = text.GetPreferredValues(text.text, Mathf.Infinity, Mathf.Infinity).x;
            width = Mathf.Max(width, preferred + padding);
        }
        if (width <= 0 || maximumWidth <= 0) return;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(maximumWidth, Mathf.Ceil(width)));
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }
}

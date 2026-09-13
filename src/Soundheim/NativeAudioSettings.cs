using System;
using System.Collections.Generic;
using GUIFramework;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameAudioSettings = Valheim.SettingsGui.AudioSettings;

namespace Soundheim;

// Uses the game's dropdown prefab, fonts, and navigation in its existing Audio tab.
internal sealed class NativeAudioSettings : MonoBehaviour
{
    private GuiDropdown? dropdown;
    private TMP_Text? status;
    private readonly List<string> identifiers = new();
    private IReadOnlyList<Audio.AudioDevice>? shownDevices;
    private string shownSelection = "";
    private Settings? settings;
    private Plugin? plugin;
    private RectTransform? rowRect, parentRect, previousRect, sliderRect, volumeRect, labelRect;
    private readonly Vector3[] corners = new Vector3[4];

    internal static void Attach(GameAudioSettings audio, Toggle lastControl, Slider volume, TMP_Text volumeText)
    {
        if (Plugin.Instance == null || audio.GetComponent<NativeAudioSettings>() != null) return;
        Settings settings = audio.GetComponentInParent<Settings>();
        var graphics = settings.GetComponentInChildren<Valheim.SettingsGui.GraphicsSettings>(true);
        GuiDropdown? template = graphics != null &&
            AccessTools.Field(typeof(Valheim.SettingsGui.GraphicsSettings), "m_resolutionDropdown")?.GetValue(graphics) is GuiDropdown resolution
            ? resolution : null;
        TMP_Text? font = lastControl.GetComponentInChildren<TMP_Text>(true);
        if (template == null || font == null)
        {
            Plugin.Instance.ReportError($"{Plugin.PluginName} could not find Valheim's native audio label or dropdown. Check compatibility with this Valheim version.");
            return;
        }
        var component = audio.gameObject.AddComponent<NativeAudioSettings>();
        component.settings = settings;
        component.plugin = Plugin.Instance;
        component.sliderRect = volume.GetComponent<RectTransform>();
        component.volumeRect = volumeText.rectTransform;
        component.Create(audio, lastControl, template, font);
        Plugin.Instance.Refresh();
    }

    private void Create(GameAudioSettings audio, Toggle lastControl, GuiDropdown template, TMP_Text font)
    {
        var parent = audio.GetComponent<RectTransform>();
        var row = new GameObject("Soundheim output device", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rowRect = rect;
        parentRect = parent;
        previousRect = lastControl.GetComponent<RectTransform>();
        PositionRow();
        rect.sizeDelta = new Vector2(-48, 112);
        TMP_Text label = Label(row.transform, font, "Output device", 0, 0, 0.42f, 40);
        label.alignment = TextAlignmentOptions.MidlineRight;
        labelRect = label.rectTransform;
        dropdown = Instantiate(template, row.transform);
        dropdown.name = "Output device";
        dropdown.gameObject.SetActive(false);
        var field = dropdown.GetComponent<RectTransform>();
        field.anchorMin = new Vector2(0.44f, 1);
        field.anchorMax = Vector2.one;
        field.pivot = new Vector2(0.5f, 1);
        field.anchoredPosition = Vector2.zero;
        field.sizeDelta = new Vector2(0, 40);
        // Replace the entire event, including serialized Graphics-tab callbacks.
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        dropdown.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        dropdown.ClearOptions();
        dropdown.onValueChanged.AddListener(Selected);
        dropdown.OnExpandedStateChange += Expanded;
        foreach (TMP_Text text in dropdown.GetComponentsInChildren<TMP_Text>(true))
            if (text != dropdown.captionText && text != dropdown.itemText &&
                (dropdown.template == null || !text.transform.IsChildOf(dropdown.template))) text.enabled = false;
        if (dropdown.captionText != null) dropdown.captionText.richText = false;
        if (dropdown.itemText != null) dropdown.itemText.richText = false;
        status = Label(row.transform, font, "", 0, 48, 1, 64);
        status.fontSize = Math.Min(font.fontSize, 16);
        status.enableAutoSizing = false;
        status.alignment = TextAlignmentOptions.TopLeft;
        RefreshOptions();
        dropdown.gameObject.SetActive(true);
        PositionRow();
    }

    private static TMP_Text Label(Transform parent, TMP_Text style, string text, float x, float y, float width, float height)
    {
        var obj = new GameObject(text.Length > 0 ? text : "Audio output status", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var label = obj.AddComponent<TextMeshProUGUI>();
        label.font = style.font;
        label.fontSharedMaterial = style.fontSharedMaterial;
        label.fontSize = style.fontSize;
        label.fontStyle = style.fontStyle;
        label.color = style.color;
        label.richText = false;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.text = text;
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(width, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(-x, height);
        return label;
    }

    private void Selected(int index)
    {
        if (index >= 0 && index < identifiers.Count) plugin?.Preview(identifiers[index]);
    }

    private void Expanded(bool expanded) { if (settings != null) settings.BlockNavigation(expanded); }

    private void LateUpdate() => PositionRow();
    private void PositionRow()
    {
        if (rowRect == null || parentRect == null || previousRect == null) return;
        previousRect.GetWorldCorners(corners);
        float top = parentRect.InverseTransformPoint(corners[0]).y - parentRect.rect.yMax - 20;
        rowRect.anchoredPosition = new Vector2(0, top);
        if (sliderRect == null || volumeRect == null || labelRect == null || dropdown == null) return;
        sliderRect.GetWorldCorners(corners);
        float left = rowRect.InverseTransformPoint(corners[0]).x - rowRect.rect.xMin;
        volumeRect.GetWorldCorners(corners);
        float right = rowRect.InverseTransformPoint(corners[2]).x - rowRect.rect.xMin;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0, 1);
        labelRect.pivot = new Vector2(0, 1);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(Math.Max(0, left - 16), 40);
        var field = dropdown.GetComponent<RectTransform>();
        field.anchorMin = field.anchorMax = new Vector2(0, 1);
        field.pivot = new Vector2(0, 1);
        field.anchoredPosition = new Vector2(left, 0);
        field.sizeDelta = new Vector2(Math.Max(200, right - left), 40);
    }

    private void Update()
    {
        if (plugin == null || dropdown == null) return;
        if (!dropdown.IsExpanded && (shownDevices != plugin.Devices || shownSelection != plugin.Selection)) RefreshOptions();
        if (status != null) status.text = (plugin.Experimental ? "Windows support is experimental and untested.\n" : "") + plugin.Status;
    }

    private void RefreshOptions()
    {
        if (plugin == null || dropdown == null) return;
        shownDevices = plugin.Devices;
        shownSelection = plugin.Selection;
        identifiers.Clear();
        identifiers.Add("");
        var names = new List<string> { "System default" };
        foreach (Audio.AudioDevice device in plugin.Devices) { identifiers.Add(device.Id); names.Add(device.Name); }
        int selected = identifiers.IndexOf(plugin.Selection);
        if (selected < 0)
        {
            selected = identifiers.Count;
            identifiers.Add(plugin.Selection);
            names.Add("Selected device (disconnected)");
        }
        dropdown.ClearOptions();
        dropdown.AddOptions(names);
        dropdown.SetValueWithoutNotify(selected);
        dropdown.RefreshShownValue();
    }

    internal void SetNavigation(Toggle previous, Button back, Button ok)
    {
        if (dropdown == null) return;
        GuiUtils.SetNavigationDown(previous, dropdown);
        GuiUtils.SetNavigationUp(dropdown, previous);
        GuiUtils.SetNavigationDown(dropdown, back);
        GuiUtils.SetNavigationUp(back, dropdown);
        GuiUtils.SetNavigationUp(ok, dropdown);
    }

    private void OnEnable() => plugin?.Refresh();
    private void OnDisable() { if (dropdown != null && dropdown.IsExpanded) { dropdown.Hide(); Expanded(false); } }
    private void OnDestroy()
    {
        if (dropdown != null) dropdown.OnExpandedStateChange -= Expanded;
        plugin?.Revert();
    }

    [HarmonyPatch(typeof(GameAudioSettings), "Initialize")]
    private static class InitializePatch
    {
        private static void Postfix(GameAudioSettings __instance, Toggle ___m_continousMusic, Slider ___m_volumeSlider, TMP_Text ___m_volumeText) =>
            Attach(__instance, ___m_continousMusic, ___m_volumeSlider, ___m_volumeText);
    }
    [HarmonyPatch(typeof(GameAudioSettings), "OnTabOpen")]
    private static class NavigationPatch
    {
        private static void Postfix(GameAudioSettings __instance, Toggle ___m_continousMusic, Button backButton, Button okButton) =>
            __instance.GetComponent<NativeAudioSettings>()?.SetNavigation(___m_continousMusic, backButton, okButton);
    }
    [HarmonyPatch(typeof(GameAudioSettings), "OnOkAsync")]
    private static class SavePatch { private static void Prefix() => Plugin.Instance?.Save(); }
    [HarmonyPatch(typeof(GameAudioSettings), "OnBack")]
    private static class RevertPatch { private static void Postfix() => Plugin.Instance?.Revert(); }
}

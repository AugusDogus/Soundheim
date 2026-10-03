using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Soundheim.Audio;
using UnityEngine;

namespace Soundheim;

[BepInPlugin(PluginId, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginId = "augusdogus.mods.Soundheim";
    public const string PluginName = "Custom Audio Output Device";
    public const string PluginVersion = "1.0.3";

    internal static Plugin? Instance { get; private set; }
    internal IReadOnlyList<AudioDevice> Devices => output?.Devices ?? Array.Empty<AudioDevice>();
    internal string Selection => output?.Selection ?? "";
    internal string Status => output?.Status ?? "Audio output selection is unavailable on this platform.";
    private ConfigEntry<string>? preference;
    private AudioOutput? output;
    private readonly Harmony harmony = new(PluginId);

    private void Awake()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { enabled = false; return; }
        using var process = Process.GetCurrentProcess();
        IAudioBackend backend;
        if (Application.platform == RuntimePlatform.LinuxPlayer) backend = new PulseAudioBackend(process.Id, new Pactl().Run);
        else if (Application.platform == RuntimePlatform.WindowsPlayer) backend = new WindowsAudioBackend(process.Id);
        else { Logger.LogWarning($"{PluginName} supports Linux and Windows only."); enabled = false; return; }
        preference = Config.Bind("Audio", "Output device", "", "Stable output identifier. Empty follows the system default. Select a device in Settings > Audio.");
        output = new AudioOutput(backend, preference.Value);
        output.Failed += LogError;
        preference.SettingChanged += PreferenceChanged;
        Instance = this;
        harmony.PatchAll(typeof(NativeAudioSettings).Assembly);
        Logger.LogInfo($"{PluginName} loaded. Select your output in Settings > Audio.");
    }

    internal void Preview(string id) => output?.Select(id);

    internal void Save() { if (preference != null) preference.Value = Selection; }
    internal void Revert() { if (preference != null && Selection != preference.Value) Preview(preference.Value); }
    internal void Refresh() => output?.Refresh();
    internal void ReportTooltipUnavailable() => Logger.LogWarning("Audio device tooltips could not find the dropdown's text style. Device selection remains available.");
    private void PreferenceChanged(object sender, EventArgs args) { if (preference != null) Preview(preference.Value); }

    private void Update() => output?.Update();
    internal void ReportError(string message) => output?.ReportError(message);
    private void LogError(string message) => Logger.LogWarning(message);

    private void OnDestroy()
    {
        if (preference != null) preference.SettingChanged -= PreferenceChanged;
        if (output != null) output.Failed -= LogError;
        harmony.UnpatchSelf();
        if (Instance == this) Instance = null;
    }
}

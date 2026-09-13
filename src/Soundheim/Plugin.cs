using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Soundheim.Audio;
using UnityEngine;

namespace Soundheim;

[BepInPlugin(PluginId, "Soundheim", PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginId = "augusdogus.mods.Soundheim";
    public const string PluginVersion = "1.0.0";

    internal static Plugin? Instance { get; private set; }
    internal IReadOnlyList<AudioDevice> Devices { get; private set; } = Array.Empty<AudioDevice>();
    internal string Selection { get; private set; } = "";
    internal string Status { get; private set; } = "Reading audio outputs...";
    internal bool Experimental => Application.platform == RuntimePlatform.WindowsPlayer;
    private ConfigEntry<string>? preference;
    private IAudioBackend? backend;
    private Task<PollResult>? pending;
    private float nextPoll;
    private int revision;
    private string lastError = "";
    private readonly Harmony harmony = new(PluginId);

    private sealed class PollResult(int revision, AudioResult<IReadOnlyList<AudioDevice>> devices, AudioResult<string> route)
    {
        public int Revision { get; } = revision;
        public AudioResult<IReadOnlyList<AudioDevice>> Devices { get; } = devices;
        public AudioResult<string> Route { get; } = route;
    }

    private void Awake()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { enabled = false; return; }
        using var process = Process.GetCurrentProcess();
        if (Application.platform == RuntimePlatform.LinuxPlayer) backend = new PulseAudioBackend(process.Id, new Pactl().Run);
        else if (Application.platform == RuntimePlatform.WindowsPlayer) backend = new WindowsAudioBackend(process.Id);
        else { Logger.LogWarning("Soundheim supports Linux and Windows only."); enabled = false; return; }
        preference = Config.Bind("Audio", "Output device", "", "Stable output identifier. Empty follows the system default. Select a device in Settings > Audio.");
        Selection = preference.Value;
        preference.SettingChanged += PreferenceChanged;
        Instance = this;
        harmony.PatchAll(typeof(NativeAudioSettings).Assembly);
        Logger.LogInfo("Soundheim loaded. Select your output in Settings > Audio.");
    }

    internal void Preview(string id)
    {
        Selection = id;
        revision++;
        nextPoll = 0;
        Status = "Applying output preference...";
    }

    internal void Save() { if (preference != null) preference.Value = Selection; }
    internal void Revert() { if (preference != null && Selection != preference.Value) Preview(preference.Value); }
    internal void Refresh() => nextPoll = 0;
    private void PreferenceChanged(object sender, EventArgs args) { if (preference != null) Preview(preference.Value); }

    private void Update()
    {
        if (backend == null) return;
        if (pending != null && pending.IsCompleted)
        {
            Task<PollResult> completed = pending;
            pending = null;
            if (completed.IsFaulted) ReportError(completed.Exception?.GetBaseException().Message ?? "Unknown audio error.");
            else
            {
                PollResult result = completed.GetAwaiter().GetResult();
                if (result.Devices is AudioResult<IReadOnlyList<AudioDevice>>.Success devices) Devices = devices.Value;
                else if (result.Devices is AudioResult<IReadOnlyList<AudioDevice>>.Failure failure) ReportError(failure.Message);
                if (result.Revision == revision)
                {
                    if (result.Route is AudioResult<string>.Success route) { Status = route.Value; lastError = ""; }
                    else if (result.Route is AudioResult<string>.Failure error) ReportError(error.Message);
                }
            }
        }
        if (pending != null || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 5;
        string selected = Selection;
        int requestRevision = revision;
        IAudioBackend selectedBackend = backend;
        // Keep process and COM operations off the game's main thread. Never overlap polls.
        pending = Task.Run(() => new PollResult(requestRevision, selectedBackend.ListOutputs(), selectedBackend.Route(selected)));
    }

    internal void ReportError(string message)
    {
        Status = message;
        if (message == lastError) return;
        lastError = message;
        Logger.LogWarning(message);
    }

    private void OnDestroy()
    {
        if (preference != null) preference.SettingChanged -= PreferenceChanged;
        harmony.UnpatchSelf();
        if (Instance == this) Instance = null;
    }
}

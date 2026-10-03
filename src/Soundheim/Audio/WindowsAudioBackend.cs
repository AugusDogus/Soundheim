using System;
using System.Collections.Generic;
using System.Linq;

namespace Soundheim.Audio;

internal sealed class WindowsAudioBackend(int processId) : IAudioBackend
{
    public AudioResult<IReadOnlyList<AudioDevice>> ListOutputs() => AudioBoundary.Run(WindowsOutputs.List);

    public AudioResult<string> Route(string deviceId) => AudioBoundary.Run(() =>
    {
        using var apartment = new WindowsCom.Apartment();
        IReadOnlyList<AudioDevice> outputs = WindowsOutputs.List();
        if (deviceId.Length > 0 && !outputs.Any(device => device.Id == deviceId))
            return "Selected output is disconnected. Your preference is saved; reconnect it and reopen Audio Settings to retry.";
        bool refresh = false;
        if (deviceId.Length > 0)
        {
            IReadOnlyList<string> active = WindowsAudioSessions.ActiveOutputs((uint)processId, outputs);
            if (active.Count == 0) return "Valheim's audio stream is not available yet. Reopen Audio Settings to apply your selection once audio starts.";
            refresh = active.Any(id => !string.Equals(id, deviceId, StringComparison.OrdinalIgnoreCase));
        }
        using var policy = new WindowsAudioPolicy();
        policy.Apply((uint)processId, deviceId, refresh);
        return refresh ? "Applying saved audio output..." : "";
    });
}

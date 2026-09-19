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
            return "Selected output is disconnected. Keeping your preference and waiting for it to return.";
        bool refresh = false;
        if (deviceId.Length > 0)
        {
            IReadOnlyList<string> active = WindowsAudioSessions.ActiveOutputs((uint)processId, outputs);
            if (active.Count == 0) return "Waiting for Valheim's audio stream. Your selection will apply when it starts.";
            refresh = active.Any(id => !string.Equals(id, deviceId, StringComparison.OrdinalIgnoreCase));
        }
        using var policy = new WindowsAudioPolicy();
        policy.Apply((uint)processId, deviceId, refresh);
        return refresh ? "Applying saved audio output..." : "";
    });
}

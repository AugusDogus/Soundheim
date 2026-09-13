using System.Collections.Generic;
using System.Linq;

namespace Soundheim.Audio;

internal sealed class WindowsAudioBackend(int processId) : IAudioBackend
{
    public AudioResult<IReadOnlyList<AudioDevice>> ListOutputs() => AudioBoundary.Run(WindowsOutputs.List);

    public AudioResult<string> Route(string deviceId) => AudioBoundary.Run(() =>
    {
        using var apartment = new WindowsCom.Apartment();
        if (deviceId.Length > 0 && !WindowsOutputs.List().Any(device => device.Id == deviceId))
            return "Selected output is disconnected. Keeping your preference and waiting for it to return.";
        using var policy = new WindowsAudioPolicy();
        policy.Apply((uint)processId, deviceId);
        return "Output preference applied. If audio hasn't moved, restart Valheim.";
    });
}

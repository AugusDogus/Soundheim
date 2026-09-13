using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Soundheim.Audio;

internal sealed class Pactl
{
    public string Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("pactl")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["LC_ALL"] = "C";
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("Cannot start pactl. Install PulseAudio client tools and enable PulseAudio or PipeWire's PulseAudio service.", ex);
        }
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(3000))
        {
            try { process.Kill(); }
            catch (InvalidOperationException) { /* The command already exited. */ }
            throw new TimeoutException("The audio server did not respond within 3 seconds. Check your audio service, then refresh.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"pactl {string.Join(" ", arguments)} failed: {error.GetAwaiter().GetResult().Trim()}");
        return output.GetAwaiter().GetResult();
    }
}

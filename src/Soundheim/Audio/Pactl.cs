using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Soundheim.Audio;

internal sealed class Pactl
{
    private const string SteamLauncher = "/usr/bin/steam-runtime-launch-client";

    private static ProcessStartInfo StartInfo(string[] arguments, bool host)
    {
        var start = new ProcessStartInfo(host ? SteamLauncher : "pactl")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (host)
            foreach (string argument in new[] { "--alongside-steam", "--env=LC_ALL=C", "--", "pactl" }) start.ArgumentList.Add(argument);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["LC_ALL"] = "C";
        return start;
    }

    public string Run(params string[] arguments)
    {
        using var process = new Process { StartInfo = StartInfo(arguments, false) };
        try
        {
            try { process.Start(); }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2 && File.Exists(SteamLauncher))
            {
                // Steam's game container omits pactl. Its launcher runs the host copy
                // with host libraries and audio-service access, preserving argument boundaries.
                process.StartInfo = StartInfo(arguments, true);
                process.Start();
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"Cannot launch the audio command '{process.StartInfo.FileName}' (error {ex.NativeErrorCode}): {ex.Message}. Check that PulseAudio client tools are installed and Steam's launcher service is running.", ex);
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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Soundheim.Audio;

namespace Soundheim.Tests;

[TestClass]
public sealed class LivePulseAudioTests
{
    [TestMethod]
    [TestCategory("LiveAudio")]
    public void ListsHostOutputsFromTheSteamRuntime()
    {
        if (Environment.GetEnvironmentVariable("SOUNDHEIM_LIVE_AUDIO_TEST") != "1")
            Assert.Inconclusive("Set SOUNDHEIM_LIVE_AUDIO_TEST=1 inside the Steam runtime to check host output discovery.");
        var result = new PulseAudioBackend(Environment.ProcessId, new Pactl().Run).ListOutputs();
        if (result is AudioResult<System.Collections.Generic.IReadOnlyList<AudioDevice>>.Failure failure)
            Assert.Fail(failure.Message);
        Assert.IsInstanceOfType<AudioResult<System.Collections.Generic.IReadOnlyList<AudioDevice>>.Success>(result);
        if (result is AudioResult<System.Collections.Generic.IReadOnlyList<AudioDevice>>.Success success)
            Assert.IsTrue(success.Value.Count > 0, "Expected the workstation's connected outputs.");
    }

    [TestMethod]
    [TestCategory("LiveAudio")]
    public void RoutesASilentTestStreamWithoutMovingTheControlStreamOrSystemDefault()
    {
        if (Environment.GetEnvironmentVariable("SOUNDHEIM_LIVE_AUDIO_TEST") != "1")
            Assert.Inconclusive("Set SOUNDHEIM_LIVE_AUDIO_TEST=1 to test against the local PulseAudio/PipeWire server.");
        var pactl = new Pactl();
        string originalDefault = pactl.Run("get-default-sink").Trim();
        string name = "soundheim_test_" + Guid.NewGuid().ToString("N");
        string file = Path.Combine(Path.GetTempPath(), name + ".raw");
        string module = pactl.Run("load-module", "module-null-sink", "sink_name=" + name).Trim();
        try
        {
            File.WriteAllBytes(file, new byte[48000 * 2 * 2 * 15]);
            using var game = PlaySilence(file, originalDefault);
            using var control = PlaySilence(file, originalDefault);
            try
            {
                uint original = WaitForStream(pactl, control.Id).Value<uint>("sink");
                WaitForStream(pactl, game.Id);
                var backend = new PulseAudioBackend(game.Id, pactl.Run);
                var result = backend.Route(name);
                Assert.IsInstanceOfType<AudioResult<string>.Success>(result);
                uint target = PulseAudioBackend.ReadSinks(pactl.Run("--format=json", "list", "sinks"))
                    .Single(sink => sink.Device.Id == name).Index;
                Assert.AreEqual(target, WaitForStream(pactl, game.Id, target).Value<uint>("sink"));
                Assert.AreEqual(original, WaitForStream(pactl, control.Id).Value<uint>("sink"));
                Assert.AreEqual(originalDefault, pactl.Run("get-default-sink").Trim());
            }
            finally
            {
                if (!game.HasExited) game.Kill();
                if (!control.HasExited) control.Kill();
                game.WaitForExit(3000);
                control.WaitForExit(3000);
            }
        }
        finally { pactl.Run("unload-module", module); File.Delete(file); }
    }

    private static Process PlaySilence(string file, string device)
    {
        var start = new ProcessStartInfo("pacat") { UseShellExecute = false };
        foreach (string argument in new[] { "--playback", "--raw", "--rate=48000", "--channels=2", "--format=s16le", "--device=" + device, file })
            start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start the silent test stream.");
    }

    private static JToken WaitForStream(Pactl pactl, int pid, uint? expectedSink = null)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            JToken? stream = JArray.Parse(pactl.Run("--format=json", "list", "sink-inputs"))
                .FirstOrDefault(item => (string?)item["properties"]?["application.process.id"] == pid.ToString());
            if (stream != null && (!expectedSink.HasValue || stream.Value<uint>("sink") == expectedSink.Value)) return stream;
            Thread.Sleep(50);
        }
        throw new TimeoutException($"Test audio stream for process {pid} did not appear.");
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Soundheim.Audio;

namespace Soundheim.Tests;

[TestClass]
public sealed class AudioOutputTests
{
    private sealed class Backend : IAudioBackend, IDisposable
    {
        internal readonly ConcurrentQueue<string> Routes = new();
        internal readonly ManualResetEventSlim Started = new(false);
        internal readonly ManualResetEventSlim Continue = new(true);
        internal int Discoveries;
        internal bool FailDiscovery;

        public AudioResult<IReadOnlyList<AudioDevice>> ListOutputs()
        {
            Interlocked.Increment(ref Discoveries);
            Started.Set();
            if (!Continue.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Test did not release the audio worker.");
            return FailDiscovery
                ? new AudioResult<IReadOnlyList<AudioDevice>>.Failure("Audio server unavailable")
                : new AudioResult<IReadOnlyList<AudioDevice>>.Success(new[] { new AudioDevice("headset", "Headset") });
        }

        public AudioResult<string> Route(string deviceId)
        {
            Routes.Enqueue(deviceId);
            return new AudioResult<string>.Success("");
        }

        public void Dispose() { Started.Dispose(); Continue.Dispose(); }
    }

    private static void Complete(AudioOutput output, Func<bool> condition)
    {
        Assert.IsTrue(SpinWait.SpinUntil(() => { output.Update(); return condition(); }, TimeSpan.FromSeconds(3)),
            "The requested audio operation did not finish.");
    }

    [TestMethod]
    public void AppliesSavedOutputOnceAndDoesNoMoreWorkWithoutAnExplicitRequest()
    {
        using var backend = new Backend();
        var output = new AudioOutput(backend, "headset");
        Complete(output, () => output.Status == "");
        for (int frame = 0; frame < 10000; frame++) output.Update();
        Assert.AreEqual(1, backend.Discoveries);
        CollectionAssert.AreEqual(new[] { "headset" }, backend.Routes.ToArray());
    }

    [TestMethod]
    public void ReopeningSettingsRefreshesTheUnchangedSelectionOnce()
    {
        using var backend = new Backend();
        var output = new AudioOutput(backend, "headset");
        Complete(output, () => output.Status == "");
        output.Refresh();
        Complete(output, () => output.Status == "");
        for (int frame = 0; frame < 10000; frame++) output.Update();
        Assert.AreEqual(2, backend.Discoveries);
        CollectionAssert.AreEqual(new[] { "headset", "headset" }, backend.Routes.ToArray());
    }

    [TestMethod]
    public void SelectionChangesDuringARequestApplyTheLatestChoiceWithoutOverlappingWorkers()
    {
        using var backend = new Backend();
        backend.Continue.Reset();
        var output = new AudioOutput(backend, "speakers");
        output.Update();
        try
        {
            Assert.IsTrue(backend.Started.Wait(TimeSpan.FromSeconds(3)));
            output.Select("headset");
            output.Refresh();
            output.Select("speakers"); // Back restores the saved selection.
            for (int frame = 0; frame < 1000; frame++) output.Update();
            Assert.AreEqual(1, backend.Discoveries);
            Assert.AreEqual("Applying output preference...", output.Status);
        }
        finally { backend.Continue.Set(); }
        Complete(output, () => output.Status == "");
        CollectionAssert.AreEqual(new[] { "speakers", "speakers" }, backend.Routes.ToArray());
        Assert.AreEqual(2, backend.Discoveries);
        Assert.AreEqual("speakers", output.Selection);
    }

    [TestMethod]
    public void SelectingTheCurrentOutputDoesNotRepeatRouting()
    {
        using var backend = new Backend();
        var output = new AudioOutput(backend, "headset");
        Complete(output, () => output.Status == "");
        output.Select("headset");
        for (int frame = 0; frame < 10000; frame++) output.Update();
        Assert.AreEqual(1, backend.Discoveries);
    }

    [TestMethod]
    public void DiscoveryFailureRemainsVisibleUntilAnExplicitRetrySucceeds()
    {
        using var backend = new Backend { FailDiscovery = true };
        var output = new AudioOutput(backend, "headset");
        string? error = null;
        output.Failed += message => error = message;
        Complete(output, () => error != null);
        StringAssert.Contains(output.Status, "Reopen Audio Settings");
        Assert.AreEqual("Audio server unavailable", error);
        for (int frame = 0; frame < 10000; frame++) output.Update();
        Assert.AreEqual(1, backend.Discoveries);
        backend.FailDiscovery = false;
        output.Refresh();
        Complete(output, () => output.Status == "");
        Assert.AreEqual(2, backend.Discoveries);
        Assert.AreEqual("headset", output.Selection);
    }
}

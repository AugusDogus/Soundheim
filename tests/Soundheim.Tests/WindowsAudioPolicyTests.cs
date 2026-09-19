using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Soundheim.Audio;

namespace Soundheim.Tests;

[TestClass]
public sealed class WindowsAudioPolicyTests
{
    [DataTestMethod]
    [DataRow("headset")]
    [DataRow("HEADSET")]
    public void SavedPreferenceIsReappliedWhenLiveAudioUsesAnotherOutput(string saved)
    {
        var writes = new List<(int, string)>();
        WindowsAudioPolicy.Apply("headset", _ => saved, (role, endpoint) =>
        {
            writes.Add((role, endpoint));
            return 0;
        }, refresh: true);
        CollectionAssert.AreEqual(new[] { (0, ""), (1, ""), (0, "headset"), (1, "headset") }, writes);
    }

    [TestMethod]
    public void CorrectlyRoutedAudioDoesNotResetEveryPoll()
    {
        WindowsAudioPolicy.Apply("headset", _ => "headset", (_, _) =>
        {
            Assert.Fail("A matching live output must not be interrupted.");
            return 0;
        }, refresh: false);
    }

    [TestMethod]
    public void ChangedPreferenceIsAppliedWithoutResettingFirst()
    {
        var writes = new List<(int, string)>();
        WindowsAudioPolicy.Apply("headset", _ => "speakers", (role, endpoint) =>
        {
            writes.Add((role, endpoint));
            return 0;
        }, refresh: true);
        CollectionAssert.AreEqual(new[] { (0, "headset"), (1, "headset") }, writes);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void FailedRefreshRestoresBothSavedPreferences(int failedWrite)
    {
        string[] current = ["headset", "headset"];
        int calls = 0;
        Assert.ThrowsException<COMException>(() => WindowsAudioPolicy.Apply("headset", role => current[role], (role, endpoint) =>
        {
            if (++calls == failedWrite) return unchecked((int)0x80004005);
            current[role] = endpoint;
            return 0;
        }, refresh: true));
        CollectionAssert.AreEqual(new[] { "headset", "headset" }, current);
    }
}

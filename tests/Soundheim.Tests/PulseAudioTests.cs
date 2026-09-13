using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Soundheim.Audio;

namespace Soundheim.Tests;

[TestClass]
public sealed class PulseAudioTests
{
    private const string Sinks = """
        [{"index":57,"name":"speakers","description":"Desk speakers"},
         {"index":59,"name":"headset","description":"Arctis 7 Game"}]
        """;

    private sealed class Server
    {
        public string Streams = "[]";
        public string Outputs = Sinks;
        public List<string[]> Moves { get; } = new();
        public string Run(string[] args)
        {
            if (args[0] == "get-default-sink") return "speakers\n";
            if (args[0] == "move-sink-input") { Moves.Add(args); return ""; }
            return args[2] == "sinks" ? Outputs : Streams;
        }
    }

    [TestMethod]
    public void ListsStableIdentifiersAndFriendlyNames()
    {
        var backend = new PulseAudioBackend(42, new Server().Run);
        var result = backend.ListOutputs();
        Assert.IsInstanceOfType<AudioResult<IReadOnlyList<AudioDevice>>.Success>(result);
        if (result is AudioResult<IReadOnlyList<AudioDevice>>.Success success)
        {
            Assert.AreEqual("headset", success.Value[1].Id);
            Assert.AreEqual("Arctis 7 Game", success.Value[1].Name);
        }
    }

    [TestMethod]
    public void MovesAllGameStreamsAndNeverOtherApplications()
    {
        var server = new Server { Streams = """
            [{"index":1,"sink":57,"properties":{"application.process.id":"42"}},
             {"index":2,"sink":57,"properties":{"application.process.id":"4242","application.name":"Valheim"}},
             {"index":3,"sink":57,"properties":{"application.name":"Valheim"}},
             {"index":4,"sink":57,"properties":{"application.process.id":"42"}}]
            """ };
        var result = new PulseAudioBackend(42, server.Run).Route("headset");
        Assert.IsInstanceOfType<AudioResult<string>.Success>(result);
        if (result is AudioResult<string>.Success success) Assert.AreEqual("", success.Value);
        Assert.AreEqual(2, server.Moves.Count);
        CollectionAssert.AreEqual(new[] { "move-sink-input", "1", "headset" }, server.Moves[0]);
        CollectionAssert.AreEqual(new[] { "move-sink-input", "4", "headset" }, server.Moves[1]);
    }

    [TestMethod]
    public void DoesNotMoveAnAlreadyRoutedStream()
    {
        var server = new Server { Streams = """[{"index":1,"sink":59,"properties":{"application.process.id":"42"}}]""" };
        new PulseAudioBackend(42, server.Run).Route("headset");
        Assert.AreEqual(0, server.Moves.Count);
    }

    [TestMethod]
    public void DefaultSelectionResolvesTheCurrentDefault()
    {
        var server = new Server { Streams = """[{"index":1,"sink":59,"properties":{"application.process.id":"42"}}]""" };
        new PulseAudioBackend(42, server.Run).Route("");
        Assert.AreEqual("speakers", server.Moves[0][2]);
    }

    [TestMethod]
    public void DisconnectedDeviceDoesNotMoveStreams()
    {
        var server = new Server();
        var result = new PulseAudioBackend(42, server.Run).Route("unplugged-headset");
        Assert.IsInstanceOfType<AudioResult<string>.Success>(result);
        if (result is AudioResult<string>.Success success) StringAssert.Contains(success.Value, "disconnected");
        Assert.AreEqual(0, server.Moves.Count);
    }

    [TestMethod]
    public void WaitingForGameIsNotReportedAsRoutingSuccess()
    {
        var result = new PulseAudioBackend(42, new Server().Run).Route("headset");
        Assert.IsInstanceOfType<AudioResult<string>.Success>(result);
        if (result is AudioResult<string>.Success success) StringAssert.Contains(success.Value, "Waiting");
    }

    [DataTestMethod]
    [DataRow("not json")]
    [DataRow("[{\"index\":-1,\"name\":\"headset\"}]")]
    [DataRow("[{\"index\":1}]")]
    public void InvalidAudioServerDataIsReported(string json)
    {
        var server = new Server { Outputs = json };
        Assert.IsInstanceOfType<AudioResult<IReadOnlyList<AudioDevice>>.Failure>(new PulseAudioBackend(42, server.Run).ListOutputs());
    }

    [TestMethod]
    public void CommandFailureDoesNotBecomeSuccess()
    {
        var backend = new PulseAudioBackend(42, _ => throw new InvalidOperationException("Audio server unavailable"));
        Assert.IsInstanceOfType<AudioResult<string>.Failure>(backend.Route("headset"));
    }

    [TestMethod]
    public void DisconnectedPreferenceIsAppliedWhenDeviceReturns()
    {
        var server = new Server { Outputs = "[]", Streams = """[{"index":1,"sink":57,"properties":{"application.process.id":"42"}}]""" };
        var backend = new PulseAudioBackend(42, server.Run);
        backend.Route("headset");
        Assert.AreEqual(0, server.Moves.Count);
        server.Outputs = Sinks;
        backend.Route("headset");
        Assert.AreEqual(1, server.Moves.Count);
    }
}

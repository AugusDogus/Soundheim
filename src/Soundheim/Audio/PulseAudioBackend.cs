using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Soundheim.Audio;

internal sealed class PulseAudioBackend(int processId, Func<string[], string> command) : IAudioBackend
{
    internal sealed class Sink(uint index, AudioDevice device)
    {
        public uint Index { get; } = index;
        public AudioDevice Device { get; } = device;
    }

    public AudioResult<IReadOnlyList<AudioDevice>> ListOutputs() => AudioBoundary.Run<IReadOnlyList<AudioDevice>>(
        () => ReadSinks(command(["--format=json", "list", "sinks"])).Select(sink => sink.Device).ToArray());

    public AudioResult<string> Route(string deviceId) => AudioBoundary.Run(() =>
    {
        string target = deviceId.Length == 0 ? command(["get-default-sink"]).Trim() : deviceId;
        Sink? sink = ReadSinks(command(["--format=json", "list", "sinks"]))
            .FirstOrDefault(candidate => candidate.Device.Id == target);
        if (sink == null) return "Selected output is disconnected. Keeping your preference and waiting for it to return.";

        JArray streams = JArray.Parse(command(["--format=json", "list", "sink-inputs"]));
        int count = 0;
        foreach (JToken stream in streams)
        {
            // Never match by application name: only this game's exact process ID is eligible.
            if (!int.TryParse((string?)stream["properties"]?["application.process.id"], out int owner) || owner != processId)
                continue;
            count++;
            uint index = RequiredIndex(stream, "index");
            if (RequiredIndex(stream, "sink") != sink.Index)
                command(["move-sink-input", index.ToString(CultureInfo.InvariantCulture), sink.Device.Id]);
        }
        return count == 0 ? "Waiting for Valheim's audio stream. Your selection will apply when it starts." :
            $"Output: {sink.Device.Name}";
    });

    internal static IReadOnlyList<Sink> ReadSinks(string json)
    {
        var result = new List<Sink>();
        foreach (JToken item in JArray.Parse(json))
        {
            string? id = (string?)item["name"];
            if (string.IsNullOrWhiteSpace(id)) throw new FormatException("The audio server returned an output without a name. Refresh to try again.");
            string name = (string?)item["description"] ?? id;
            result.Add(new Sink(RequiredIndex(item, "index"), new AudioDevice(id, name)));
        }
        return result;
    }

    private static uint RequiredIndex(JToken item, string name)
    {
        if (item[name]?.Type != JTokenType.Integer || !uint.TryParse(item[name]?.ToString(), out uint value))
            throw new FormatException($"The audio server returned an invalid {name}. Refresh to try again.");
        return value;
    }
}

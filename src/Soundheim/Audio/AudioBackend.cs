using System;
using System.Collections.Generic;

namespace Soundheim.Audio;

internal sealed class AudioDevice(string id, string name)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
}

internal abstract class AudioResult<T>
{
    private AudioResult() { }
    internal sealed class Success(T value) : AudioResult<T> { public T Value { get; } = value; }
    internal sealed class Failure(string message) : AudioResult<T> { public string Message { get; } = message; }
}

internal interface IAudioBackend
{
    AudioResult<IReadOnlyList<AudioDevice>> ListOutputs();
    AudioResult<string> Route(string deviceId);
}

internal static class AudioBoundary
{
    // File/process/native failures are contained at the operating-system boundary.
    public static AudioResult<T> Run<T>(Func<T> action)
    {
        try { return new AudioResult<T>.Success(action()); }
        catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException ||
            ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException ||
            ex is ArgumentException || ex is TimeoutException || ex is FormatException ||
            ex is Newtonsoft.Json.JsonException || ex is System.Runtime.InteropServices.ExternalException ||
            ex is NotSupportedException || ex is DllNotFoundException || ex is EntryPointNotFoundException)
        {
            return new AudioResult<T>.Failure(ex.Message);
        }
    }
}

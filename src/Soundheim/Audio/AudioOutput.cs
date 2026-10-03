using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Soundheim.Audio;

// Serializes explicit refreshes and selection changes. Once a request finishes,
// Update only observes state; it never schedules another request on its own.
internal sealed class AudioOutput(IAudioBackend backend, string selection)
{
    internal IReadOnlyList<AudioDevice> Devices { get; private set; } = Array.Empty<AudioDevice>();
    internal string Selection { get; private set; } = selection;
    internal string Status { get; private set; } = "Reading audio outputs...";
    private Task<RefreshResult>? pending;
    private int revision;
    private int startedRevision = -1; // Apply the saved output once at startup.
    private string lastError = "";
    internal event Action<string>? Failed;

    private sealed class RefreshResult(int revision, AudioResult<IReadOnlyList<AudioDevice>> devices, AudioResult<string> route)
    {
        internal int Revision { get; } = revision;
        internal AudioResult<IReadOnlyList<AudioDevice>> Devices { get; } = devices;
        internal AudioResult<string> Route { get; } = route;
    }

    internal void Select(string id)
    {
        if (Selection == id) return;
        Selection = id;
        Refresh();
        Status = "Applying output preference...";
    }

    internal void Refresh()
    {
        revision++;
        Status = "Reading audio outputs...";
    }

    internal void Update()
    {
        if (pending != null && pending.IsCompleted)
        {
            Task<RefreshResult> completed = pending;
            pending = null;
            if (completed.IsFaulted)
                ReportError(completed.Exception?.GetBaseException().Message ?? "Unknown audio error.");
            else
            {
                RefreshResult result = completed.GetAwaiter().GetResult();
                // A new selection may have arrived while the worker was busy.
                if (result.Revision == revision)
                {
                    if (result.Devices is AudioResult<IReadOnlyList<AudioDevice>>.Success devices) Devices = devices.Value;
                    if (result.Devices is AudioResult<IReadOnlyList<AudioDevice>>.Failure discoveryError) ReportError(discoveryError.Message);
                    else if (result.Route is AudioResult<string>.Failure routeError) ReportError(routeError.Message);
                    else if (result.Route is AudioResult<string>.Success route) { Status = route.Value; lastError = ""; }
                }
            }
        }
        if (pending != null || startedRevision == revision) return;
        string selected = Selection;
        int requestRevision = revision;
        startedRevision = requestRevision;
        pending = Task.Run(() => new RefreshResult(requestRevision, backend.ListOutputs(), backend.Route(selected)));
    }

    internal void ReportError(string message)
    {
        Status = "Couldn't update audio output. Reopen Audio Settings to retry. Details are in the game log.";
        if (message == lastError) return;
        lastError = message;
        Failed?.Invoke(message);
    }
}

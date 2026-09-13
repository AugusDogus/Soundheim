using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace ValheimDevBridge;

internal sealed class LiveLogs : ILogListener
{
    private readonly Queue<string> lines = new();
    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        lock (lines)
        {
            lines.Enqueue($"[{eventArgs.Level}:{eventArgs.Source.SourceName}] {eventArgs.Data}");
            while (lines.Count > 500) lines.Dequeue();
        }
    }
    public string[] Read(string contains) { lock (lines) return lines.Where(line => line.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0).ToArray(); }
    public void Dispose() { lock (lines) lines.Clear(); }
}

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ValheimDevBridge;

internal sealed class Reply(byte[] body, string type = "application/json", int status = 200)
{
    public byte[] Body { get; } = body;
    public string Type { get; } = type;
    public int Status { get; } = status;
    public static Reply Json(object value, int status = 200) => new(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value)), status: status);
}

internal sealed class Request(string method, string path, string filter)
{
    public string Method { get; } = method;
    public string Path { get; } = path;
    public string Filter { get; } = filter;
    public TaskCompletionSource<Reply> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class LocalApi : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly string key;
    private volatile bool stopped;
    public ConcurrentQueue<Request> Requests { get; } = new();

    public LocalApi(string key, int port)
    {
        this.key = key;
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        _ = Serve();
    }

    private async Task Serve()
    {
        while (!stopped)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException) { break; }
            try
            {
                Reply reply;
                if (context.Request.Headers["Origin"] != null || context.Request.Headers["X-Valheim-Dev-Key"] != key)
                    reply = Reply.Json(new { error = "Unauthorized" }, 403);
                else
                {
                    var request = new Request(context.Request.HttpMethod, context.Request.Url?.AbsolutePath ?? "", context.Request.QueryString["contains"] ?? "");
                    Requests.Enqueue(request);
                    Task finished = await Task.WhenAny(request.Completion.Task, Task.Delay(20000)).ConfigureAwait(false);
                    if (finished != request.Completion.Task)
                    {
                        request.Completion.TrySetCanceled();
                        reply = Reply.Json(new { error = "The game did not respond within 20 seconds." }, 504);
                    }
                    else reply = await request.Completion.Task.ConfigureAwait(false);
                }
                context.Response.StatusCode = reply.Status;
                context.Response.ContentType = reply.Type;
                context.Response.Headers["Cache-Control"] = "no-store";
                context.Response.ContentLength64 = reply.Body.Length;
                await context.Response.OutputStream.WriteAsync(reply.Body, 0, reply.Body.Length).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException || ex is System.IO.IOException || ex is TaskCanceledException) { }
            finally { context.Response.Close(); }
        }
    }

    public void Dispose()
    {
        stopped = true;
        listener.Close();
        while (Requests.TryDequeue(out Request request)) request.Completion.TrySetCanceled();
    }
}

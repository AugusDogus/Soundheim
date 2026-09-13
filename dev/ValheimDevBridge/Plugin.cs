using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace ValheimDevBridge;

[BepInPlugin("augusdogus.dev.bridge", "Valheim development bridge", "1.0.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const string TargetId = "augusdogus.mods.Soundheim";
    private LocalApi? api;
    private ConfigEntry<string>? assemblyPath;
    private readonly LiveLogs logs = new();
    private int reloadCount;
    private string loadedHash = "";

    private void Awake()
    {
        assemblyPath = Config.Bind("Development", "Assembly path", "", "Local Soundheim.dll build to load on POST /reload. This bridge is for development only.");
        string key = File.ReadAllText(Path.Combine(Paths.ConfigPath, "ValheimDevBridge.key")).Trim();
        if (key.Length < 32) throw new InvalidOperationException("The development bridge needs a random access key of at least 32 characters.");
        BepInEx.Logging.Logger.Listeners.Add(logs);
        api = new LocalApi(key, 19287);
        Logger.LogInfo("Local development API listening on 127.0.0.1:19287.");
    }

    private void Update()
    {
        if (api == null) return;
        while (api.Requests.TryDequeue(out Request request))
        {
            if (request.Completion.Task.IsCompleted) continue;
            try
            {
                switch (request.Method + " " + request.Path)
                {
                    case "GET /status": request.Completion.TrySetResult(Status()); break;
                    case "GET /logs": request.Completion.TrySetResult(Reply.Json(logs.Read(request.Filter))); break;
                    case "GET /ui": request.Completion.TrySetResult(Ui()); break;
                    case "POST /reload": StartCoroutine(Reload(request)); break;
                    case "POST /screenshot": StartCoroutine(Screenshot(request)); break;
                    default: request.Completion.TrySetResult(Reply.Json(new { error = "Unknown method or endpoint" }, 404)); break;
                }
            }
            catch (Exception ex) { Fail(request, ex); }
        }
    }

    private Reply Status()
    {
        Chainloader.PluginInfos.TryGetValue(TargetId, out PluginInfo info);
        BaseUnityPlugin? target = info?.Instance;
        using var process = Process.GetCurrentProcess();
        return Reply.Json(new
        {
            pid = process.Id,
            reloadCount,
            loadedHash,
            settingsOpen = Settings.instance != null,
            modLoaded = target != null,
            selection = ReadProperty(target, "Selection"),
            status = ReadProperty(target, "Status"),
            devices = ReadProperty(target, "Devices")
        });
    }

    private static object? ReadProperty(object? target, string name) => target?.GetType().GetProperty(name, Members)?.GetValue(target);

    private static Reply Ui()
    {
        if (Settings.instance == null) return Reply.Json(new { error = "Open game settings to inspect the Audio tab." }, 409);
        var audio = Settings.instance.GetComponentInChildren<Valheim.SettingsGui.AudioSettings>(true);
        if (audio == null) return Reply.Json(new { error = "Audio tab not found" }, 404);
        return Reply.Json(audio.GetComponentsInChildren<RectTransform>(true).Take(250).Select(rect => new
        {
            name = rect.name,
            active = rect.gameObject.activeInHierarchy,
            text = rect.GetComponent<TMP_Text>()?.text,
            x = rect.position.x, y = rect.position.y,
            width = rect.rect.width, height = rect.rect.height,
            components = rect.GetComponents<Component>().Where(component => component != null).Select(component => component.GetType().FullName).ToArray()
        }).ToArray());
    }

    private IEnumerator Reload(Request request)
    {
        byte[] bytes;
        Type type;
        PluginInfo info;
        Task? idle;
        try
        {
            if (assemblyPath == null || !Path.IsPathRooted(assemblyPath.Value)) throw new InvalidOperationException("Set an absolute Assembly path in the bridge config first.");
            if (!Chainloader.PluginInfos.TryGetValue(TargetId, out info)) throw new InvalidOperationException("The audio mod is not loaded.");
            bytes = File.ReadAllBytes(assemblyPath.Value);
            Assembly assembly = Assembly.Load(bytes);
            type = assembly.GetTypes().Single(candidate => typeof(BaseUnityPlugin).IsAssignableFrom(candidate) && candidate.GetCustomAttribute<BepInPlugin>()?.GUID == TargetId);
            MethodInfo prepare = info.Instance.GetType().GetMethod("PrepareForReload", Members) ?? throw new InvalidOperationException("This installed build does not support live reload. Install the reload-enabled build and restart once.");
            idle = prepare.Invoke(info.Instance, null) as Task;
        }
        catch (Exception ex) { Fail(request, ex); yield break; }
        float deadline = Time.realtimeSinceStartup + 15;
        while (idle != null && !idle.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (idle != null && !idle.IsCompleted)
        {
            info.Instance.enabled = true;
            request.Completion.TrySetResult(Reply.Json(new { error = "Audio command is still running; old mod retained. Retry reload." }, 409));
            yield break;
        }
        Destroy(info.Instance);
        yield return null;
        try
        {
            var instance = Chainloader.ManagerObject.AddComponent(type);
            typeof(PluginInfo).GetProperty("Instance", Members)?.SetValue(info, instance);
            using var sha = SHA256.Create();
            loadedHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            reloadCount++;
            Logger.LogInfo($"Reloaded audio mod #{reloadCount}: {loadedHash}");
            request.Completion.TrySetResult(Status());
        }
        catch (Exception ex) { Fail(request, ex); }
    }

    private IEnumerator Screenshot(Request request)
    {
        yield return new WaitForEndOfFrame();
        Texture2D? texture = null;
        try
        {
            texture = ScreenCapture.CaptureScreenshotAsTexture();
            request.Completion.TrySetResult(new Reply(ImageConversion.EncodeToPNG(texture), "image/png"));
        }
        catch (Exception ex) { Fail(request, ex); }
        finally { if (texture != null) Destroy(texture); }
    }

    private void Fail(Request request, Exception ex)
    {
        string error = ex.GetBaseException().ToString();
        Logger.LogError(error);
        request.Completion.TrySetResult(Reply.Json(new { error }, 500));
    }

    private void OnDestroy()
    {
        api?.Dispose();
        BepInEx.Logging.Logger.Listeners.Remove(logs);
        logs.Dispose();
    }
}

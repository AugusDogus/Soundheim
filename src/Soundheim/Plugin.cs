using BepInEx;

namespace Soundheim;

[BepInPlugin(PluginId, "Soundheim", PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginId = "augusdogus.mods.Soundheim";
    public const string PluginVersion = "1.0.0";

    private void Awake()
    {
        Logger.LogInfo("Soundheim loaded.");
    }
}

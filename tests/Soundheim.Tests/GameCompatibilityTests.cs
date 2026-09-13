using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace Soundheim.Tests;

[TestClass]
public sealed class GameCompatibilityTests
{
    [TestMethod]
    public void NativeSettingsHooksMatchTheInstalledGame()
    {
        string managed = Environment.GetEnvironmentVariable("MANAGED_DIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed");
        using var game = AssemblyDefinition.ReadAssembly(Path.Combine(managed, "assembly_valheim.dll"));
        TypeDefinition audio = game.MainModule.Types.Single(type => type.FullName == "Valheim.SettingsGui.AudioSettings");
        foreach (string name in new[] { "Initialize", "OnTabOpen", "OnOkAsync", "OnBack" })
            Assert.AreEqual(1, audio.Methods.Count(method => method.Name == name), $"Audio hook {name} changed.");
        Assert.AreEqual("UnityEngine.UI.Toggle", audio.Fields.Single(field => field.Name == "m_continousMusic").FieldType.FullName);
        Assert.AreEqual("UnityEngine.UI.Slider", audio.Fields.Single(field => field.Name == "m_volumeSlider").FieldType.FullName);
        Assert.AreEqual("TMPro.TMP_Text", audio.Fields.Single(field => field.Name == "m_volumeText").FieldType.FullName);
        TypeDefinition graphics = game.MainModule.Types.Single(type => type.FullName == "Valheim.SettingsGui.GraphicsSettings");
        Assert.AreEqual("GUIFramework.GuiDropdown", graphics.Fields.Single(field => field.Name == "m_resolutionDropdown").FieldType.FullName);
        var open = audio.Methods.Single(method => method.Name == "OnTabOpen");
        CollectionAssert.AreEqual(new[] { "backButton", "okButton" }, open.Parameters.Select(parameter => parameter.Name).ToArray());
        using var gui = AssemblyDefinition.ReadAssembly(Path.Combine(managed, "gui_framework.dll"));
        TypeDefinition dropdown = gui.MainModule.Types.Single(type => type.FullName == "GUIFramework.GuiDropdown");
        Assert.AreEqual("TMPro.TMP_Dropdown", dropdown.BaseType.FullName);
        Assert.IsTrue(dropdown.Events.Any(item => item.Name == "OnExpandedStateChange"));
    }
}

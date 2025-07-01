using HarmonyLib;
using SDG.Framework.Modules;
using SDG.Unturned;
using UnityEngine;

namespace RoadSelectionTool;

public class ModuleNexus : IModuleNexus
{
    public static Harmony Patcher { get; } = new Harmony("RoadSelectionTool.Module");
    
    public void initialize()
    {
        ModuleHook.onModulesInitialized += this.OnModulesInit;
        Level.onPostLevelLoaded += this.OnPostLevelLoaded;
        UnturnedLog.info("RoadSelectionTool Module Nexus loaded.");
    }
    
    private void OnModulesInit()
    {

    }

    private void OnPostLevelLoaded(int _)
    {
        CommandWindow.Log("OnPostLevelLoaded");
        
        if (!Level.isEditor) return;
        
        var multiSelectToolObject = new GameObject("RoadSelectionTool");
        multiSelectToolObject.AddComponent<RoadSelectionTool.Module.Editor.Tools.RoadSelectionTool>();
    }

    public void shutdown()
    {
        ModuleHook.onModulesInitialized -= this.OnModulesInit;
    }
}
using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using KuriosityScience.Utilities;
using KuriosityScience.Models;
using BepInEx.Logging;
using BepInEx.Configuration;
using KSP.Game;
using Newtonsoft.Json;

namespace KuriosityScience;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class KuriositySciencePlugin : BaseSpaceWarpPlugin
{
    // Useful in case some other mod wants to use this mod a dependency
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    // Singleton instance of the plugin class
    [PublicAPI]
    public static KuriositySciencePlugin Instance { get; set; }

    public static Dictionary<string, KuriosityExperiment> KuriosityExperiments;

    private const bool debugMode = false;

    // Logger
    public new static ManualLogSource Logger { get; private set; }

    // UI window state
    private bool _isWindowOpen;
    private Rect _windowRect;

    // AppBar button IDs
    private const string ToolbarFlightButtonID = "BTN-KuriosityScienceFlight";

    // Base Kuriosity Factor configuration
    internal ConfigEntry<double> baseKuriosityFactor;

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Logger = base.Logger;
        Instance = this;

        // Register Flight AppBar button
        if(debugMode)
        {
            Appbar.RegisterAppButton(
                ModName,
                ToolbarFlightButtonID,
                AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
                isOpen =>
                {
                    _isWindowOpen = isOpen;
                    GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
                }
            );
        }
        

        // Register all Harmony patches in the project
        Harmony.CreateAndPatchAll(typeof(KuriositySciencePlugin).Assembly);

        KuriosityExperiments = new();

        GameManager.Instance.Assets.LoadByLabel<TextAsset>("kuriosity_experiment", RegisterKuriosityExperiment,
            assets => { GameManager.Instance.Assets.ReleaseAsset(assets); });
    }

    private static void RegisterKuriosityExperiment(TextAsset asset)
    {
       
        var experiment = JsonConvert.DeserializeObject<KuriosityExperiment>(asset.text);

        Logger.LogDebug($"Experiment loaded: {experiment.ExperimentId}");

        KuriosityExperiments.Add(experiment.ExperimentId, experiment);
    }

    public override void OnPostInitialized()
    {
        base.OnPostInitialized();

        SetupConfiguration();
    }

    private void SetupConfiguration()
    {
        baseKuriosityFactor = Config.Bind("Kuriosity Science", $"Base Kuriosity Factor", 1.0,
            new ConfigDescription("Base Kuriosity Factor.", new AcceptableValueList<double>([0.01,0.1,0.5,1.0,5.0,10.0,50.0])));
    }

    /// <summary>
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI()
    {
        // Set the UI
        GUI.skin = Skins.ConsoleSkin;

        if (_isWindowOpen)
        {
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect,
                FillWindow,
                "Kuriosity Science",
                GUILayout.Height(350),
                GUILayout.Width(350)
            );
        }
    }

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private void FillWindow(int windowID)
    {
        GUILayout.Label("Kuriosity Science - Random occurrences of kurious science events!");
        //GUI.DragWindow(new Rect(0, 0, 10000, 500));
        if (GUILayout.Button("Kerbin Low Orbit"))
        {
            //Logger.LogInfo($"Button Pressed");
            DebugUtilities.SetOrbit("Kerbin",100);
            //Logger.LogInfo($"Attempting to set Orbit");
        }
        if (GUILayout.Button("Kerbin High Orbit"))
        {
            //Logger.LogInfo($"Button Pressed");
            DebugUtilities.SetOrbit("Kerbin", 1000);
            //Logger.LogInfo($"Attempting to set Orbit");
        }
        if (GUILayout.Button("Jool High Orbit"))
        {
            //Logger.LogInfo($"Button Pressed");
            DebugUtilities.SetOrbit("Jool", 6000);
            //Logger.LogInfo($"Attempting to set Orbit");
        }
        if (GUILayout.Button("Mun Low Orbit"))
        {
            //Logger.LogInfo($"Button Pressed");
            DebugUtilities.SetOrbit("Mun", 10);
            //Logger.LogInfo($"Attempting to set Orbit");
        }
    }
}


using KSP.Game;
using KSP.Sim.impl;
using UnityEngine;

namespace KuriosityScience.Utilities;

internal static class DebugUtilities
{
    public static void SetOrbit(string body, float altitudeKM)
    {
        GameInstance game = GameManager.Instance.Game;
        VesselComponent vessel;

        GUIStyle errorStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
        errorStyle.normal.textColor = Color.red;

        if ((vessel = game.ViewController.GetActiveSimVessel()) == null)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("No active vessel.", errorStyle);
            GUILayout.FlexibleSpace();
            return;
        }

        game.SpaceSimulation.Lua.TeleportToOrbit(
            vessel.Guid,
            body,
            0,
            0,
            (double)altitudeKM * 1000f + GameManager.Instance.Game.CelestialBodies.GetRadius(body),
            0,
            0,
            0,
            0);
    }
}

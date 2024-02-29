using UnityEngine;
using KuriosityScience.Utilities;
using Random = System.Random;
using KSP.Game;
using KuriosityScience.Modules;
using KSP.Sim.impl;
using Newtonsoft.Json;

namespace KuriosityScience.Models;

/// <summary>
///     The controller object that gets assigned to each kerbal - holding references to all
///     that kerbal's experiment trackers, and managing which experiment is the currently running one
///     
///     This object is intended to be saved as part of KuriosityControllers in the module data
/// </summary>
[Serializable]
public class KuriosityController
{
    [SerializeField]
    [Tooltip("Kerbal ID")]
    public Guid KerbalId;

    [SerializeField]
    [Tooltip("Currently active Experiment")]
    public string ActiveExperimentId = string.Empty;

    [JsonIgnore]
    public KuriosityExperimentTracker ActiveExperimentTracker
    {
        get
        {
            if (!ExperimentTrackers.TryGetValue(ActiveExperimentId, out KuriosityExperimentTracker tracker))
            {
                return null;
            }
            return tracker;
        }
    }

    [SerializeField]
    [Tooltip("Kuriosity experiment trackers")]
    public Dictionary<string, KuriosityExperimentTracker> ExperimentTrackers = new();

    /// <summary>
    ///     Create a list of best experiments for this kerbal to run, based on Validity -> Priority/Non-Priority -> Running, Paused, then Initialized
    /// </summary>
    private List<KuriosityExperimentTracker> GetBestExperiments()
    {
        List<KuriosityExperimentPrecedence> precedences = new List<KuriosityExperimentPrecedence>
        {
            KuriosityExperimentPrecedence.Priority,
            KuriosityExperimentPrecedence.NonPriority
        };

        List<KuriosityExperimentState> states = new List<KuriosityExperimentState>
        {
            KuriosityExperimentState.Running,
            KuriosityExperimentState.Paused,
            KuriosityExperimentState.Initialized
        };

        foreach (KuriosityExperimentPrecedence precedence in precedences)
        {
            foreach (KuriosityExperimentState state in states)
            {
                List<KuriosityExperimentTracker> bestExperiments = ExperimentTrackers.Values.Where(e => e.ExperimentPrecedence == precedence && e.State == state).ToList();
                if (bestExperiments.Count > 0)
                {
                    return bestExperiments;
                }
            }
        }
        return null;
    }

    /// <summary>
    ///     Select a random experiment from a list of 'best' experiments that could be run, and make it the active running experiment
    /// </summary>
    /// <param name="currentKuriosityFactor">The current Kuriosity Factor that should be applied to the running experiment</param>
    public void UpdateActiveExperiment(double currentKuriosityFactor)
    {
        string newActiveExperimentId;

        List<KuriosityExperimentTracker> bestExperiments = GetBestExperiments();
        if (bestExperiments == null)
        {
            newActiveExperimentId = string.Empty;
        }
        else
        {
            Random rnd = new Random();
            int index = rnd.Next(bestExperiments.Count);
            newActiveExperimentId = bestExperiments[index].Experiment.ExperimentId;
        }

        if (string.IsNullOrEmpty(ActiveExperimentId) || newActiveExperimentId != ActiveExperimentId)
        {
            if (!string.IsNullOrEmpty(ActiveExperimentId) && ActiveExperimentTracker.State != KuriosityExperimentState.Completed)
            {
                ActiveExperimentTracker.State = KuriosityExperimentState.Paused;
                KuriositySciencePlugin.Logger.LogDebug($"Experiment paused: {ActiveExperimentId} TimeLeft: {Utility.ToDateTime(ActiveExperimentTracker.TimeLeft)}");

                ActiveExperimentId = string.Empty;
            } else if (!string.IsNullOrEmpty(newActiveExperimentId))
            {
                ActiveExperimentId = newActiveExperimentId;
                ActiveExperimentTracker.State = KuriosityExperimentState.Running;
                ActiveExperimentTracker.CurrentKuriosityFactor = currentKuriosityFactor;

                KuriositySciencePlugin.Logger.LogDebug($"Experiment running: {ActiveExperimentId} TimeLeft: {Utility.ToDateTime(ActiveExperimentTracker.TimeLeft)}");
            } else
            {
                ActiveExperimentId = string.Empty;
                KuriositySciencePlugin.Logger.LogDebug($"All possible experiments completed");

            }
        }
    }

    /// <summary>
    ///     Decrease the Time Left on the Active experiment
    /// </summary>
    /// <param name="deltaUniversalTime">The amount of time to decrease by</param>
    /// <param name="multiplier">the rate multiplier to apply</param>
    public bool UpdateActiveExperimentTimeLeft(double deltaUniversalTime, double multiplier)
    {
        if (string.IsNullOrEmpty(ActiveExperimentId)) return false;

        return ActiveExperimentTracker.UpdateTick(deltaUniversalTime, multiplier);
    }

    /// <summary>
    ///     Update the validity and precedence of this kerbals experiments
    /// </summary>
    /// <param name="vessel">Reference to the vessel of the calling part component module</param>
    /// <param name="dataKuriosityScience">Reference to the data of the calling part component module</param>
    public void UpdateExperimentPrecedences(VesselComponent vessel, Data_KuriosityScience dataKuriosityScience)
    {
        foreach (KuriosityExperimentTracker experimentTracker in ExperimentTrackers.Values)
        {
            experimentTracker.UpdateExperimentPrecedence(vessel, dataKuriosityScience, KerbalId);
        }
    }

    /// <summary>
    ///     Return a Kuriosity Controller (or create a new one if it doesn't exist) for the kerbal
    /// </summary>
    /// <param name="kerbal">The kerbal who will own the Kuriosity Controller</param>
    /// <param name="dataKuriosityScience">Reference to the data of the calling part component module</param>
    /// <returns></returns>
    public static KuriosityController GetKuriosityController(KerbalInfo kerbal, Data_KuriosityScience dataKuriosityScience)
    {
        if (!dataKuriosityScience.KuriosityControllers.TryGetValue(kerbal.Id, out KuriosityController controller))
        {
            controller = new KuriosityController()
            {
                KerbalId = kerbal.Id,
                ExperimentTrackers = new()
            };

            dataKuriosityScience.KuriosityControllers.Add(kerbal.Id.Guid, controller);

            KuriositySciencePlugin.Logger.LogDebug($"New kuriosity controller created for: " + kerbal.Attributes.GetFullName() + " : " + controller.KerbalId);
        }

        return controller;
    }

    /// <summary>
    ///     Refreshes the tracked experiment states for this controller, and updates the active experiment
    /// </summary>
    /// <param name="vessel">this vessel</param>
    /// <param name="dataKuriosityScience">this parts kuriosity science module data</param>
    public void RefreshControllerExperimentStates(VesselComponent vessel, Data_KuriosityScience dataKuriosityScience)
    {
        UpdateExperimentPrecedences(vessel, dataKuriosityScience);

        UpdateActiveExperiment(dataKuriosityScience.GetKuriosityFactor());
    }
}

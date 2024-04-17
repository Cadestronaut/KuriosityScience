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
    ///     Select the best experiment to be running based on priority and state.
    /// </summary>
    private string ChooseNextExperimentId()
    {
        if (ExperimentTrackers.Count == 0)
            return string.Empty;

        // Use a Span to access the value list without allocations.
        Span<KuriosityExperimentTracker> experiments = ExperimentTrackers.Values.ToArray();

        // Capture the id and fitness of the first element, then we can compare all others
        // against it.
        string bestId = experiments[0].ExperimentId;
        int bestFit   = experiments[0].Fitness();

        // Count how many experiments match each fitness value, which means that experiments[0]
        // will match, so before we start the loop the count needs to be zero, whereas inside
        // the loop we set it to 1 on first encounter with a better fitness.
        int fitCount  = 0;

        foreach (var experiment in experiments)
        {
            var fitness = experiment.Fitness();
            if (fitness > bestFit)
            {
                // This experiment has a higher fitness than the previous candidate, so switch to it.
                bestId = experiment.ExperimentId;
                bestFit = fitness;
                fitCount = 1;
            }
            else if (fitness == bestFit)
            {
                fitCount++;
            }
        }

        // No matches.
        if (bestFit < 0)
            return string.Empty;

        // If there was one or less matches, we can return whatever bestId contains
        if (fitCount <= 1)
            return bestId;

        // Reaching this point should be infrequent based on when a new experiment actually needs
        // selecting, in which case we can pick a random number between 0,fitCount then revisit the
        // experiment tracker and count down to that random index.
        Random rng = new Random();
        int pickCountdown = rng.Next(bestFit) + 1;
        foreach (var experiment in experiments)
        {
            var fitness = experiment.Fitness();
            if (fitness >= bestFit && --pickCountdown <= 0)
                return experiment.ExperimentId;
        }

        // Fallback: return the last matched ExperimentID
        return bestId;
    }

    /// <summary>
    ///     Select a random experiment from a list of 'best' experiments that could be run, and make it the active running experiment
    /// </summary>
    /// <param name="currentKuriosityFactor">The current Kuriosity Factor that should be applied to the running experiment</param>
    public void UpdateActiveExperiment(double currentKuriosityFactor)
    {
        string newActiveExperimentId = ChooseNextExperimentId();
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

using KSP.Sim.impl;
using KSP.Game;
using KuriosityScience.Models;
using KSP.Messages;
using KuriosityScience.Utilities;

namespace KuriosityScience.Modules;

public class PartComponentModule_KuriosityScience : PartComponentModule
{
    public override Type PartBehaviourModuleType => typeof(Module_KuriosityScience);

    // Module data
    private Data_KuriosityScience _dataKuriosityScience;

    // Useful game objects
    private KerbalRosterManager _rosterManager;
    
    // Useful part objects
    private List<KerbalInfo> kerbalsInPart = [];

    // Constants
    private const double KURIOSITY_FACTOR_SCALING_FACTOR = 0.3;

    // Something I will remove soon...
    private List<string> badIds = new List<string>
        {
            "space_sickness",
            "bugs",
            "mun_cheesecake",
            "kedankenexperiment",
            "ghost_in_the_machine",
            "kabin_fever",
            "micrometeor",
            "ufk",
            "karicebo"
        };

    // This triggers when Flight scene is loaded. It triggers for active vessels also.
    public override void OnStart(double universalTime)
    {
        KuriositySciencePlugin.Logger.LogDebug($"PartComponent OnStart started for: {Part.PartName} on {Part.PartOwner.SimulationObject.Vessel.Name}");

        if (!DataModules.TryGetByType(out _dataKuriosityScience))
        {
            KuriositySciencePlugin.Logger.LogError("Unable to find a Data_KuriosityScience in the PartComponentModule for " + Part.PartName);
            return;
        } else if (GameManager.Instance == null || GameManager.Instance.Game.ScienceManager == null)
        {
            KuriositySciencePlugin.Logger.LogError("Unable to find a valid game instance");
        } else
        {
            _rosterManager = GameManager.Instance.Game.SessionManager.KerbalRosterManager;
        }

        if (_dataKuriosityScience.KuriosityControllers == null)
        {
            _dataKuriosityScience.KuriosityControllers = new();
        }

        // Load Kuriosity Experiments
        _dataKuriosityScience.KuriosityExperiments = KuriositySciencePlugin.KuriosityExperiments.Where(e => _dataKuriosityScience.AllowedKuriosityExperiments.Contains(e.Key)).Select(e => e.Value).ToList();

        // Temporary cleaning out of older Ids
        CleanOldExperimentIds();

        // refresh the list of kerbals in this part
        kerbalsInPart = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId);

        // Initialize controllers & experiments
        UpdateKuriosityFactor();
        foreach (KerbalInfo kerbal in kerbalsInPart)
        {
            //refresh all the controllers to check we're working on a valid / best experiment
            ControllerRefresh(kerbal);
        }

        // Message listeners
        Game.Messages.Subscribe<KerbalLocationChanged>(OnKerbalLocationChanged);
        Game.Messages.Subscribe<VesselScienceSituationChangedMessage>(OnVesselScienceSituationChanged);
        Game.Messages.Subscribe<VesselCommNetConnectionStatusChangedMessage>(OnVesselCommNetStatusChanged);
        Game.Messages.Subscribe<VesselChangedMessage>(OnVesselChanged); //vesselSplit??
        Game.Messages.Subscribe<KerbalRemovedFromRoster>(OnKerbalRemovedFromRoster);
    }

    // This starts triggering when vessel is placed in Flight. Does not trigger in OAB.
    // Keeps triggering in every scene once it's in Flight 
    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        UpdateKerbalExperiments(deltaUniversalTime);
    }

    /// <summary>
    ///     Updates the active experiments for each of the kerbals in the part, and triggers completion if needed
    /// </summary>
    /// <param name="deltaUniversalTime">the time slice (s) of this update</param>
    public void UpdateKerbalExperiments(double deltaUniversalTime)
    {
        //Loop through each kerbal in part
        foreach (KerbalInfo kerbal in kerbalsInPart)
        {
            //check that we're working on an appropriate experiment
            KuriosityController controller = KuriosityController.GetKuriosityController(kerbal, _dataKuriosityScience);

            if(controller.UpdateActiveExperimentTimeLeft(deltaUniversalTime, _dataKuriosityScience.GetKuriosityFactor()))
            {
                VesselComponent vessel = Part.PartOwner.SimulationObject.Vessel;

                string completedExperimentId = controller.ActiveExperimentId;

                // Experiment has completed, need to record it
                controller.ActiveExperimentTracker.TriggerExperiment(kerbal, vessel);

                DeprioritizeExperimentAcrossVessel(completedExperimentId, vessel, kerbal.Id);

                controller.RefreshControllerExperimentStates(vessel, _dataKuriosityScience);
            }
        }
    }

    private void DeprioritizeExperimentAcrossVessel(string experimentId, VesselComponent vessel, Guid callingKerbalId)
    {
        if (string.IsNullOrEmpty(experimentId)) return;

        foreach (Data_KuriosityScience dataKuriosityScience in GetAll_Data_KuriosityScience(vessel))
        {
            foreach (KuriosityController otherController in dataKuriosityScience.KuriosityControllers.Values)
            {
                if(otherController.KerbalId != callingKerbalId)
                {
                    foreach (KuriosityExperimentTracker tracker in otherController.ExperimentTrackers.Values)
                    {
                        if (tracker.ExperimentId == experimentId)
                        {
                            tracker.ExperimentPrecedence = KuriosityExperimentPrecedence.DePrioritized;
                            KuriositySciencePlugin.Logger.LogDebug($"{experimentId} deprioritized for {otherController.KerbalId}");
                            if (tracker.State == KuriosityExperimentState.Running) tracker.State = KuriosityExperimentState.Paused;
                            if (otherController.ActiveExperimentId == tracker.ExperimentId) otherController.ActiveExperimentId = string.Empty;
                        }
                    }

                    otherController.RefreshControllerExperimentStates(vessel, dataKuriosityScience);
                }
            }
        }
    }


    private List<Data_KuriosityScience> GetAll_Data_KuriosityScience(VesselComponent vessel)
    {
        //TODO - Make this generic and add to my Utility class
        List<Data_KuriosityScience> returnDataModules = new();

        var parts = vessel.SimulationObject.PartOwner.Parts;
        foreach (var part in parts)
        {
            if (part.TryGetModuleData<PartComponentModule_KuriosityScience, Data_KuriosityScience>(out var m))
            {
                returnDataModules.Add(m as Data_KuriosityScience);
            }
        }

        return returnDataModules;
    }

    /// <summary>
    ///     Refreshes the controller by synchronising with the allowed experiments for the part, and updating the tracked experiment states / validity
    /// </summary>
    /// <param name="kerbal">this kerbal</param>
    private void ControllerRefresh(KerbalInfo kerbal)
    {
        KuriosityController controller = KuriosityController.GetKuriosityController(kerbal, _dataKuriosityScience);

        SyncKerbalExperimentsWithPart(controller);

        controller.RefreshControllerExperimentStates(Part.PartOwner.SimulationObject.Vessel, _dataKuriosityScience);
    }

    /// <summary>
    ///     synchronises the the allowed experiments for the part with this controller
    /// </summary>
    /// <param name="controller">the controller to synchronise this part's experiments with</param>
    private void SyncKerbalExperimentsWithPart(KuriosityController controller)
    {
        //Check we're tracking all possible experiments from this part
        foreach (KuriosityExperiment experiment in _dataKuriosityScience.KuriosityExperiments)
        {
            if (!controller.ExperimentTrackers.ContainsKey(experiment.ExperimentId))
            {
                KuriosityExperimentTracker experimentTracker = KuriosityExperimentTracker.CreateExperimentTracker(experiment);
                controller.ExperimentTrackers.Add(experiment.ExperimentId, experimentTracker);
            }
        }
    }

    /// <summary>
    ///     Run when a kerbal changes location (subscribed to KerbalLocationChanged message), to move the kerbal's controller from the old location to the
    ///     new location and then refresh it.
    /// </summary>
    /// <param name="msg">the event message</param>
    private void OnKerbalLocationChanged(MessageCenterMessage msg)
    {
        if (msg is not KerbalLocationChanged kerbalLocationChanged)
            return;

        // update the list of kerbals in the part
        kerbalsInPart = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId);

        // Kerbal
        KerbalInfo kerbal = kerbalLocationChanged.Kerbal;

        // Old location
        var oldLocationId = kerbalLocationChanged.OldLocation.SimObjectId;
        var oldSimObject = Game.UniverseModel.FindSimObject(oldLocationId);
        var oldPart = oldSimObject.Part;
        oldPart.TryGetModuleData<PartComponentModule_KuriosityScience, Data_KuriosityScience>(out var oldData);

        // New location
        var newLocationId = kerbalLocationChanged.Kerbal.Location.SimObjectId;
        var newSimObject = Game.UniverseModel.FindSimObject(newLocationId);
        var newPart = newSimObject.Part;
        newPart.TryGetModuleData<PartComponentModule_KuriosityScience, Data_KuriosityScience>(out var newData);

        // Did the kerbal enter this part?
        if (newLocationId.Equals(Part.SimulationObject.GlobalId) && oldLocationId != newLocationId)
        {
            // move this kerbal's controller from the old part to the new part
            if (newData.KuriosityControllers.TryAdd(kerbal.Id, oldData.KuriosityControllers[kerbal.Id]))
            {
                newData.KuriosityControllers.Remove(kerbal.Id);
                newData.KuriosityControllers.Add(kerbal.Id, oldData.KuriosityControllers[kerbal.Id]);
            }

            //remove the old controller
            oldData.KuriosityControllers.Remove(kerbal.Id);

            //run updates
            UpdateKuriosityFactor();

            ControllerRefresh(kerbal);
        }
    }

    /// <summary>
    ///     Runs when a kerbal gets removed from the roster (e.g. a kerbal dies because of lack of life support)
    /// </summary>
    /// <param name="msg"></param>
    private void OnKerbalRemovedFromRoster(MessageCenterMessage msg)
    {
        if (msg is not KerbalRemovedFromRoster kerbalRemovedFromRoster) return;

        //Check if we have a controller for this kerbal and if we do, remove it
        _dataKuriosityScience.KuriosityControllers.Remove(kerbalRemovedFromRoster.Kerbal.Id);
    }

    /// <summary>
    ///     Runs when thee vessel's commnet state changes
    /// </summary>
    /// <param name="msg"></param>
    private void OnVesselCommNetStatusChanged(MessageCenterMessage msg)
    {
        if (msg is not VesselCommNetConnectionStatusChangedMessage vesselCommNetConnectionStatusChanged) return;

        if (vesselCommNetConnectionStatusChanged.Vessel.GlobalId != Part.PartOwner.SimulationObject.Vessel.GlobalId) return;

        kerbalsInPart = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId);

        UpdateKuriosityFactor();

        foreach (KerbalInfo kerbal in kerbalsInPart)
        {
            KuriosityController controller = KuriosityController.GetKuriosityController(kerbal, _dataKuriosityScience);

            controller.RefreshControllerExperimentStates(vesselCommNetConnectionStatusChanged.Vessel, _dataKuriosityScience);
        }
    }

    /// <summary>
    ///     Runs when the vessel changes (assumed to include if the vessel joins or separates parts)
    /// </summary>
    /// <param name="msg"></param>
    private void OnVesselChanged(MessageCenterMessage msg) //TODO - need more testing around this
    {
        if (msg is not VesselChangedMessage vesselChanged) return;

        if (vesselChanged.Vessel.GlobalId != Part.PartOwner.SimulationObject.Vessel.GlobalId) return;

        kerbalsInPart = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId);

        UpdateKuriosityFactor();

        foreach (KerbalInfo kerbal in kerbalsInPart)
        {
            KuriosityController controller = KuriosityController.GetKuriosityController(kerbal, _dataKuriosityScience);

            controller.RefreshControllerExperimentStates(vesselChanged.Vessel, _dataKuriosityScience);
        }
    }

    /// <summary>
    ///     Run when a vessel's Science Situation changes, so that the kuriosity factor can be updates and all the kerbal's controllers / tracked experiments can be checked for validity
    /// </summary>
    /// <param name="msg">the event message</param>
    private void OnVesselScienceSituationChanged(MessageCenterMessage msg)
    {
        if (msg is not VesselScienceSituationChangedMessage vesselScienceSituationChanged)
            return;

        if (vesselScienceSituationChanged.Vessel.GlobalId != Part.PartOwner.SimulationObject.Vessel.GlobalId) return;

        kerbalsInPart = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId);

        UpdateKuriosityFactor();

        foreach (KerbalInfo kerbal in kerbalsInPart)
        {
            KuriosityController controller = KuriosityController.GetKuriosityController(kerbal, _dataKuriosityScience);
            
            controller.RefreshControllerExperimentStates(vesselScienceSituationChanged.Vessel, _dataKuriosityScience);
        }
    }

    /// <summary>
    ///     Updates the current kuriosity factors (which impacts the rate at which experiments reduce their time left
    /// </summary>
    public void UpdateKuriosityFactor()
    {
        double factor = Math.Pow(Utility.CurrentScienceMultiplier(Part.PartOwner.SimulationObject.Vessel), KURIOSITY_FACTOR_SCALING_FACTOR);
        factor *= _dataKuriosityScience.PartKuriosityFactorAdjustment;
        factor *= KuriositySciencePlugin.Instance.baseKuriosityFactor.Value;

        _dataKuriosityScience.BaseKuriosityFactor.SetValue(factor);
    }

    private void CleanOldExperimentIds()
    {
        foreach (KuriosityController controller in _dataKuriosityScience.KuriosityControllers.Values)
        {
            // Update ActiveExperimentId
            if (!string.IsNullOrEmpty(controller.ActiveExperimentId)) controller.ActiveExperimentId = UpdatedOldExperimentId(controller.ActiveExperimentId);

            // Update ExperimentTracker keys
            foreach (string badId in badIds)
            {
                if(controller.ExperimentTrackers.ContainsKey(badId))
                {
                    controller.ExperimentTrackers.Add(UpdatedOldExperimentId(badId), controller.ExperimentTrackers[badId]);
                    controller.ExperimentTrackers.Remove(badId);
                }
            }

            // Update ExperimentTracker experimentIds
            foreach (KuriosityExperimentTracker tracker in controller.ExperimentTrackers.Values)
            {
                tracker.ExperimentId = UpdatedOldExperimentId(tracker.ExperimentId);
            }
        }
    }

    private string UpdatedOldExperimentId(string experimentId)
    {
        // A bit hacky but it guarantees I only alter my own old Ids rather than any others        
        if (badIds.Contains(experimentId))
        {
            KuriositySciencePlugin.Logger.LogDebug($"Fixing old Id: {experimentId}");
            return "kuriosity_experiment_" + experimentId;
        }

        return experimentId;
    }

    public override void OnShutdown()
    {
        KuriositySciencePlugin.Logger.LogDebug($"OnShutdown triggered. Vessel '{Part?.PartOwner?.SimulationObject?.Vessel?.Name ?? "n/a"}' ");

        Game.Messages.Unsubscribe<KerbalLocationChanged>(OnKerbalLocationChanged);
        Game.Messages.Unsubscribe<VesselSituationChangedMessage>(OnVesselScienceSituationChanged);
        Game.Messages.Unsubscribe<VesselCommNetConnectionStatusChangedMessage>(OnVesselCommNetStatusChanged);
        Game.Messages.Unsubscribe<VesselChangedMessage>(OnVesselChanged); //vesselSplit??
        Game.Messages.Unsubscribe<KerbalRemovedFromRoster>(OnKerbalRemovedFromRoster);
    }
}

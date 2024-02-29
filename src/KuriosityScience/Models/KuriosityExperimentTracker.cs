using UnityEngine;
using KuriosityScience.Utilities;
using KSP.Game;
using KSP.Game.Science;
using KSP.Sim.impl;
using KuriosityScience.Modules;
using KSP.Messages;
using Newtonsoft.Json;
using KSP.Modules;
using KSP.Sim;

namespace KuriosityScience.Models;

/// <summary>
///     The tracks and updates progress against a specific Kuriosity Experiment for a kerbal. Also used to check validity of the experiment,
///     and to store the research report once the experiment has finished running.
/// </summary>
[Serializable]
public class KuriosityExperimentTracker
{
    // Constants
    private const double STDEV_PROPORTION_OF_MEAN = 3.0;
    private const double BASE_SCIENCE_MULTIPLIER = 1.0;
    private const double SCIENCE_MULTIPLIER_SCALING_FACTOR = 0.3;

    // Useful objects
    private SessionManager _sessionManager;
    private ScienceManager _scienceManager;
    private MessageCenter _messageCenter;
    private NotificationManager _notificationManager;
    private KerbalRosterManager _rosterManager;

    [SerializeField]
    [Tooltip("Base experiment Id")]
    public string ExperimentId;

    [JsonIgnore]
    private KuriosityExperiment _experiment;

    [JsonIgnore]
    [Tooltip("Base experiment")]
    public KuriosityExperiment Experiment
    {
        get 
        {
            if (_experiment == null)
            {
                _experiment = KuriositySciencePlugin.KuriosityExperiments[ExperimentId];
            }
            return _experiment;
        }
        set { _experiment = value; }
    }

    [SerializeField]
    [Tooltip("KuriosityScience Experiment time left")]
    private double _timeLeft;

    [JsonIgnore]
    public double TimeLeft
    {
        get { return _timeLeft / CurrentKuriosityFactor; }
    }

    [SerializeField]
    [Tooltip("KuriosityScience Experiment run state")]
    public KuriosityExperimentState State;

    [SerializeField]
    [Tooltip("Kuriosity experiment precedence")]
    public KuriosityExperimentPrecedence ExperimentPrecedence;

    [SerializeField]
    [Tooltip("Currently applied Kuriosity Factor")]
    public double CurrentKuriosityFactor;

    public KuriosityExperimentTracker()
    {
        // Initialize useful game objects
        _sessionManager = GameManager.Instance.Game.SessionManager;
        _scienceManager = GameManager.Instance.Game.ScienceManager;
        _messageCenter = GameManager.Instance.Game.Messages;
        _notificationManager = GameManager.Instance.Game.Notifications;
        _rosterManager = GameManager.Instance.Game.SessionManager.KerbalRosterManager;
    }

    /// <summary>
    ///     Run each time we want to decrease the Time Left on this experiment. 
    /// </summary>
    /// <param name="deltaUniversalTime">The amount of time to decrease by</param>
    /// <param name="multiplier">the rate multiplier to apply</param>
    /// <returns>Returns true if the experiment completes</returns>
    public bool UpdateTick(double deltaUniversalTime, double multiplier)
    {
        CurrentKuriosityFactor = multiplier;

        if (State != KuriosityExperimentState.Running)
        {
            State = KuriosityExperimentState.Running;
        }

        _timeLeft -= (deltaUniversalTime * CurrentKuriosityFactor);

        if (_timeLeft <= 0)
        {
            State = KuriosityExperimentState.Completed;

            KuriositySciencePlugin.Logger.LogDebug($"Experiment completed: {Experiment.ExperimentId} at UT: {Utility.ToDateTime(GameManager.Instance.Game.UniverseModel.UniverseTime)}");

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Creates a new kuriosity experiment tracker object based on the supplied experiment, and initializes it.
    /// </summary>
    /// <param name="experiment">The Kuriosity Experiment to create the tracker for</param>
    /// <returns>The new experiment tracker</returns>
    public static KuriosityExperimentTracker CreateExperimentTracker(KuriosityExperiment experiment)
    {
        KuriosityExperimentTracker experimentTracker = new KuriosityExperimentTracker()
        {
            ExperimentId = experiment.ExperimentId,
            Experiment = experiment
        };

        experimentTracker.Initialize();

        return experimentTracker;
    }

    /// <summary>
    ///     Initializes this tracker, setting the time left based on a random gaussian distribution applied to the mean time to happen.
    /// </summary>
    public void Initialize()
    {
        if (State == KuriosityExperimentState.Uninitialized) 
        {
            _timeLeft = Utility.RandomGaussianDistribution(Experiment.MeanTimeToHappen, Experiment.MeanTimeToHappen / STDEV_PROPORTION_OF_MEAN);
            CurrentKuriosityFactor = 1;
            State = KuriosityExperimentState.Initialized;
        }

        KuriositySciencePlugin.Logger.LogDebug($"Experiment Tracker initialized: {Experiment.ExperimentId}, TimeLeft: {Utility.ToDateTime(TimeLeft)}  at UT: {Utility.ToDateTime(GameManager.Instance.Game.UniverseModel.UniverseTime)}");
    }

    /// <summary>
    ///     Creates a new ID to be used when we want to register / complete a kerbal-specific experiment.
    /// </summary>
    /// <param name="kerbalId">The kerbal who we want to assign the experiment to</param>
    /// <returns>The new report ID</returns>
    private string CreateKuriosityReportID(string experimentId, Guid kerbalId)
    {
        return string.Format("{0}_{1}", experimentId, kerbalId.ToString());
    }

    /// <summary>
    ///     Checks the research location & situation of the vessel to see if it's valid for the experiment
    /// </summary>
    /// <param name="vessel">the vessel that we're checking against (the kerbal's vessel)</param>
    /// <returns>Returns true if the research location & situation is valid for this experiment</returns>
    private bool ValidExperimentResearchLocation(VesselComponent vessel)
    {
        ResearchLocation vesselResearchLocation = vessel.VesselScienceRegionSituation.ResearchLocation;

        return (Experiment.ExperimentDefinition.IsLocationValid(vesselResearchLocation, out bool regionRequired));
    }

    /// <summary>
    ///     Checks if this experiment is allowed to run based on the Part's allowed Kuriosity experiments
    /// </summary>
    /// <param name="dataKuriosityScience">Module data for the kerbal's current part</param>
    /// <returns>Returns true if the part allows this experiment to be run</returns>
    private bool ValidExperimentForPart(Data_KuriosityScience dataKuriosityScience)
    {
        foreach (KuriosityExperiment partExperiment in dataKuriosityScience.KuriosityExperiments)
        {
            if (partExperiment.ExperimentId == Experiment.ExperimentId) return true;
        }
        return false;
    }

    /// <summary>
    ///     Checks to see if this experiment has already been completed and submitted for this kerbal
    /// </summary>
    /// <param name="kerbalId">the kerbal we're checking against</param>
    /// <returns>Returns true if the experiment has not yet been completed & submitted for this kerbal</returns>
    private bool ValidThisExperimentNotCompleted(Guid kerbalId)
    {
        if (Experiment.IsRerunnable) return true;

        if(GetAllSubmittedReportsForExperiment(CreateKuriosityReportID(Experiment.ExperimentId, kerbalId)).Count > 0)
        {
            return false;
        } else
        {
            return true;
        }
    }

    /// <summary>
    ///     Returns all completed science reports for this experiment Id
    /// </summary>
    /// <param name="experimentId">the experiment Id to match</param>
    /// <returns>List of completed science reports</returns>
    private List<CompletedResearchReport> GetAllSubmittedReportsForExperiment(string experimentId)
    {
        //KuriositySciencePlugin.Logger.LogDebug($"GetAllSubmittedReportsForExperiment: {experimentId}");
        if (_sessionManager == null)
        {
            _sessionManager = GameManager.Instance.Game.SessionManager;
        }

        if(_sessionManager.TryGetMyAgencySubmittedResearchReports(out List<CompletedResearchReport> completedReports))
        {
            if (completedReports == null)
            {
                return new List<CompletedResearchReport>();
            }
            return completedReports.Where(r => (r.ExperimentID == experimentId || r.ExperimentID == experimentId.Replace("kuriosity_experiment_",""))).ToList(); // TODO = remove this hack after a few versions (added in 0.1.2)
        }

        return new List<CompletedResearchReport>();
    }

    /// <summary>
    ///     Checks if all this experiments conditions are valid
    /// </summary>
    /// <param name="vessel">the current vessel</param>
    /// <param name="dataKuriosityScience">the part's data module</param>
    /// <param name="kerbalId">this kerbal</param>
    /// <returns>True if all conditions are valid</returns>
    private bool IsExperimentValid(VesselComponent vessel, Data_KuriosityScience dataKuriosityScience, Guid kerbalId)
    {
        bool hasValidExperimentResearchLocation = ValidExperimentResearchLocation(vessel);
        bool hasValidExperimentForPart = ValidExperimentForPart(dataKuriosityScience);
        bool hasValidThisExperimentNotCompleted = ValidThisExperimentNotCompleted(kerbalId);
        bool hasValidAdditionalCrewRequirement = ValidAdditionalCrewRequirement(vessel);
        bool hasValidProbeCoreRequirement = ValidProbeCoreRequirement(vessel);
        bool hasValidTechRequirement = ValidTechRequirement();
        bool hasValidOtherExperimentCompleted = ValidOtherExperimentCompleted(kerbalId);
        bool hasValidCommNetState = ValidCommNetState(vessel);
        bool hasValidAllowHomePlanet = ValidAllowHomePlanet(vessel);
        bool hasValidRequiresEVA = ValidRequiresEVA(vessel);

        /*
        KuriositySciencePlugin.Logger.LogDebug($"Experiment: {Experiment.ExperimentId}\n" +
            $"  Location:                   {hasValidExperimentResearchLocation}\n" +
            $"  Part:                       {hasValidExperimentForPart}\n" +
            $"  Experiment not completed:   {hasValidThisExperimentNotCompleted}\n" +
            $"  AdditionalCrew:             {hasValidAdditionalCrewRequirement}\n" +
            $"  ProbeCore:                  {hasValidProbeCoreRequirement}\n" +
            $"  Tech:                       {hasValidTechRequirement}\n" +
            $"  Other Experiment completed: {hasValidOtherExperimentCompleted}\n" +
            $"  CommNet:                    {hasValidCommNetState}\n" +
            $"  Homeworld:                  {hasValidAllowHomePlanet}\n" +
            $"  EVA:                        {hasValidRequiresEVA}\n");
        */

        return (hasValidExperimentResearchLocation
            && hasValidExperimentForPart
            && hasValidThisExperimentNotCompleted
            && hasValidAdditionalCrewRequirement
            && hasValidProbeCoreRequirement
            && hasValidTechRequirement
            && hasValidOtherExperimentCompleted
            && hasValidCommNetState
            && hasValidAllowHomePlanet
            && hasValidRequiresEVA
            );
    }

    /// <summary>
    ///     Updates the precedence and validity for the tracked experiment
    /// </summary>
    /// <param name="vessel">the current vessel</param>
    /// <param name="dataKuriosityScience">the part's data module</param>
    /// <param name="kerbalId">this kerbal</param>
    public void UpdateExperimentPrecedence(VesselComponent vessel, Data_KuriosityScience dataKuriosityScience, Guid kerbalId)
    {
        if(ExperimentPrecedence != KuriosityExperimentPrecedence.DePrioritized)
        {
            if (!(State == KuriosityExperimentState.Completed
            || State == KuriosityExperimentState.Uninitialized)
            && IsExperimentValid(vessel, dataKuriosityScience, kerbalId))
            {
                if (dataKuriosityScience.PartPriorityExperiments.Contains(Experiment.ExperimentId))
                {
                    ExperimentPrecedence = KuriosityExperimentPrecedence.Priority;
                }
                else
                {
                    ExperimentPrecedence = KuriosityExperimentPrecedence.NonPriority;
                }
            }
            else
            {
                ExperimentPrecedence = KuriosityExperimentPrecedence.None;
            }
        }
    }

    /// <summary>
    ///     Completes the tracked experiment and registers a kerbal-specific copy of it with the best science
    ///     storage component on the vessel (which is attached to any ScienceExperiment module).
    ///     
    ///     Also requests a notification to the user that the experiment has completed.
    /// </summary>
    /// <param name="kerbal">the kerbal that has completed the experiment</param>
    /// <param name="vessel">the kerbal's current vessel</param>
    public void TriggerExperiment(KerbalInfo kerbal, VesselComponent vessel)
    { 
        PartComponentModule_ScienceExperiment _moduleScienceExperiment = GetVessel_PartComponentModule_ScienceExperiment(vessel);

        if (_scienceManager == null)
        {
            _scienceManager = GameManager.Instance.Game.ScienceManager;
            KuriositySciencePlugin.Logger.LogDebug($"Had to reassign ScienceManager");
        }

        ExperimentDefinition experimentDefinition = Experiment.ExperimentDefinition;

        if (experimentDefinition == null)
        {
            KuriositySciencePlugin.Logger.LogError($"Experiment {Experiment.ExperimentId} ID not found. Cannot activate KuriosityExperiment class");
            return;
        }

        //create ResearchReport(s) for data result & store
        double multiplier = BASE_SCIENCE_MULTIPLIER;
        if(Experiment.ApplyScienceMultiplier) multiplier *= (Math.Pow(Utility.CurrentScienceMultiplier(vessel),SCIENCE_MULTIPLIER_SCALING_FACTOR));
        Guid kerbalId = kerbal.Id;

        string newExperimentId = CreateKuriosityReportID(Experiment.ExperimentId, kerbalId);

        // Suffix the Id with how many reports we've already run, if experiment is rerunnable
        if (Experiment.IsRerunnable)
        {
            newExperimentId = string.Format("{0}_{1}", newExperimentId, NumberOfCompletedResearchReports(newExperimentId));
        }

        ExperimentDefinition newExperimentDefinition = new ExperimentDefinition()
        {
            ExperimentID = newExperimentId,
            ExperimentType = experimentDefinition.ExperimentType,
            RequiresEVA = experimentDefinition.RequiresEVA,
            DataReportDisplayName = experimentDefinition.DataReportDisplayName,
            DataFlavorDescriptions = experimentDefinition.DataFlavorDescriptions,
            DataValue = (float)multiplier * experimentDefinition.DataValue,
            DisplayName = experimentDefinition.DisplayName,
            DisplayRequirements = experimentDefinition.DisplayRequirements,
            SampleReportDisplayName = experimentDefinition.SampleReportDisplayName,
            SampleFlavorDescriptions = experimentDefinition.SampleFlavorDescriptions,
            SampleValue = (float)multiplier * experimentDefinition.SampleValue,
            ValidLocations = experimentDefinition.ValidLocations,
            TransmissionSize = experimentDefinition.TransmissionSize
        };

        _scienceManager.ScienceExperimentsDataStore.AddExperimentDefinition(newExperimentDefinition);
        
        if (newExperimentDefinition.ExperimentType == ScienceExperimentType.DataType || newExperimentDefinition.ExperimentType == ScienceExperimentType.Both)
        {
            string flavorText = _scienceManager.ScienceExperimentsDataStore.GetFlavorText(newExperimentDefinition.ExperimentID, _moduleScienceExperiment._currentLocation.ResearchLocationId, ScienceReportType.DataType);
            ResearchReport researchReport = new ResearchReport(newExperimentDefinition.ExperimentID, newExperimentDefinition.DataReportDisplayName, _moduleScienceExperiment._currentLocation, ScienceReportType.DataType, newExperimentDefinition.DataValue, flavorText);
            _moduleScienceExperiment._storageComponent.StoreResearchReport(researchReport);
        }

        if (newExperimentDefinition.ExperimentType == ScienceExperimentType.SampleType || newExperimentDefinition.ExperimentType == ScienceExperimentType.Both)
        {
            //as of 0.2.1 GetFlavorText is currently bugged for returning 'default' Sample flavor descriptions, so need to add a default Data Flavor description for those experiments
            string flavorText2 = _scienceManager.ScienceExperimentsDataStore.GetFlavorText(newExperimentDefinition.ExperimentID, _moduleScienceExperiment._currentLocation.ResearchLocationId, ScienceReportType.SampleType);
            ResearchReport researchReport2 = new ResearchReport(newExperimentDefinition.ExperimentID, newExperimentDefinition.SampleReportDisplayName, _moduleScienceExperiment._currentLocation, ScienceReportType.SampleType, newExperimentDefinition.SampleValue, flavorText2);
            _moduleScienceExperiment._storageComponent.StoreResearchReport(researchReport2);
        }

        if(_messageCenter == null)
        {
            _messageCenter = GameManager.Instance.Game.Messages;
        }

        ResearchReportAcquiredMessage message;
        if (_messageCenter.TryCreateMessage(out message))
        {
            _messageCenter.Publish(message);
        }

        NotifyExperimentTriggered(kerbal.Attributes.GetFullName(), vessel);
    }

    /// <summary>
    ///     Creates the notification to the user that the experiment has completed
    /// </summary>
    /// <param name="kerbalName">this kerbal's full name</param>
    /// <param name="vessel">the kerbal's vessel</param>
    private void NotifyExperimentTriggered(string kerbalName, VesselComponent vessel)
    {
        if(_notificationManager == null)
        {
            _notificationManager = GameManager.Instance.Game.Notifications;
        }

        _notificationManager.ProcessNotification(new NotificationData
        {
            Tier = NotificationTier.Alert,
            Importance = NotificationImportance.High,
            AlertTitle = new NotificationLineItemData
            {
                ObjectParams = [kerbalName, vessel.Name],
                LocKey = "KuriosityScience/Notifications/ExperimentTriggered"
            },
            TimeStamp = GameManager.Instance.Game.UniverseModel.UniverseTime,
            FirstLine = new NotificationLineItemData
            {
                LocKey = Experiment.ExperimentDefinition.DisplayName
            }
        });
    }

    /// <summary>
    ///     Finds any ScienceExperiment module on the vessel
    /// </summary>
    /// <param name="vessel">the vessel to search</param>
    /// <returns>the found Part Component ScienceExperiment module</returns>
    private PartComponentModule_ScienceExperiment GetVessel_PartComponentModule_ScienceExperiment(VesselComponent vessel)
    {
        var parts = vessel.SimulationObject.PartOwner.Parts;
        foreach (var part in parts)
        {
            if (part.TryGetModule(typeof(PartComponentModule_ScienceExperiment), out var m))
            {
                return m as PartComponentModule_ScienceExperiment;
            }
        }

        KuriositySciencePlugin.Logger.LogError($"Unable to find a PartComponentModule_ScienceExperiment in the vessel: {vessel.Name}");

        return null;
    }

    /// <summary>
    ///     Finds and returns all Data_Command modules attached ta parts of this vessel
    /// </summary>
    /// <param name="vessel">The vessel to search</param>
    /// <returns>List of Data_Command classes</returns>
    private List<Data_Command> GetAll_Data_Command(VesselComponent vessel)
    {
        //TODO - Make this generic and add to my Utility class
        List<Data_Command> returnDataModules = new();

        var parts = vessel.SimulationObject.PartOwner.Parts;
        foreach (var part in parts)
        {
            if (part.TryGetModuleData<PartComponentModule_Command,Data_Command>(out var m))
            {
                returnDataModules.Add(m as Data_Command);
            }
        }

        return returnDataModules;
    }

    /// <summary>
    ///     Does this vessel fulfill any Additional Crew requirements of the experiment?
    /// </summary>
    /// <param name="vessel"></param>
    /// <returns></returns>
    private bool ValidAdditionalCrewRequirement(VesselComponent vessel)
    {
        if(_rosterManager == null) _rosterManager = GameManager.Instance.Game.SessionManager.KerbalRosterManager;

        int additionalKerbalCount = _rosterManager.GetAllKerbalsInVessel(vessel.GlobalId).Count - 1;

        if (additionalKerbalCount >= Experiment.MinimumVesselAdditionalCrew
            && (Experiment.MaximumVesselAdditionalCrew == -1
            || additionalKerbalCount <= Experiment.MaximumVesselAdditionalCrew ))
        {
            return true;
        } else
        {
            return false;
        }
    }

    /// <summary>
    ///     Does this vessel fulfill any probe core requirements of this experiment?
    /// </summary>
    /// <param name="vessel"></param>
    /// <returns></returns>
    private bool ValidProbeCoreRequirement(VesselComponent vessel)
    {
        if(Experiment.RequiresProbeCore)
        {
            foreach(Data_Command dataCommand in GetAll_Data_Command(vessel))
            {
                if (dataCommand.minimumCrew == 0) return true;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    ///     Have any required techs for this experiment been unlocked?
    /// </summary>
    /// <returns></returns>
    private bool ValidTechRequirement()
    {
        if (Experiment.TechRequired != null && Experiment.TechRequired.Count > 0)
        {
            if (_scienceManager == null) _scienceManager = GameManager.Instance.Game.ScienceManager;

            foreach (string techToCheck in Experiment.TechRequired)
            {
                if (!_scienceManager.IsNodeUnlocked(techToCheck)) return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Have any required experiments needed to run this experiment been completed by this kerbal?
    /// </summary>
    /// <param name="kerbalId"></param>
    /// <returns></returns>
    private bool ValidOtherExperimentCompleted(Guid kerbalId)
    {
        if (Experiment.KuriosityExperimentRequired != null && Experiment.KuriosityExperimentRequired.Count > 0)
        {
            foreach (string kuriosityExperimentToCheck in Experiment.KuriosityExperimentRequired)
            {
                if (GetAllSubmittedReportsForExperiment(CreateKuriosityReportID(kuriosityExperimentToCheck, kerbalId)).Count == 0) return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Does this vessel have the required CommNet state for this experiment?
    /// </summary>
    /// <param name="vessel"></param>
    /// <returns></returns>
    private bool ValidCommNetState(VesselComponent vessel)
    {
        if (Experiment.CommNetStateRequired != CommNetState.Any)
        {
            TelemetryComponent telemetry = vessel.SimulationObject.Telemetry;

            if (telemetry != null)
            {
                if (Experiment.CommNetStateRequired == CommNetState.Connected && telemetry.CommNetConnectionStatus == ConnectionNodeStatus.Connected)
                {
                    return true;
                }

                if (Experiment.CommNetStateRequired == CommNetState.Disconnected && telemetry.CommNetConnectionStatus != ConnectionNodeStatus.Connected)
                {
                    return true;
                }
            }
            else
            {
                return true; 
            }
        }
        
        return true;
    }

    /// <summary>
    ///     Does this vessel's SOI fulfill any homeworld requirements of the experiment?
    /// </summary>
    /// <param name="vessel"></param>
    /// <returns></returns>
    private bool ValidAllowHomePlanet(VesselComponent vessel)
    {
        if (!Experiment.AllowHomePlanet)
        {
            CelestialBodyComponent body = vessel.SimulationObject.CelestialBody;

            if (body == null || body.isHomeWorld)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        return true;
    }

    /// <summary>
    ///     Does this vessel (or EVA kerbal) fulfill the EVA requirements of the experiment?
    /// </summary>
    /// <param name="vessel"></param>
    /// <returns></returns>
    private bool ValidRequiresEVA(VesselComponent vessel)
    {
        if(Experiment.ExperimentDefinition.RequiresEVA)
        {
            return vessel.SimulationObject.IsKerbal;
        }

        return true;
    }

    /// <summary>
    ///     Counts any completed research report that starts with the passed experiment Id.
    /// </summary>
    /// <param name="experimentId"></param>
    /// <returns>The number of matching experiments</returns>
    private int NumberOfCompletedResearchReports(string experimentId)
    {
        GameManager.Instance.Game.AgencyManager.TryGetMyAgencyEntry(out AgencyEntry agencyEntry);
        if (agencyEntry != null && agencyEntry.SubmittedResearchReports != null)
        {
            return agencyEntry.SubmittedResearchReports.Where(r => r.ExperimentID.StartsWith(experimentId)).Count();
        }

        return 0;
    }
}

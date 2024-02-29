using KSP.Game;
using KSP.Game.Science;
using Newtonsoft.Json;
using UnityEngine;

namespace KuriosityScience.Models;

/// <summary>
///     Object that holds the Kuriosity Science specific information about the experiment, as well as a reference to the KSP2 ExperimentDefintion.
///     
/// These data for these objects is initially created by Patch Manager (see kuriosity_experiments.patch) and then populated by the KuriositySciencePlugin class
/// </summary>
[Serializable]
public class KuriosityExperiment
{
    [SerializeField]
    public string ExperimentId;

    [JsonIgnore]
    public ExperimentDefinition ExperimentDefinition { get { return GameManager.Instance.Game.ScienceManager.ScienceExperimentsDataStore.GetExperimentDefinition(ExperimentId); } }
    
    [SerializeField]
    public double MeanTimeToHappen = 20000000;

    [SerializeField]
    public int MinimumVesselAdditionalCrew = 0;

    [SerializeField]
    public int MaximumVesselAdditionalCrew = -1;

    [SerializeField]
    public bool RequiresProbeCore = false;

    [SerializeField]
    public List<string> TechRequired = new();

    [SerializeField]
    public List<string> KuriosityExperimentRequired = new();

    [SerializeField]
    public CommNetState CommNetStateRequired = CommNetState.Any;

    [SerializeField]
    public bool AllowHomePlanet = true;

    [SerializeField]
    public bool IsRerunnable = false;

    [SerializeField]
    public bool ApplyScienceMultiplier = false;

    [SerializeField]
    public string ConditionsDescription = "";
}   

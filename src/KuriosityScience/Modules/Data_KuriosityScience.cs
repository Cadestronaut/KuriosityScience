using KSP.Sim;
using KSP.Sim.Definitions;
using KuriosityScience.Models;
using UnityEngine;

namespace KuriosityScience.Modules;

[Serializable]
public class Data_KuriosityScience : ModuleData
{
    public override Type ModuleType => typeof(Module_KuriosityScience);

    [KSPDefinition]
    [Tooltip("Adjustment this part applies to the Kuriosity Factor")]
    public double PartKuriosityFactorAdjustment = 1.0;

    [KSPDefinition]
    public List<KuriosityExperiment> KuriosityExperiments = new();

    [KSPDefinition]
    [Tooltip("Allowed Kuriosity Experiments")]
    public List<string> AllowedKuriosityExperiments = new();

    [KSPDefinition]
    [Tooltip("Priority Kuriosity Experiments")]
    public List<string> PartPriorityExperiments = new();

    [KSPState]
    [Tooltip("Kuriosity Controllers for the kerbals")]
    public Dictionary<Guid, KuriosityController> KuriosityControllers = new();

    public ModuleProperty<double> BaseKuriosityFactor = new(1.0, true, val => $"{val:P0}");

    public double GetKuriosityFactor()
    {
        return BaseKuriosityFactor.GetValue();
    }

    /// <summary>
    /// Add OAB module description on all eligible parts
    /// </summary>
    public override List<OABPartData.PartInfoModuleEntry> GetPartInfoEntries(Type partBehaviourModuleType,
        List<OABPartData.PartInfoModuleEntry> delegateList)
    {
        if (partBehaviourModuleType == ModuleType && PartKuriosityFactorAdjustment != 1.0)
        {
            // module description
            delegateList.Add(new OABPartData.PartInfoModuleEntry("", (_) => LocalizationStrings.OAB_DESCRIPTION["ModuleDescription"]));
            OABPartData.PartInfoModuleEntry entry = new OABPartData.PartInfoModuleEntry(LocalizationStrings.OAB_DESCRIPTION["PartSettings"],
                _ =>
                {
                    var subEntries = new List<OABPartData.PartInfoModuleSubEntry>();

                    subEntries.Add(new OABPartData.PartInfoModuleSubEntry(
                        LocalizationStrings.OAB_DESCRIPTION["KuriosityFactorAdjustment"],
                        $" {(PartKuriosityFactorAdjustment).ToString("P")}"));

                    return subEntries;
                });
            delegateList.Add(entry);
        }
        return delegateList;
    }
}

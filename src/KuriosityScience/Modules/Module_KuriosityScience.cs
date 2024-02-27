using KSP.Sim.Definitions;
using KuriosityScience.Models;
using UnityEngine;

namespace KuriosityScience.Modules;

[DisallowMultipleComponent]
public class Module_KuriosityScience : PartBehaviourModule
{
    public override Type PartComponentModuleType => typeof(PartComponentModule_KuriosityScience);

    [SerializeField]
    protected Data_KuriosityScience _dataKuriosityScience;

    public override void AddDataModules()
    {
        base.AddDataModules();
        _dataKuriosityScience ??= new Data_KuriosityScience();
        DataModules.TryAddUnique(_dataKuriosityScience, out _dataKuriosityScience);
    }

    public override void OnInitialize()
    {
        base.OnInitialize();
        if (PartBackingMode == PartBackingModes.Flight)
        {
            moduleIsEnabled = true;
        }
    }

    public override string GetModuleDisplayName()
    {
        return LocalizationStrings.OAB_DESCRIPTION["ModuleName"];
    }
}

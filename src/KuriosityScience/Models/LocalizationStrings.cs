using I2.Loc;

namespace KuriosityScience.Models;

public static class LocalizationStrings
{
    public static readonly Dictionary<string, LocalizedString> OAB_DESCRIPTION = new()
    {
        { "ModuleName", "KuriosityScience/ModuleName" },
        { "ModuleDescription", "KuriosityScience/OAB/Description" },
        { "PartSettings", "KuriosityScience/OAB/PartSettings" },
        { "KuriosityFactorAdjustment", "KuriosityScience/OAB/KuriosityFactorAdjustment" }
    };
}

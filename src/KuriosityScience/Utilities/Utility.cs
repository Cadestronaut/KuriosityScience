using KSP.Game;
using KSP.Sim.impl;

namespace KuriosityScience.Utilities;

internal static class Utility
{
    private const long SecondsInMinute = 60;
    private const long MinutesInHour = 60;
    private const long HoursInDay = 6;
    private const long DaysInYear = 426;

    //1 year is 9201600.0 seconds

    public static List<CelestialBodyComponent> GetAllCelestialBodies() => GameManager.Instance.Game?.UniverseModel?.GetAllCelestialBodies();

    public static List<string> GetAllCelestialBodyNames() => GetAllCelestialBodies().Select(body => body.Name).ToList();

    /// <summary>
    ///     Converts a time in seconds to a KSP formatted datetime string
    /// </summary>
    /// <param name="time">Duration in seconds</param>
    /// <returns>Formatted KSP datetime string</returns>
    public static string ToDateTime(double time)
    {
        var num = (long)Math.Truncate(time);

        // Seconds
        var seconds = (int)(num % SecondsInMinute);
        num -= seconds;
        num /= SecondsInMinute;

        // Minutes
        var minutes = (int)(num % MinutesInHour);
        num -= minutes;
        num /= MinutesInHour;

        // Hours
        var hours = (int)(num % HoursInDay);
        num -= hours;
        num /= HoursInDay;

        // Days
        var days = (int)(num % DaysInYear);
        num -= days;
        num /= DaysInYear;

        // Years
        var years = (int)num;

        var totalSeconds = (long)Math.Truncate(time);

        var res = totalSeconds switch
        {
            < MinutesInHour * SecondsInMinute => $"{minutes:d2}m{seconds:d2}s",
            < HoursInDay * MinutesInHour * SecondsInMinute => $" {hours}h{minutes:d2}m",
            < 100 * HoursInDay * MinutesInHour * SecondsInMinute => $"{days}d {hours}h",
            < DaysInYear * HoursInDay * MinutesInHour * SecondsInMinute => $"{days}d {hours}h",
            _ => $"{years}y {days}d"
        };

        return res;
    }

    /// <summary>
    ///     creates a gaussian (normal) distribution to apply to a random number that is created 
    /// </summary>
    /// <param name="mean">the mean of the resultant random numbers</param>
    /// <param name="stdDev">the standard deviation of the resultant random numbers</param>
    /// <returns>a random number from within the gaussian distribution</returns>
    public static double RandomGaussianDistribution(double mean, double stdDev)
    {
        Random rnd = new Random();
        double u1 = 1.0 - rnd.NextDouble();
        double u2 = 1.0 - rnd.NextDouble();
        double rndStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        double rndNormal = mean + stdDev * rndStdNormal;

        return rndNormal;
    }

    /// <summary>
    ///     works out and returns the current science multiplier based on the various scalars for celestial body, situation and region.
    /// </summary>
    /// <returns>the current science multiplier</returns>
    public static double CurrentScienceMultiplier(VesselComponent vessel)
    {
        double multiplier = vessel.VesselScienceRegionSituation.CelestialBodyScalar;
        multiplier *= vessel.VesselScienceRegionSituation.SituationScalar;
        multiplier *= vessel.VesselScienceRegionSituation.ScienceRegionScalar;

        return multiplier;
    }
}

namespace KuriosityScience.Models;

public enum KuriosityExperimentState
{
    Uninitialized,
    Initialized,
    Running,
    Paused,
    Completed
}

public enum KuriosityExperimentPrecedence
{
    Priority,
    NonPriority,
    DePrioritized,
    None
}

public enum CommNetState
{
    Connected,
    Disconnected,
    Any
}
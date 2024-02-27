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
    None
}

public enum CommNetState
{
    Connected,
    Disconnected,
    Any
}
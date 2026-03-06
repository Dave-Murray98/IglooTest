/// <summary>
/// Defines whether a sound originates from inside or outside the submarine.
/// Interior sounds (engine hum, crew voices, UI) play directly and cleanly.
/// Exterior sounds (ambience, water, creature noises) are routed through the
/// Exterior mixer group, which applies hull-muffling filters to simulate the
/// sound passing through the submarine's hull before reaching the crew.
/// </summary>
public enum AudioLayer
{
    /// <summary>
    /// Sound originates from inside the submarine.
    /// Plays through the Interior mixer group — no hull filtering applied.
    /// Use for: engine sounds, crew dialogue, UI clicks, interior SFX.
    /// </summary>
    Interior,

    /// <summary>
    /// Sound originates from outside the submarine.
    /// Plays through the Exterior mixer group — hull low-pass and reverb applied.
    /// Use for: ambient ocean sounds, creature noises, water impacts, breach/submerge SFX.
    /// </summary>
    Exterior
}
/// <summary>
/// Categories for organizing and managing different types of audio in the game.
/// Each category has its own object pool and volume control.
/// </summary>
public enum AudioCategory
{
    /// <summary>
    /// Background environmental sounds (underwater ambience, wind, etc.)
    /// </summary>
    Ambience,

    /// <summary>
    /// Player-related sound effects (footsteps, swimming, breathing, etc.)
    /// </summary>
    PlayerSFX,

    /// <summary>
    /// Enemy and creature sound effects (growls, attacks, movements)
    /// </summary>
    EnemySFX,

    /// <summary>
    /// Character dialogue and voice lines
    /// </summary>
    Dialogue,

    /// <summary>
    /// User interface sounds (button clicks, menu navigation)
    /// </summary>
    UI,

    /// <summary>
    /// Background music and musical stingers
    /// </summary>
    Music
}
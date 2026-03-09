/// <summary>
/// Defines what roles a single player slot has in a given configuration.
/// A player can hold multiple roles simultaneously (e.g. Pilot + Engineer + Gunner).
/// </summary>
[System.Flags]
public enum PlayerRole
{
    None = 0,
    Pilot = 1 << 0,   // Controls submarine movement
    Engineer = 1 << 1,   // Repairs hull regions
    Gunner = 1 << 2,   // Controls a turret
}

/// <summary>
/// Describes the role(s) assigned to one player slot within a configuration.
/// </summary>
[System.Serializable]
public class PlayerRoleAssignment
{
    /// <summary>The slot index this assignment applies to (0 = first player to join).</summary>
    public int SlotIndex;

    /// <summary>
    /// The combined roles this player holds.
    /// Use the | operator to combine, e.g. PlayerRole.Pilot | PlayerRole.Engineer.
    /// </summary>
    public PlayerRole Roles;

    /// <summary>
    /// The turret name this player controls, if they have the Gunner role.
    /// Must match a name in TurretManager's turret list. Empty if not a gunner.
    /// </summary>
    public string AssignedTurretName;

    public PlayerRoleAssignment(int slotIndex, PlayerRole roles, string turretName = "")
    {
        SlotIndex = slotIndex;
        Roles = roles;
        AssignedTurretName = turretName;
    }

    /// <summary>Convenience check — does this assignment include a specific role?</summary>
    public bool HasRole(PlayerRole role) => (Roles & role) != 0;
}

/// <summary>
/// Describes the complete game setup for a given number of connected players.
/// Defines each player's roles and which turrets should be active.
///
/// All configurations are defined as static readonly fields at the bottom of this class —
/// one entry per possible player count (1–6).
/// </summary>
public class RoleConfiguration
{
    /// <summary>How many players this configuration is designed for.</summary>
    public readonly int PlayerCount;

    /// <summary>One entry per player slot, describing their role(s).</summary>
    public readonly PlayerRoleAssignment[] Assignments;

    /// <summary>
    /// Names of turrets that should be active in this configuration.
    /// All other turrets will be deactivated. Must match names in TurretManager.
    /// </summary>
    public readonly string[] ActiveTurretNames;

    public RoleConfiguration(int playerCount, PlayerRoleAssignment[] assignments, string[] activeTurretNames)
    {
        PlayerCount = playerCount;
        Assignments = assignments;
        ActiveTurretNames = activeTurretNames;
    }

    /// <summary>
    /// Returns the assignment for a given slot index, or null if none exists.
    /// </summary>
    public PlayerRoleAssignment GetAssignment(int slotIndex)
    {
        foreach (var assignment in Assignments)
        {
            if (assignment.SlotIndex == slotIndex)
                return assignment;
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // All game configurations — one per player count.
    // To change a setup, edit the relevant entry below.
    // -------------------------------------------------------------------------

    /// <summary>
    /// 1 Player: Solo player handles everything.
    /// Pilot + Engineer + Front gunner.
    /// </summary>
    public static readonly RoleConfiguration OnePlayer = new RoleConfiguration(
        playerCount: 1,
        assignments: new[]
        {
            new PlayerRoleAssignment(0, PlayerRole.Pilot | PlayerRole.Engineer | PlayerRole.Gunner, "FrontTurret"),
        },
        activeTurretNames: new[] { "FrontTurret" }
    );

    /// <summary>
    /// 2 Players: Pilot takes front gun, Engineer takes rear gun.
    /// </summary>
    public static readonly RoleConfiguration TwoPlayers = new RoleConfiguration(
        playerCount: 2,
        assignments: new[]
        {
            new PlayerRoleAssignment(0, PlayerRole.Pilot | PlayerRole.Gunner, "FrontTurret"),
            new PlayerRoleAssignment(1, PlayerRole.Engineer | PlayerRole.Gunner, "RearTurret"),
        },
        activeTurretNames: new[] { "FrontTurret", "RearTurret" }
    );

    /// <summary>
    /// 3 Players: Pilot handles navigation and repairs, two dedicated side gunners.
    /// Front and rear turrets are replaced by left and right.
    /// </summary>
    public static readonly RoleConfiguration ThreePlayers = new RoleConfiguration(
        playerCount: 3,
        assignments: new[]
        {
            new PlayerRoleAssignment(0, PlayerRole.Pilot | PlayerRole.Engineer),
            new PlayerRoleAssignment(1, PlayerRole.Gunner, "LeftTurret"),
            new PlayerRoleAssignment(2, PlayerRole.Gunner, "RightTurret"),
        },
        activeTurretNames: new[] { "LeftTurret", "RightTurret" }
    );

    /// <summary>
    /// 4 Players: Pilot+Engineer, two side gunners, one rear gunner.
    /// </summary>
    public static readonly RoleConfiguration FourPlayers = new RoleConfiguration(
        playerCount: 4,
        assignments: new[]
        {
            new PlayerRoleAssignment(0, PlayerRole.Pilot | PlayerRole.Engineer),
            new PlayerRoleAssignment(1, PlayerRole.Gunner, "LeftTurret"),
            new PlayerRoleAssignment(2, PlayerRole.Gunner, "RightTurret"),
            new PlayerRoleAssignment(3, PlayerRole.Gunner, "RearTurret"),
        },
        activeTurretNames: new[] { "LeftTurret", "RightTurret", "RearTurret" }
    );

    /// <summary>
    /// 5 Players: Pilot is now solo, dedicated engineer joins, three gunners remain.
    /// </summary>
    public static readonly RoleConfiguration FivePlayers = new RoleConfiguration(
        playerCount: 5,
        assignments: new[]
        {
            new PlayerRoleAssignment(0, PlayerRole.Pilot),
            new PlayerRoleAssignment(1, PlayerRole.Gunner, "LeftTurret"),
            new PlayerRoleAssignment(2, PlayerRole.Gunner, "RightTurret"),
            new PlayerRoleAssignment(3, PlayerRole.Gunner, "RearTurret"),
            new PlayerRoleAssignment(4, PlayerRole.Engineer),
        },
        activeTurretNames: new[] { "LeftTurret", "RightTurret", "RearTurret" }
    );

    /// <summary>
    /// 6 Players: Full crew. Four corner turrets replace the 3-turret layout.
    /// Pilot and Engineer unchanged; four gunners each take a corner.
    /// </summary>
    public static readonly RoleConfiguration SixPlayers = new RoleConfiguration(
        playerCount: 6,
        assignments: new[]
        {
            new PlayerRoleAssignment(0, PlayerRole.Pilot),
            new PlayerRoleAssignment(1, PlayerRole.Gunner, "FrontLeftTurret"),
            new PlayerRoleAssignment(2, PlayerRole.Gunner, "FrontRightTurret"),
            new PlayerRoleAssignment(3, PlayerRole.Gunner, "RearLeftTurret"),
            new PlayerRoleAssignment(4, PlayerRole.Engineer),
            new PlayerRoleAssignment(5, PlayerRole.Gunner, "RearRightTurret"),
        },
        activeTurretNames: new[] { "FrontLeftTurret", "FrontRightTurret", "RearLeftTurret", "RearRightTurret" }
    );

    /// <summary>
    /// Lookup table — index directly by player count (index 0 is unused).
    /// e.g. AllConfigurations[3] gives you the 3-player configuration.
    /// </summary>
    public static readonly RoleConfiguration[] AllConfigurations =
    {
        null,           // [0] unused
        OnePlayer,      // [1]
        TwoPlayers,     // [2]
        ThreePlayers,   // [3]
        FourPlayers,    // [4]
        FivePlayers,    // [5]
        SixPlayers,     // [6]
    };

    /// <summary>
    /// Returns the correct configuration for a given player count.
    /// Clamps to valid range (1–6) so callers don't need to guard against edge cases.
    /// </summary>
    public static RoleConfiguration GetForPlayerCount(int count)
    {
        int clamped = UnityEngine.Mathf.Clamp(count, 1, 6);
        return AllConfigurations[clamped];
    }
}
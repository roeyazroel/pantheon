namespace Pantheon.Domain.Politics;

/// <summary>The treaties two gods may sign besides an alliance (spec §38, ADR-025).</summary>
public enum TreatyKind
{
    /// <summary>Neither may declare war on the other.</summary>
    NonAggression,

    /// <summary>The first god joins the second's wars against an aggressor (one named god, or any).</summary>
    DefensivePact,

    /// <summary>Both make war on a named god, and neither makes a separate peace with it.</summary>
    JointWar,

    /// <summary>Each casts in the other's influence as an ally would.</summary>
    SharedInfluence,

    /// <summary>Each may preach where the other leads: its missionaries reach those villages, and they are open to it.</summary>
    ReligiousAccess,

    /// <summary>Their Creatures never fight each other.</summary>
    CreatureNonAggression,

    /// <summary>Each answers disasters among the other's people.</summary>
    DisasterAssistance,

    /// <summary>Each joins the other's wars against any aggressor.</summary>
    MutualDefense,

    /// <summary>Neither casts miracles in the villages the other leads.</summary>
    TerritorialAgreement,

    /// <summary>One pays the other Divine Power every minute.</summary>
    Tribute,

    /// <summary>Each welcomes the other's migrants, whatever its refugee policy.</summary>
    MigrationAgreement,
}

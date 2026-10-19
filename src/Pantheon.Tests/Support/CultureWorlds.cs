using Pantheon.Domain.Culture;

namespace Pantheon.Tests.Support;

/// <summary>Pinned culture rules for tests (ADR-024).</summary>
internal static class CultureWorlds
{
    /// <summary>Culture that changes nothing: tests of earlier milestones keep their numbers.</summary>
    public static CultureRules Inert { get; } = new()
    {
        ResentmentFromFear = 0f, ResentmentFromLove = 0f, ResentmentFromHarm = 0f, DependencyFromHelp = 0f, DependencyFade = 0f, DependencyExpectation = 0f,
        DependencyWorship = 0f, SacrificeWorship = 0f, SecularPowerLoss = 0f, SecularResistance = 0f, MiracleResistance = 0f, SecularScience = 0f,
        Agnostic = 0.3f, Atheist = 0.55f, AntiDivine = 0.8f, AntiDivineApostasy = 0f, SecularSpreadPerSecond = 0f,
        Secular = new SecularWeights { Doubt = 0f, Godless = 0f, Schism = 0f, Resentment = 0f, Science = 0f, Faith = 0f },
        SecularDriftPerSecond = 0f, DoctrineDriftPerSecond = 0f, Embraced = 0.5f, DoctrineFromMiracle = 0f, DoctrineEffect = 0f, CharityPerSecond = 0f,
        MissionPerSecond = 0f, MissionRange = 1, RebellionResentment = 1f, RebellionShare = 0f, RebellionVent = 0f, ConductFadePerSecond = 0f,
        TraitThreshold = 1000000f, Contradiction = 0f,
    };

    /// <summary>The culture shipped with Milestone 15, part 4.</summary>
    public static CultureRules Shipped => TestContent.Shipped.DivineRules.Culture;
}

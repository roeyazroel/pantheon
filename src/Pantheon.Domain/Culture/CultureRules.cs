namespace Pantheon.Domain.Culture;

/// <summary>
/// Data-driven tuning of belief's deeper feelings, doctrines, secularism, missionaries, rebellion
/// and gods' conduct (spec §7, §8, §11, §32–35, ADR-024), loaded with <c>divine_rules.json</c>.
/// Rates are per belief step unless named per second; values without units are 0..1.
/// </summary>
public sealed record CultureRules
{
    /// <summary>Resentment fear breeds per step, times fear and the room left.</summary>
    public required float ResentmentFromFear { get; init; }

    /// <summary>Share of resentment love dissolves per step, times love.</summary>
    public required float ResentmentFromLove { get; init; }

    /// <summary>Resentment a harmful miracle adds among the god's own followers who witness it.</summary>
    public required float ResentmentFromHarm { get; init; }

    /// <summary>Dependency a miracle that answers a need adds, times the need.</summary>
    public required float DependencyFromHelp { get; init; }

    /// <summary>Share of dependency that fades per step.</summary>
    public required float DependencyFade { get; init; }

    /// <summary>Extra doubt per step in hardship, times dependency: the dependent expect protection (§8).</summary>
    public required float DependencyExpectation { get; init; }

    /// <summary>Extra share of worship per unit of dependency.</summary>
    public required float DependencyWorship { get; init; }

    /// <summary>Extra share of worship per unit of sacrifice, times fear.</summary>
    public required float SacrificeWorship { get; init; }

    /// <summary>Share of a settlement's worship lost per unit of secularism (§35: little Divine Power).</summary>
    public required float SecularPowerLoss { get; init; }

    /// <summary>Share of the gods' pull lost per unit of secularism: resistance to conversion.</summary>
    public required float SecularResistance { get; init; }

    /// <summary>Share of a miracle's effect on belief lost per unit of secularism: resistance to miracles.</summary>
    public required float MiracleResistance { get; init; }

    /// <summary>Extra share of learning and science per unit of secularism (§35: science bonuses).</summary>
    public required float SecularScience { get; init; }

    /// <summary>Secularism at which a settlement is agnostic.</summary>
    public required float Agnostic { get; init; }

    /// <summary>Secularism at which it is atheist.</summary>
    public required float Atheist { get; init; }

    /// <summary>Secularism at which it is anti-divine.</summary>
    public required float AntiDivine { get; init; }

    /// <summary>Share of every god's followers an anti-divine settlement turns away per step.</summary>
    public required float AntiDivineApostasy { get; init; }

    /// <summary>Secularism per second an anti-divine settlement spreads to each neighbour, times its own.</summary>
    public required float SecularSpreadPerSecond { get; init; }

    /// <summary>How secularism is made.</summary>
    public required SecularWeights Secular { get; init; }

    /// <summary>Share of the gap to its target secularism closed per second.</summary>
    public required float SecularDriftPerSecond { get; init; }

    /// <summary>Share of the gap to its target a doctrine closes per second: cultures change slowly (§33).</summary>
    public required float DoctrineDriftPerSecond { get; init; }

    /// <summary>Hold at which a doctrine is embraced.</summary>
    public required float Embraced { get; init; }

    /// <summary>Hold a miracle that touches a doctrine adds to it, times the witnesses' closeness.</summary>
    public required float DoctrineFromMiracle { get; init; }

    /// <summary>How strongly each doctrine bends belief: the factor on the effects it scales.</summary>
    public required float DoctrineEffect { get; init; }

    /// <summary>Share of their stores a charitable settlement gives each second to a starving neighbour, times its charity.</summary>
    public required float CharityPerSecond { get; init; }

    /// <summary>Conversion per second a temple's missionaries make in each settlement within <see cref="MissionRange"/> regions, of the people who follow no god.</summary>
    public required float MissionPerSecond { get; init; }

    /// <summary>How far, in regions, missionaries go.</summary>
    public required int MissionRange { get; init; }

    /// <summary>Resentment of a god's followers at which they may rise against it.</summary>
    public required float RebellionResentment { get; init; }

    /// <summary>Share of the god's followers who renounce it in a rebellion.</summary>
    public required float RebellionShare { get; init; }

    /// <summary>Share of resentment a rebellion vents.</summary>
    public required float RebellionVent { get; init; }

    /// <summary>Share of a god's conduct that fades per second: the world forgets slowly.</summary>
    public required float ConductFadePerSecond { get; init; }

    /// <summary>Weight of conduct at which a trait shows.</summary>
    public required float TraitThreshold { get; init; }

    /// <summary>Extra doubt from betrayal of love per unit of a god's compassion: the more it was known for mercy, the deeper the shock (§34).</summary>
    public required float Contradiction { get; init; }
}

/// <summary>The weights of a settlement's secularism (spec §35): what turns people from the gods, and what holds them.</summary>
public sealed record SecularWeights
{
    /// <summary>Weight of doubt among its followers: repeated divine failure.</summary>
    public required float Doubt { get; init; }

    /// <summary>Weight of the share that follows no god.</summary>
    public required float Godless { get; init; }

    /// <summary>Weight of an Old Faith's share: contradictions.</summary>
    public required float Schism { get; init; }

    /// <summary>Weight of resentment: destruction.</summary>
    public required float Resentment { get; init; }

    /// <summary>Weight of science, per level: technological development.</summary>
    public required float Science { get; init; }

    /// <summary>How much faith among its followers holds it back.</summary>
    public required float Faith { get; init; }
}

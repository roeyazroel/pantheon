using Pantheon.Domain.Culture;

namespace Pantheon.Game.Culture;

/// <summary>Words for culture (ADR-024): doctrines, secular stages and gods' traits.</summary>
public static class CultureText
{
    /// <summary>A doctrine's name.</summary>
    /// <param name="doctrine">The doctrine.</param>
    /// <returns>For example "nature worship".</returns>
    public static string Name(Doctrine doctrine) => doctrine switch
    {
        Doctrine.Pacifism => "pacifism",
        Doctrine.Militarism => "militarism",
        Doctrine.Sacrifice => "sacrifice",
        Doctrine.Charity => "charity",
        Doctrine.NatureWorship => "nature worship",
        Doctrine.KnowledgeSeeking => "knowledge seeking",
        Doctrine.Expansionism => "expansionism",
        Doctrine.Isolationism => "isolationism",
    };

    /// <summary>A secular stage's name.</summary>
    /// <param name="stage">The stage.</param>
    /// <returns>For example "atheist".</returns>
    public static string Name(SecularStage stage) => stage switch
    {
        SecularStage.Devout => "devout",
        SecularStage.Agnostic => "agnostic",
        SecularStage.Atheist => "atheist",
        SecularStage.AntiDivine => "against every god",
    };

    /// <summary>A trait's name.</summary>
    /// <param name="trait">The trait.</param>
    /// <returns>For example "merciful".</returns>
    public static string Name(GodTrait trait) => trait switch
    {
        GodTrait.Compassionate => "compassionate",
        GodTrait.Cruel => "cruel",
        GodTrait.Vengeful => "vengeful",
        GodTrait.Merciful => "merciful",
        GodTrait.Generous => "generous",
        GodTrait.Deceptive => "deceptive",
        GodTrait.Honest => "honest",
        GodTrait.Chaotic => "chaotic",
        GodTrait.Ambitious => "ambitious",
    };
}

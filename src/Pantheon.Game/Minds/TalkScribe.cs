using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Game.Minds;

/// <summary>
/// A god's words without a model (ADR-066): its greeting, its answers to what the player asks, and
/// what the player might say, each the asking of an act. The god does only what it is willing to.
/// </summary>
public static class TalkScribe
{
    /// <summary>The act id of asking a god what it wants.</summary>
    public const string Ask = "ask";

    // The most suggestions offered at once, the question of what it wants included.
    private const int MostSuggestions = 5;

    /// <summary>How a god greets the player, by how things stand between them.</summary>
    /// <param name="world">The world.</param>
    /// <param name="god">The god.</param>
    /// <param name="me">The player's god.</param>
    /// <returns>The greeting, in quotes.</returns>
    public static string Greeting(Domain.World.WorldState world, GodState god, int me)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        var player = world.Gods.Find(g => g.Id == me)?.Name ?? "stranger";
        var view = DiplomacyModel.FindRelation(world, god.Id, me);
        return DiplomacyModel.StatusOf(world, god.Id, me) switch
        {
            DiplomaticStatus.War => $"“Speak quickly, {player}. Our people are dying while you talk.”",
            DiplomaticStatus.Alliance => $"“Ally. What do you need of me?”",
            DiplomaticStatus.Hostile => $"“You have a nerve, {player}, coming to me.”",
            DiplomaticStatus.Friendly or DiplomaticStatus.Neutral => view switch
            {
                { Trust: >= 0.65f } => $"“{player}. You are welcome here.”",
                { Trust: <= 0.35f } => $"“{player}. I have not forgotten what you are.”",
                _ => $"“Speak, {player}. I am listening.”",
            },
        };
    }

    /// <summary>What the player might ask: what the god wants, then the deals it is likeliest to agree to.</summary>
    /// <param name="acts">The acts the god may take.</param>
    /// <param name="goodwill">The conversation's goodwill.</param>
    /// <param name="reach">How far full goodwill moves the god.</param>
    /// <param name="world">The world, for the names of the gods a treaty concerns.</param>
    /// <returns>The suggestions, each the asking of an act.</returns>
    public static IReadOnlyList<TalkSuggestion> Suggestions(IReadOnlyList<TalkAct> acts, float goodwill, float reach, Domain.World.WorldState? world = null)
    {
        ArgumentNullException.ThrowIfNull(acts);
        var asks = acts
            .Where(a => a.Kind is TalkActKind.Alliance or TalkActKind.Peace or TalkActKind.Treaty or TalkActKind.Accept)
            .OrderBy(a => a.Willingness(goodwill, reach))
            .ThenByDescending(a => a.Utility)
            .Take(MostSuggestions - 1)
            .Select(a => new TalkSuggestion(Asking(a, world), a.Id));
        return [new TalkSuggestion("What do you want?", Ask), .. asks];
    }

    /// <summary>The god's answer to an act asked of it without a model.</summary>
    /// <param name="god">The god.</param>
    /// <param name="act">The act asked, or <c>null</c> for what it wants.</param>
    /// <param name="willing">Whether it is willing.</param>
    /// <returns>Its words, in quotes.</returns>
    public static string Answer(GodState god, TalkAct? act, bool willing)
    {
        ArgumentNullException.ThrowIfNull(god);
        if (act is null)
        {
            return god.Persona is { } persona ? $"“{Capital(persona.Desire.Trim().TrimEnd('.'))}. That is what I want.”" : "“Followers. What else is there?”";
        }

        if (!willing)
        {
            return god.Personality switch
            {
                GodPersonality.Tyrant or GodPersonality.Warlord => "“You presume too much.”",
                GodPersonality.Trickster or GodPersonality.Opportunist => "“And what would I gain? No.”",
                GodPersonality.Isolationist => "“No. Leave me be.”",
                GodPersonality.Zealot => "“Not while you stand against the true faith.”",
                GodPersonality.Guardian or GodPersonality.NatureKeeper or GodPersonality.None => "“Not now. Give me reason to trust you.”",
            };
        }

        return act.Kind switch
        {
            TalkActKind.Alliance => "“Then we stand together.”",
            TalkActKind.Peace => "“Enough. Let there be peace.”",
            TalkActKind.Treaty or TalkActKind.Accept => "“So be it.”",
            TalkActKind.Decline or TalkActKind.ProposeAlliance or TalkActKind.ProposePeace or TalkActKind.DeclareWar or TalkActKind.BreakAlliance => "“Very well.”",
        };
    }

    /// <summary>The player's words asking an act.</summary>
    private static string Asking(TalkAct act, Domain.World.WorldState? world) => act.Kind switch
    {
        TalkActKind.Alliance => "Let us be allies.",
        TalkActKind.Peace => "Let there be peace between us.",
        TalkActKind.Accept => "Will you accept my proposal?",
        TalkActKind.Treaty => act.Deal?.Diplomacy is { Terms: not null } deal ? Proposing(deal, world) : "Let us make a treaty.",
        TalkActKind.Decline or TalkActKind.ProposeAlliance or TalkActKind.ProposePeace or TalkActKind.DeclareWar or TalkActKind.BreakAlliance => act.Words,
    };

    /// <summary>The player proposing a treaty, in their own voice.</summary>
    private static string Proposing(DiplomaticCommand deal, Domain.World.WorldState? world)
    {
        var terms = deal.Terms!;
        var against = terms.TargetGodId is { } id && world?.Gods.Find(g => g.Id == id) is { } target ? " against " + target.Name : string.Empty;
        var lasting = terms.DurationSeconds > 0f ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $", for {terms.DurationSeconds / 60f:0} minutes") : string.Empty;
        return terms.Kind switch
        {
            Domain.Politics.TreatyKind.Tribute => string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{(terms.PayerGodId == deal.GodId ? "I will pay you tribute" : "Pay me tribute")}: {terms.Amount:0} power a minute{lasting}."),
            Domain.Politics.TreatyKind.JointWar => $"Let us make war together{against}{lasting}.",
            Domain.Politics.TreatyKind.DefensivePact => $"Let us make a defensive pact{against}{lasting}.",
            Domain.Politics.TreatyKind.NonAggression or Domain.Politics.TreatyKind.SharedInfluence or Domain.Politics.TreatyKind.ReligiousAccess
                or Domain.Politics.TreatyKind.CreatureNonAggression or Domain.Politics.TreatyKind.DisasterAssistance or Domain.Politics.TreatyKind.MutualDefense
                or Domain.Politics.TreatyKind.TerritorialAgreement or Domain.Politics.TreatyKind.MigrationAgreement => $"Let us make a {Politics.PoliticsText.Name(terms.Kind)}{lasting}.",
        };
    }

    /// <summary>A line with a capital first letter.</summary>
    private static string Capital(string line) => line.Length == 0 ? line : char.ToUpperInvariant(line[0]) + line[1..];
}

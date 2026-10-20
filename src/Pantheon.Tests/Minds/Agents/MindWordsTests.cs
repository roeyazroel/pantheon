using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Game.Minds;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Minds.Agents;

/// <summary>A god's stance towards another in its own words, for its mind (ADR-066, ADR-072).</summary>
public sealed class MindWordsTests
{
    [Fact]
    public void AGodsFeeling_IsInItsOwnWords()
    {
        var view = new GodRelation { ObserverGodId = 2, SubjectGodId = 1, Trust = 0.2f, Respect = 0.5f, Rivalry = 0.7f, Fear = 0f, Gratitude = 0f, Suspicion = 0.6f, Memories = [] };

        Assert.Equal("you distrust Ilyra, see it as a rival and suspect its motives", MindWords.Feeling(view, "Ilyra"));
        Assert.Equal("you have formed no view of Ilyra yet", MindWords.Feeling(null, "Ilyra"));
    }

    [Fact]
    public void AGodsMemories_AreOfWhatTheOtherDidToIt()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var village = GodPlanner.Home(session.World, rival.Id)!;
        var view = DiplomacyModel.EnsureRelation(session.Content.Diplomacy, session.World, rival.Id, me);
        view.Memories.Add(new DiplomaticMemory { Tick = 1, Act = DiplomaticAct.CreatureAttacked, SettlementId = village.Id, Weight = 3f });
        view.Memories.Add(new DiplomaticMemory { Tick = 2, Act = DiplomaticAct.BrokePromise, SettlementId = null, Weight = 2f });
        view.Memories.Add(new DiplomaticMemory { Tick = 3, Act = DiplomaticAct.KeptWord, SettlementId = null, Weight = 1f });

        var remembered = MindWords.Remembered(view, session.World, "Ilyra");

        Assert.Equal($"Ilyra let its Creature attack your followers in {village.Name}; broke a promise to you; kept its word to you", remembered);
        Assert.StartsWith("you ", MindWords.Stance(session.World, rival.Id, me, "Ilyra"));
        Assert.Contains("; Ilyra let its Creature", MindWords.Stance(session.World, rival.Id, me, "Ilyra"));
        Assert.Equal(string.Empty, MindWords.Remembered(null, session.World, "Ilyra"));
    }
}

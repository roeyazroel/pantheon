using System;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Hud;
using Pantheon.Persistence;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The session as the authority for the local player's god: casting, events, commands and the HUD.</summary>
public sealed class DivineSessionTests
{
    private readonly GameSession _session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));

    private GroundPosition Home => _session.World.Settlements[0].Position;

    [Fact]
    public void NewSession_PlaysTheHumanGod_FollowedInTheFirstSettlement()
    {
        var god = Assert.IsType<GodState>(_session.LocalGod);

        Assert.Equal(PantheonFounder.FirstGodId, _session.LocalGodId);
        Assert.Equal(GodController.Human, god.Controller);
        Assert.True(GodStanding.CountFollowers(_session.World, god.Id) > 0);
    }

    [Fact]
    public void CheckCast_ChangesNothing()
    {
        var before = WorldSaveSerializer.Serialize(_session.World);

        var check = _session.CheckCast("food", Home);

        Assert.True(check.IsAccepted);
        Assert.Equal(before, WorldSaveSerializer.Serialize(_session.World));
    }

    [Fact]
    public void CastMiracle_SpendsPower_AndRaisesAnEventOnce()
    {
        var power = _session.LocalGod!.DivinePower;

        var check = _session.CastMiracle("food", Home);

        Assert.True(check.IsAccepted);
        Assert.Equal(power - check.Cost, _session.LocalGod!.DivinePower, 3);
        Assert.Single(_session.DrainEvents().OfType<MiracleCast>());
        Assert.Empty(_session.DrainEvents());
    }

    [Fact]
    public void Ticks_RaiseEventsForTheSession()
    {
        foreach (var settlement in _session.World.Settlements)
        {
            settlement.Food = 0.01f;
            foreach (var region in _session.World.Regions.Cells)
            {
                region.Fertility = 0f;
            }
        }

        _session.RunTicks(10);

        Assert.Contains(_session.DrainEvents(), e => e is FamineStarted);
    }

    [Fact]
    public void CastCommand_DescribesTheResult_OrWhyItWasRefused()
    {
        var name = _session.World.Settlements[0].Name;

        var cast = SessionCommands.CastMiracle(_session, "food", Home);
        var again = SessionCommands.CastMiracle(_session, "food", Home);
        var locked = SessionCommands.CastMiracle(_session, "mass_healing", Home);

        Assert.StartsWith($"Food on {name} for ", cast, StringComparison.Ordinal);
        Assert.StartsWith("Food is ready in 10.0 s", again, StringComparison.Ordinal);
        Assert.Equal("Mass Healing needs the rank of Lesser God", locked);
    }

    [Fact]
    public void GrantPower_AddsDivinePower()
    {
        var before = _session.LocalGod!.DivinePower;

        var status = SessionCommands.GrantPower(_session);

        Assert.Equal(before + SessionCommands.GrantedPower, _session.LocalGod!.DivinePower);
        Assert.Contains("gains 500 Divine Power", status, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangeSpeed_StepsThroughThePresets()
    {
        Assert.Equal("Simulation speed x4", SessionCommands.ChangeSpeed(_session, 1));
        Assert.Equal("Simulation speed x16", SessionCommands.ChangeSpeed(_session, 1));
        Assert.Equal("Simulation speed x16", SessionCommands.ChangeSpeed(_session, 1));
        Assert.Equal("Simulation speed x4", SessionCommands.ChangeSpeed(_session, -1));
    }

    [Fact]
    public void Hud_ShowsPowerFollowersAndTheMiracleBar()
    {
        var hud = HudReadout.Build(_session, selectedMiracleId: "food", target: Home);

        Assert.True(hud.HasGod);
        Assert.StartsWith(_session.LocalGod!.Name + " - Minor Spirit", hud.GodLine, StringComparison.Ordinal);
        Assert.Equal("100", hud.PowerText);
        Assert.EndsWith("Lesser God at 100", hud.FollowersText, StringComparison.Ordinal);
        Assert.Equal(TestContent.Shipped.Miracles.Miracles.Count, hud.Miracles.Count);
        Assert.Equal(Enumerable.Range(1, hud.Miracles.Count), hud.Miracles.Select(slot => slot.Hotkey));
        Assert.True(hud.Miracles[0].Selected);
        Assert.True(hud.Miracles.Single(slot => slot.MiracleId == "mass_healing").Locked);
        Assert.True(hud.TargetValid);
        Assert.StartsWith($"Food on {_session.World.Settlements[0].Name}: costs", hud.TargetHint, StringComparison.Ordinal);
        Assert.Equal(TestContent.Shipped.Miracles.Find("food")!.Radius, hud.TargetRadius);
    }

    [Fact]
    public void Hud_ExplainsAnInvalidTarget()
    {
        var hud = HudReadout.Build(_session, selectedMiracleId: "food", target: new GroundPosition(5f, 5f));

        Assert.False(hud.TargetValid);
        Assert.StartsWith("Outside your influence", hud.TargetHint, StringComparison.Ordinal);
        Assert.Null(HudReadout.Build(_session, null, Home).TargetHint);
    }

    [Fact]
    public void Hud_ShowsCooldowns()
    {
        _session.CastMiracle("food", Home);

        var hud = HudReadout.Build(_session, null, null);

        Assert.Equal(10.0, hud.Miracles[0].CooldownSeconds, 3);
        Assert.Equal(0.0, hud.Miracles[1].CooldownSeconds);
    }

    [Fact]
    public void WorldWithoutGods_HasNoLocalGod()
    {
        var world = WorldGeneratorWithoutGods();
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pantheon-tests", Guid.NewGuid().ToString("N") + ".json");
        WorldSaveSerializer.SaveToFile(world, path);

        var session = GameSession.Load(TestContent.Small, path);

        Assert.Null(session.LocalGodId);
        Assert.Equal(CastRejection.UnknownGod, session.CastMiracle("food", Home).Rejection);
        Assert.False(HudReadout.Build(session, "food", Home).HasGod);
        System.IO.File.Delete(path);
    }

    /// <summary>A generated world with no gods, as a test of a world built without a pantheon.</summary>
    private static WorldState WorldGeneratorWithoutGods() =>
        Pantheon.Simulation.WorldGen.WorldGenerator.Generate(new WorldSeed(8), TestContent.SmallWorldGen).State;
}

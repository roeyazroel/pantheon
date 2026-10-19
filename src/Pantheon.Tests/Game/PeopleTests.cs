using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Game.Villagers;
using Pantheon.Simulation.Settlements;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>People that look and move like people (spec §61, §80, ADR-049).</summary>
public sealed class PeopleTests
{
    private static readonly PeopleSettings People = TestContent.SettlementView.People;
    private static readonly CitizenSettings Citizens = TestContent.SettlementView.Citizens;

    [Fact]
    public void TheCrowdsClock_KeepsAHumanPace_AtAnySpeed_RunsSlowerBelowRealTime_AndStopsWhenPaused()
    {
        var clock = new CrowdClock();

        clock.Advance(2.0, 1.0, paused: false, Citizens.PaceCap);
        clock.Advance(2.0, 16.0, paused: false, Citizens.PaceCap);
        clock.Advance(2.0, 0.5, paused: false, Citizens.PaceCap);
        clock.Advance(5.0, 4.0, paused: true, Citizens.PaceCap);

        Assert.Equal(1f, Citizens.PaceCap);
        Assert.Equal(5.0, clock.Seconds, 6);
        Assert.Equal(9.0, new CrowdClock().Advance(3.0, 16.0, paused: false, 3f), 6);
    }

    [Fact]
    public void F1_ShowsThePeoplesOwnClock_AndThatTheyAreKeptToAHumanPace()
    {
        var world = SettlementWorlds.Create(population: 540, housing: 645, food: 2000f, fertility: 0.5f);
        var crowd = new CrowdStatus(SettlementWorlds.Settlement(world).Id, SimulationTier.Regional, 300, 1.8f, 90f, 450f, 550f);
        var snapshot = new Pantheon.Game.Diagnostics.DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = TestContent.Shipped.DivineRules,
            IsPaused = false,
            TimeScale = 16,
            TicksPerSecond = 10,
            TerrainBackend = "Terrain3D",
            TerrainIsFallback = false,
            ZoomBand = Pantheon.Game.Camera.CameraZoomBand.Regional,
            CameraDistance = 90f,
            RegionBoundariesVisible = false,
            Crowds = [crowd],
            CrowdClock = (123.4, 1f),
        };

        var lines = Pantheon.Game.Diagnostics.DebugReadout.Build(snapshot).Lines;

        Assert.Contains("  people: their own clock at 123 s, running x1 with the game at x16, kept to a human pace", lines);
        Assert.Contains("  people: their own clock at 123 s, running x0 with the game at x16, kept to a human pace", Pantheon.Game.Diagnostics.DebugReadout.Build(snapshot with { IsPaused = true }).Lines);
    }

    [Fact]
    public void EachPerson_LooksTheirOwn_TheSameEachTime_AndThePeopleOfAVillageVary()
    {
        var looks = Enumerable.Range(0, 300).Select(i => PersonLooks.Of(People, 1234UL, 3, i, (CitizenRole)(i % 3))).ToList();

        Assert.Equal(looks[17], PersonLooks.Of(People, 1234UL, 3, 17, CitizenRole.Villager));
        Assert.NotEqual(looks[17], PersonLooks.Of(People, 1234UL, 4, 17, CitizenRole.Villager));
        Assert.True(looks.Min(l => l.Skin) < 0.1f && looks.Max(l => l.Skin) > 0.9f, "skin tones should span the range");
        Assert.Equal(People.HairColors.Count, looks.Select(l => l.Hair).Distinct().Count());
        Assert.All(looks, l => Assert.InRange(l.Height, People.MinHeight, People.MaxHeight));
        Assert.True(looks.Select(l => l.Clothes).Distinct().Count() >= 10);
        var farmers = looks.Where((_, i) => i % 3 == (int)CitizenRole.Farmer).ToList();
        var builders = looks.Where((_, i) => i % 3 == (int)CitizenRole.Builder).ToList();
        Assert.All(farmers, l => Assert.Contains(l.Clothes, People.FarmerClothes));
        Assert.InRange(farmers.Count(l => l.Headwear == PersonHeadwear.StrawHat) / (float)farmers.Count, People.FarmerHatChance - 0.12f, People.FarmerHatChance + 0.12f);
        Assert.DoesNotContain(farmers, l => l.Headwear == PersonHeadwear.Scarf);
        Assert.DoesNotContain(builders, l => l.Dress || l.Headwear == PersonHeadwear.StrawHat);
        Assert.Equal(5f + (8 * 2) + 32, new PersonLook(default, 0.5f, 5, PersonHeadwear.Scarf, true, 1f).Packed);
    }

    [Fact]
    public void WhatAFigureDoes_PicksItsMotion_SittingHomeHoeingHammeringChoresAndPrayer()
    {
        Assert.Equal(PersonMotion.Sitting, PersonMotions.Of(CitizenActivity.AtHome, CitizenRole.Farmer));
        Assert.Equal(PersonMotion.Walking, PersonMotions.Of(CitizenActivity.Walking, CitizenRole.Builder));
        Assert.Equal(PersonMotion.Praying, PersonMotions.Of(CitizenActivity.Gathering, CitizenRole.Villager));
        Assert.Equal(
            [PersonMotion.Hoeing, PersonMotion.Hammering, PersonMotion.Chores],
            new[] { CitizenRole.Farmer, CitizenRole.Builder, CitizenRole.Villager }.Select(r => PersonMotions.Of(CitizenActivity.Working, r)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheFigure_StandsOnItsFeet_WithEveryPartInPlace_AndFacesOutwards(bool detailed)
    {
        var mesh = PersonMesh.Build(detailed);
        IEnumerable<Vector3> Of(PersonPart part) => mesh.Positions.Where((_, i) => (int)mesh.Tags[i].X == (int)part);

        Assert.All(Enum.GetValues<PersonPart>(), part => Assert.NotEmpty(Of(part)));
        Assert.InRange(mesh.Positions.Min(p => p.Y), -0.01f, 0.01f);
        Assert.InRange(Of(PersonPart.Hair).Max(p => p.Y), PersonMesh.Height - 0.03f, PersonMesh.Height + 0.03f);
        Assert.All(Of(PersonPart.LeftLeg).Concat(Of(PersonPart.RightLeg)), p => Assert.True(p.Y <= PersonMesh.HipHeight + 0.05f));
        Assert.All(Of(PersonPart.LeftArm), p => Assert.True(p.X > 0.15f && p.Y <= PersonMesh.ShoulderHeight + 0.08f));
        Assert.All(Of(PersonPart.RightArm), p => Assert.True(p.X < -0.15f));
        Assert.All(Of(PersonPart.Hoe).Concat(Of(PersonPart.Hammer)), p => Assert.True(p.X < -0.15f, "tools are in the right hand"));
        Assert.All(mesh.Normals, n => Assert.Equal(1f, n.Length(), 3));
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Positions.Length - 1));
        for (var t = 0; t < mesh.Indices.Length; t += 3)
        {
            var (a, b, c) = (mesh.Positions[mesh.Indices[t]], mesh.Positions[mesh.Indices[t + 1]], mesh.Positions[mesh.Indices[t + 2]]);
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), mesh.Normals[mesh.Indices[t]]) <= 1e-6f, "Godot's front faces wind clockwise");
        }

        Assert.True(PersonMesh.Build(detailed: false).Indices.Length * 2 < PersonMesh.Build(detailed: true).Indices.Length, "the far figure is much simpler");
    }

    [Fact]
    public void TheRoutine_CountsStridesWhileWalking_StrokesWhileWorking_AndBreathsAtRest()
    {
        var farmer = new RepresentativeCitizen(0, CitizenRole.Farmer, 0, Vector2.Zero, new Vector2(40f, 0f), 0f, new Vector2(0f, 30f), 0f, 0f);
        var walking = Citizens.RestSeconds + 5.0;
        var working = Citizens.RestSeconds + (40.0 / Citizens.WalkSpeed) + 10.0;

        var walk = CitizenRoutine.Evaluate(farmer, walking, Citizens);
        var work = CitizenRoutine.Evaluate(farmer, working, Citizens);
        var rest = CitizenRoutine.Evaluate(farmer, 8.0, Citizens);

        Assert.Equal((CitizenActivity.Walking, 5f * Citizens.WalkSpeed / (2f * Citizens.StrideLength)), (walk.Activity, walk.Cycle));
        Assert.Equal(CitizenActivity.Working, work.Activity);
        Assert.Equal(10f * Citizens.WorkStrokesPerSecond, work.Cycle, 3);
        Assert.Equal((CitizenActivity.AtHome, 2f), (rest.Activity, rest.Cycle));
    }

    [Fact]
    public void Slash_PicksTheFigureWhereTheCrowdsClockShowsThem_NotWhereSimulationTimeWould()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var view = TestContent.SettlementView;
        var village = session.World.Settlements.First(s => s.Population > 20);
        session.Crowds.Advance(61.0, 16.0, paused: false, view.Citizens.PaceCap);
        var rules = session.Content.SettlementRules;
        var site = SettlementLayout.ComputeSite(village, rules, session.Terrain, view.Layout);
        var roster = RepresentativeRoster.Build(session.World.Seed, village, SettlementEconomy.AllocateLabour(rules, village), site, view);
        var walker = roster.First(c => CitizenRoutine.Evaluate(c, session.Crowds.Seconds, view.Citizens).Activity == CitizenActivity.Walking);
        var at = CitizenRoutine.Evaluate(walker, session.Crowds.Seconds, view.Citizens).Ground;

        var picked = VillagerCommands.Pick(session, view, new GroundPosition(at.X, at.Y), radius: 0.01f);

        Assert.Equal(61.0, session.Crowds.Seconds, 6);
        Assert.Equal(walker.Index, picked?.Slot);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Game.Presentation;
using Pantheon.Game.Presentation.Animals;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Animals;

/// <summary>The Creatures' bodies (ADR-061): their shapes, meshes and paint.</summary>
public sealed class AnimalBodyTests
{
    private static readonly AnimalPalette Palette = new(new(0.9f, 0.5f, 0.1f), new(1f, 1f, 1f), new(0.9f, 0.7f, 0.7f), new(0.2f, 0.1f, 0.1f), new(1f, 0.8f, 0.8f), new(0.9f, 0.9f, 0.8f), new(0.2f, 0.2f, 0.2f), new(0.4f, 0.2f, 0.1f), new(0.4f, 0.3f, 0.2f), new(0.1f, 0.1f, 0.1f));

    [Fact]
    public void ASphere_MeshesToAClosedSurface_OnItsRadius_FacingOut()
    {
        var sphere = new SdfShape([new SdfEllipsoid { Centre = Vector3.Zero, Radii = new Vector3(1f), Tag = PaintTag.Fur }]);

        var mesh = SurfaceNets.Mesh(sphere, 0.1f, (_, _) => Vector3.One);

        Assert.All(mesh.Positions, p => Assert.InRange(p.Length(), 0.93f, 1.07f));
        Assert.All(mesh.Normals, n => Assert.InRange(n.Length(), 0.99f, 1.01f));
        for (var i = 0; i < mesh.Indices.Length; i += 3)
        {
            var (a, b, c) = (mesh.Positions[mesh.Indices[i]], mesh.Positions[mesh.Indices[i + 1]], mesh.Positions[mesh.Indices[i + 2]]);
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f) > 0f, "each triangle faces out of the shape");
        }

        // A closed surface shares every edge between exactly two triangles.
        var edges = new Dictionary<(int, int), int>();
        for (var i = 0; i < mesh.Indices.Length; i += 3)
        {
            for (var e = 0; e < 3; e++)
            {
                var (u, v) = (mesh.Indices[i + e], mesh.Indices[i + ((e + 1) % 3)]);
                var key = (Math.Min(u, v), Math.Max(u, v));
                edges[key] = edges.GetValueOrDefault(key) + 1;
            }
        }

        Assert.All(edges.Values, count => Assert.Equal(2, count));
    }

    [Fact]
    public void BlendedParts_MeltTogether_AndTheNearestPartsPaintShows()
    {
        var body = new SdfEllipsoid { Centre = Vector3.Zero, Radii = new Vector3(1f), Tag = PaintTag.Fur };
        var snout = new SdfRoundCone { A = new Vector3(0f, 0f, 0.5f), B = new Vector3(0f, 0f, 1.6f), RadiusA = 0.4f, RadiusB = 0.25f, Tag = PaintTag.Muzzle, Blend = 0.3f };
        var shape = new SdfShape([body, snout]);

        Assert.True(shape.Distance(new Vector3(0f, 0f, 1.5f)) < 0f, "inside the snout");
        Assert.True(shape.Distance(new Vector3(0f, 0.42f, 0.95f)) < Math.Min(body.Distance(new Vector3(0f, 0.42f, 0.95f)), snout.Distance(new Vector3(0f, 0.42f, 0.95f))), "the blend fills the crease");
        Assert.Equal(PaintTag.Muzzle, shape.TagAt(new Vector3(0f, 0f, 1.84f)));
        Assert.Equal(PaintTag.Fur, shape.TagAt(new Vector3(0f, -1f, 0f)));
        Assert.Throws<ArgumentException>(() => new SdfShape([snout with { Carve = true }]));
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public void EveryBody_StandsWithItsSolesOnTheGround_AndHasAHeadAboveItsShoulders(string name)
    {
        var plan = AnimalPlans.For(name);
        var b = plan.Body;

        Assert.Equal(name, plan.Name);
        Assert.Equal(b.Ankle + (AnimalProportions.StandingReach * (b.Thigh + b.Shin)), b.HipHeight, 4);
        var sole = new SdfShape(plan.Foot).Bounds().Min.Y;
        var footMesh = SurfaceNets.Mesh(new SdfShape(plan.Foot), 0.05f, (_, _) => Vector3.One);
        Assert.InRange(footMesh.Positions.Min(p => p.Y), -b.Ankle - 0.12f, -b.Ankle + 0.1f);
        Assert.True(sole < 0f);
        Assert.True(b.Torso + b.Neck > b.Torso, "the head sits above the shoulders");
        Assert.True(plan.Face.Eye.X > 0f && plan.Face.Eye.Z > 0f, "the eyes look forward from either side");
        Assert.NotEmpty(SurfaceNets.Mesh(new SdfShape(plan.Head), 0.08f, (_, _) => Vector3.One).Indices);
    }

    [Fact]
    public void EveryAnimal_IsADifferentBody_AndEveryShippedSpeciesHasOne()
    {
        var view = TestContent.DivineView;
        var species = TestContent.Shipped.Creatures.Species.Select(s => s.Name).ToList();

        Assert.Equal(8, species.Count);
        Assert.All(species, s => Assert.Contains(view.LookFor(s).BodyName, AnimalPlans.Names));
        Assert.Equal(species.Count, species.Select(s => view.LookFor(s).BodyName).Distinct().Count());
        Assert.Equal("Beast", view.LookFor("Beast").BodyName);
    }

    [Fact]
    public void ALookWithAnUnknownBody_IsRefused()
    {
        var text = TestContent.ReadText(DivineViewSettings.FileName).Replace("\"Species\": \"Cow\",", "\"Species\": \"Cow\", \"Body\": \"Griffin\",", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => DivineViewSettings.Parse(text));

        Assert.Contains("Griffin", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTiger_IsStriped_TheCow_Patched_AndTheirOtherPartsKeepTheirColours()
    {
        var points = Enumerable.Range(0, 400).Select(i => new Vector3(MathF.Sin(i * 0.37f), i * 0.012f, MathF.Cos(i * 0.21f))).ToList();

        var stripes = points.Select(p => AnimalPaint.Color(PaintTag.Fur, p, Palette, FurPattern.Stripes, 1)).ToList();
        var patches = points.Select(p => AnimalPaint.Color(PaintTag.Fur, p * 3f, Palette, FurPattern.Patches, 1)).ToList();

        Assert.Contains(stripes, c => c.X < 0.2f);
        Assert.Contains(stripes, c => c.X > 0.6f);
        Assert.Contains(patches, c => c.X < 0.2f);
        Assert.Contains(patches, c => c.X > 0.6f);
        Assert.All(points, p => Assert.True(AnimalPaint.Color(PaintTag.Belly, p, Palette, FurPattern.Stripes, 1).Y > 0.85f, "a belly is never striped"));
        Assert.Equal(AnimalPaint.Color(PaintTag.Fur, points[3], Palette, FurPattern.Patches, 2), AnimalPaint.Color(PaintTag.Fur, points[3], Palette, FurPattern.Patches, 2));
    }

    public static TheoryData<string> Bodies() => [.. AnimalPlans.Names];
}

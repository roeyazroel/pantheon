using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// Nodes of one settlement: the wayshrine, its houses, building site and fields, a floating name
/// label and, near the camera, its representative citizens. <see cref="Sync"/> follows the state,
/// so new houses appear as housing grows.
/// </summary>
internal sealed class SettlementView
{
    // The label floats above the shrine's crown.
    private const float LabelHeight = 11f;
    private const int LabelFontSize = 30;
    private const int LabelOutlineSize = 10;

    // With FixedSize the label keeps its on-screen size; this gives about 30 px per line at 900 px height.
    private const float LabelPixelSize = 0.001f;

    private readonly TerrainField _terrain;
    private readonly SettlementRules _rules;
    private readonly SettlementLayoutSettings _layout;
    private readonly SiteView _siteView;
    private readonly CivicView _civicView;
    private readonly StyleView _styleView;
    private readonly StyleSettings _style;
    private readonly SettlementCrowd _crowd;
    private readonly CitizenView _citizens;
    private readonly DeclutteredLabel _label;
    private readonly DivineViewSettings _divine;
    private (int Housing, float ConstructionProgress, int Ruins)? _siteInputs;

    /// <summary>Creates the settlement's nodes under <paramref name="parent"/> and shows its current state.</summary>
    /// <param name="parent">Node that holds all settlements.</param>
    /// <param name="assets">Shared meshes.</param>
    /// <param name="seed">World seed, for the representative citizens.</param>
    /// <param name="settlement">Settlement to show.</param>
    /// <param name="terrain">Terrain the settlement stands on.</param>
    /// <param name="rules">Settlement rules, for the house count, the work per house and the labour split.</param>
    /// <param name="settings">Settlement presentation settings.</param>
    /// <param name="divine">God colours, for the label.</param>
    /// <param name="world">The world, for god names.</param>
    public SettlementView(Node3D parent, SettlementAssets assets, WorldSeed seed, SettlementState settlement, TerrainField terrain, SettlementRules rules, SettlementViewSettings settings, DivineViewSettings divine, WorldState world)
    {
        _terrain = terrain;
        _divine = divine;
        _rules = rules;
        _layout = settings.Layout;
        var root = parent.AddLayer(string.Create(CultureInfo.InvariantCulture, $"Settlement_{settlement.Id}"));
        var center = new Vector3(settlement.Position.X, terrain.SampleHeight(settlement.Position.X, settlement.Position.Z), settlement.Position.Z);

        AddPart(root, "ShrineBase", assets.ShrineBase, center + SettlementAssets.ShrineBaseOffset);
        AddPart(root, "ShrinePillar", assets.ShrinePillar, center + SettlementAssets.ShrinePillarOffset);
        AddPart(root, "ShrineCrown", assets.ShrineCrown, center + SettlementAssets.ShrineCrownOffset);
        _siteView = new SiteView(root, assets, _layout);
        _civicView = new CivicView(root, assets);
        _style = settings.Style;
        _styleView = new StyleView(root, _style);
        _crowd = new SettlementCrowd(seed, settlement.Id, settings);
        _citizens = new CitizenView(root, assets, settings.Citizens);
        _label = CreateLabel(center + (Vector3.Up * LabelHeight));
        root.AddChild(_label);

        UpdateLabel(settlement, world);
        ShowSite(settlement);
    }

    /// <summary>The layout currently shown.</summary>
    public SettlementSite Site { get; private set; }

    /// <summary>Updates the label and, when housing or construction changed, the houses, building site and fields.</summary>
    /// <param name="settlement">Current settlement state.</param>
    /// <param name="world">The world, for god names.</param>
    /// <param name="fell">Collects the houses that fell since the last sync, for the debris (ADR-048).</param>
    public void Sync(SettlementState settlement, WorldState world, List<HutPlacement> fell)
    {
        UpdateLabel(settlement, world);
        if ((settlement.Housing, settlement.ConstructionProgress, settlement.Ruins) != _siteInputs)
        {
            var before = Site;
            ShowSite(settlement);
            fell.AddRange(DebrisLayout.Fallen(before, Site));
        }

        var temple = settlement.Society.Buildings.Find(b => b.Kind == Domain.Society.BuildingKind.Temple);
        _civicView.Show(settlement, Site, _terrain, temple?.GodId is { } god ? _divine.GodColor(god).ToGodot() : null);

        // Religion's look (ADR-042): laid out again only when the style or the village's reach changes.
        var style = ReligiousStyles.Of(settlement, world, _style);
        var count = style is null ? 0 : (int)System.MathF.Round(_style.MaxPieces * style.Strength * System.Math.Min(1f, (Site.Houses.Count + 2) / 14f));
        var key = StyleView.KeyOf(style, count, Site.HouseDiscRadius);
        _styleView.Show(style, style is null ? [] : key == _styleKey ? [] : ReligiousStyles.Layout(style, settlement, Site, _terrain, _style), key);
        _styleKey = key;
    }

    private string _styleKey = string.Empty;

    /// <summary>Chooses the tier for the camera distance and, in Tier 2, rebuilds and moves the representative citizens.</summary>
    /// <param name="settlement">Current settlement state.</param>
    /// <param name="simulationSeconds">Simulation time, which drives the citizens' routines.</param>
    /// <param name="camera">Camera position in the world.</param>
    /// <param name="followed">The villagers the gods follow, whose figures in the crowd they replace (ADR-043).</param>
    /// <returns>The settlement's tier and representatives.</returns>
    public CrowdStatus UpdateCitizens(SettlementState settlement, double simulationSeconds, Vector3 camera, IReadOnlyList<Pantheon.Domain.Villagers.VillagerState> followed)
    {
        var distance = camera.DistanceTo(Site.Center.ToGodot());
        _crowd.SelectTier(distance);
        if (_crowd.Tier == SimulationTier.Regional)
        {
            _crowd.Refresh(settlement, SettlementEconomy.AllocateLabour(_rules, settlement), Site);
        }

        _crowd.Advance(simulationSeconds, _terrain);
        _citizens.Show(_crowd, settlement.Id, followed);
        return _crowd.GetStatus(settlement.Population, distance);
    }

    /// <summary>Sets the label's text, its name for when space is short and its place by the people, tinted by the god most of them follow.</summary>
    private void UpdateLabel(SettlementState settlement, WorldState world)
    {
        _label.SetTexts(SettlementLabel.Format(settlement, world, _rules), settlement.Name);
        _label.Priority = settlement.Population;

        var main = SettlementLabel.MainBelief(settlement);
        var tint = main is { Adherence: >= 0.5f } ? _divine.GodColor(main.GodId).ToGodot() : Colors.White;
        if (_label.Modulate != tint)
        {
            _label.Modulate = tint;
        }
    }

    /// <summary>Lays the settlement out again and shows the result.</summary>
    [MemberNotNull(nameof(Site))]
    private void ShowSite(SettlementState settlement)
    {
        _siteInputs = (settlement.Housing, settlement.ConstructionProgress, settlement.Ruins);
        Site = SettlementLayout.ComputeSite(settlement, _rules, _terrain, _layout);
        _siteView.Show(Site);
    }

    /// <summary>Adds a single static mesh.</summary>
    private static void AddPart(Node3D root, string name, Mesh mesh, Vector3 position) =>
        root.AddChild(new MeshInstance3D { Name = name, Mesh = mesh, Position = position });

    /// <summary>Billboard label with a constant screen size that draws over the terrain, giving way to bigger villages.</summary>
    private static DeclutteredLabel CreateLabel(Vector3 position) => new()
    {
        Name = "Label",
        Position = position,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        FixedSize = true,
        PixelSize = LabelPixelSize,
        FontSize = LabelFontSize,
        OutlineSize = LabelOutlineSize,
        OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.85f),
        NoDepthTest = true,
        VerticalAlignment = VerticalAlignment.Bottom,
    };
}

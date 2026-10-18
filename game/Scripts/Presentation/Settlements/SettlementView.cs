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
    private readonly SettlementCrowd _crowd;
    private readonly CitizenView _citizens;
    private readonly Label3D _label;
    private (int Housing, float ConstructionProgress)? _siteInputs;

    /// <summary>Creates the settlement's nodes under <paramref name="parent"/> and shows its current state.</summary>
    /// <param name="parent">Node that holds all settlements.</param>
    /// <param name="assets">Shared meshes.</param>
    /// <param name="seed">World seed, for the representative citizens.</param>
    /// <param name="settlement">Settlement to show.</param>
    /// <param name="terrain">Terrain the settlement stands on.</param>
    /// <param name="rules">Settlement rules, for the house count, the work per house and the labour split.</param>
    /// <param name="settings">Settlement presentation settings.</param>
    public SettlementView(Node3D parent, SettlementAssets assets, WorldSeed seed, SettlementState settlement, TerrainField terrain, SettlementRules rules, SettlementViewSettings settings)
    {
        _terrain = terrain;
        _rules = rules;
        _layout = settings.Layout;
        var root = parent.AddLayer(string.Create(CultureInfo.InvariantCulture, $"Settlement_{settlement.Id}"));
        var center = new Vector3(settlement.Position.X, terrain.SampleHeight(settlement.Position.X, settlement.Position.Z), settlement.Position.Z);

        AddPart(root, "ShrineBase", assets.ShrineBase, center + SettlementAssets.ShrineBaseOffset);
        AddPart(root, "ShrinePillar", assets.ShrinePillar, center + SettlementAssets.ShrinePillarOffset);
        AddPart(root, "ShrineCrown", assets.ShrineCrown, center + SettlementAssets.ShrineCrownOffset);
        _siteView = new SiteView(root, assets, _layout);
        _crowd = new SettlementCrowd(seed, settlement.Id, settings);
        _citizens = new CitizenView(root, assets, settings.Citizens);
        _label = CreateLabel(center + (Vector3.Up * LabelHeight));
        root.AddChild(_label);

        UpdateLabel(settlement);
        ShowSite(settlement);
    }

    /// <summary>The layout currently shown.</summary>
    public SettlementSite Site { get; private set; }

    /// <summary>Updates the label and, when housing or construction changed, the houses, building site and fields.</summary>
    /// <param name="settlement">Current settlement state.</param>
    public void Sync(SettlementState settlement)
    {
        UpdateLabel(settlement);
        if ((settlement.Housing, settlement.ConstructionProgress) != _siteInputs)
        {
            ShowSite(settlement);
        }
    }

    /// <summary>Chooses the tier for the camera distance and, in Tier 2, rebuilds and moves the representative citizens.</summary>
    /// <param name="settlement">Current settlement state.</param>
    /// <param name="simulationSeconds">Simulation time, which drives the citizens' routines.</param>
    /// <param name="camera">Camera position in the world.</param>
    /// <returns>The settlement's tier and representatives.</returns>
    public CrowdStatus UpdateCitizens(SettlementState settlement, double simulationSeconds, Vector3 camera)
    {
        var distance = camera.DistanceTo(Site.Center.ToGodot());
        _crowd.SelectTier(distance);
        if (_crowd.Tier == SimulationTier.Regional)
        {
            _crowd.Refresh(settlement, SettlementEconomy.AllocateLabour(_rules, settlement), Site);
        }

        _crowd.Advance(simulationSeconds, _terrain);
        _citizens.Show(_crowd);
        return _crowd.GetStatus(settlement.Population, distance);
    }

    /// <summary>Sets the label text when it changed.</summary>
    private void UpdateLabel(SettlementState settlement)
    {
        var text = SettlementLabel.Format(settlement);
        if (_label.Text != text)
        {
            _label.Text = text;
        }
    }

    /// <summary>Lays the settlement out again and shows the result.</summary>
    [MemberNotNull(nameof(Site))]
    private void ShowSite(SettlementState settlement)
    {
        _siteInputs = (settlement.Housing, settlement.ConstructionProgress);
        Site = SettlementLayout.ComputeSite(settlement, _rules, _terrain, _layout);
        _siteView.Show(Site);
    }

    /// <summary>Adds a single static mesh.</summary>
    private static void AddPart(Node3D root, string name, Mesh mesh, Vector3 position) =>
        root.AddChild(new MeshInstance3D { Name = name, Mesh = mesh, Position = position });

    /// <summary>Billboard label with a constant screen size that draws over the terrain.</summary>
    private static Label3D CreateLabel(Vector3 position) => new()
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

using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Population;

/// <summary>
/// A village's local navigation (spec §85, ADR-046): a grid of the ground baked from its layout,
/// where huts, the building site, ruins, the shrine, the temple and water are blocked. A figure
/// walks straight where it can, and otherwise along an A* way round what stands in it, pulled
/// taut. Derived from the layout and the terrain, never saved; the same layout gives the same ways.
/// </summary>
public sealed class LocalNavGrid
{
    /// <summary>A hut's wall radius at scale 1, in metres, as drawn.</summary>
    public const float HutRadius = 2.1f;

    /// <summary>The shrine's plinth radius, in metres.</summary>
    public const float ShrineRadius = 2.8f;

    /// <summary>The temple's footprint radius, in metres.</summary>
    public const float TempleRadius = 3.4f;

    // How far, in metres, the grid reaches beyond the farthest hut or field.
    private const float Margin = 12f;

    private static readonly (int X, int Z, float Cost)[] Steps =
    [
        (1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f),
        (1, 1, 1.41421356f), (1, -1, 1.41421356f), (-1, 1, 1.41421356f), (-1, -1, 1.41421356f),
    ];

    private readonly Vector2 _origin;
    private readonly float _cell;
    private readonly int _columns;
    private readonly int _rows;
    private readonly bool[] _blocked;
    private readonly Dictionary<(Vector2 From, Vector2 To), Vector2[]?> _ways = [];

    private LocalNavGrid(Vector2 origin, float cell, int columns, int rows, bool[] blocked)
    {
        (_origin, _cell, _columns, _rows, _blocked) = (origin, cell, columns, rows, blocked);
        foreach (var closed in blocked)
        {
            Blocked += closed ? 1 : 0;
        }
    }

    /// <summary>The grid's cells.</summary>
    public int Cells => _blocked.Length;

    /// <summary>The cells nobody may walk through.</summary>
    public int Blocked { get; }

    /// <summary>The ways searched for, for the debug panel.</summary>
    public int Searches { get; private set; }

    /// <summary>The searched ways that go round something.</summary>
    public int Detours { get; private set; }

    /// <summary>Bakes a village's grid from its layout.</summary>
    /// <param name="site">The village's layout.</param>
    /// <param name="settlement">The village, for its temple.</param>
    /// <param name="terrain">Terrain, for water.</param>
    /// <param name="settings">Citizen settings: the cell size and the clearance.</param>
    /// <returns>The grid.</returns>
    public static LocalNavGrid Build(SettlementSite site, SettlementState settlement, TerrainField terrain, CitizenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        var centre = new Vector2(site.Center.X, site.Center.Z);
        var reach = site.HouseDiscRadius + HutRadius + Margin;
        foreach (var field in site.Fields)
        {
            reach = MathF.Max(reach, Vector2.Distance(centre, new Vector2(field.Position.X, field.Position.Z)) + Margin);
        }

        var cell = settings.NavCellSize;
        var side = Math.Max(2, (int)MathF.Ceiling(2f * reach / cell));
        var origin = centre - new Vector2(side * cell / 2f);
        var blocked = new bool[side * side];
        for (var z = 0; z < side; z++)
        {
            for (var x = 0; x < side; x++)
            {
                var (wx, wz) = (origin.X + ((x + 0.5f) * cell), origin.Y + ((z + 0.5f) * cell));
                blocked[(z * side) + x] = terrain.SampleHeight(wx, wz) < 0f || terrain.IsUnderFreshWater(wx, wz);
            }
        }

        // The obstacles are marked on a first grid over the same cells, which the finished grid then counts.
        var grid = new LocalNavGrid(origin, cell, side, side, blocked);
        var clearance = settings.NavClearance;
        foreach (var hut in site.Houses)
        {
            grid.Block(hut.Position, (HutRadius * hut.Scale) + clearance);
        }

        foreach (var ruin in site.Ruins)
        {
            grid.Block(ruin.Position, (HutRadius * ruin.Scale) + clearance);
        }

        if (site.Construction is { } construction)
        {
            grid.Block(construction.House.Position, (HutRadius * construction.House.Scale) + clearance);
        }

        grid.Block(site.Center, ShrineRadius + clearance);
        if (Simulation.Settlements.CivicModel.Has(settlement, BuildingKind.Temple))
        {
            grid.Block(CivicLayout.Temple(site, terrain, settlement.Id).Position, TempleRadius + clearance);
        }

        return new LocalNavGrid(origin, cell, side, side, blocked);
    }

    /// <summary>
    /// The way from one point to another round what stands between them: <c>null</c> when the
    /// straight line is clear or no way is found, else the points to pass, ending at the goal.
    /// </summary>
    /// <param name="from">Where the figure sets off.</param>
    /// <param name="to">Where it goes.</param>
    /// <returns>The waypoints after the start, or <c>null</c> to walk straight.</returns>
    public Vector2[]? Way(Vector2 from, Vector2 to)
    {
        var key = (from, to);
        if (_ways.TryGetValue(key, out var known))
        {
            return known;
        }

        Vector2[]? way = null;
        if (!Clear(from, to, from, to))
        {
            Searches++;
            way = Search(from, to);
            Detours += way is null ? 0 : 1;
        }

        _ways[key] = way;
        return way;
    }

    /// <summary>Whether a point lies in a blocked cell; off the grid counts as open.</summary>
    /// <param name="point">The point.</param>
    /// <returns><c>true</c> when blocked.</returns>
    public bool IsBlocked(Vector2 point) => CellOf(point) is var index && index >= 0 && _blocked[index];

    /// <summary>Marks the cells within a radius of a point blocked.</summary>
    private void Block(Vector3 point, float radius)
    {
        var (x0, z0) = ((int)MathF.Floor((point.X - radius - _origin.X) / _cell), (int)MathF.Floor((point.Z - radius - _origin.Y) / _cell));
        var (x1, z1) = ((int)MathF.Ceiling((point.X + radius - _origin.X) / _cell), (int)MathF.Ceiling((point.Z + radius - _origin.Y) / _cell));
        for (var z = Math.Max(0, z0); z <= Math.Min(_rows - 1, z1); z++)
        {
            for (var x = Math.Max(0, x0); x <= Math.Min(_columns - 1, x1); x++)
            {
                var (dx, dz) = (_origin.X + ((x + 0.5f) * _cell) - point.X, _origin.Y + ((z + 0.5f) * _cell) - point.Z);
                if ((dx * dx) + (dz * dz) <= radius * radius)
                {
                    _blocked[(z * _columns) + x] = true;
                }
            }
        }
    }

    /// <summary>A* over the cells from the nearest open cell to the start to the nearest open cell to the goal, pulled taut.</summary>
    private Vector2[]? Search(Vector2 from, Vector2 to)
    {
        if (Nearest(CellOf(from)) is not { } start || Nearest(CellOf(to)) is not { } goal)
        {
            return null;
        }

        var cost = new Dictionary<int, float> { [start] = 0f };
        var came = new Dictionary<int, int>();
        var open = new PriorityQueue<int, float>();
        open.Enqueue(start, 0f);
        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal)
            {
                return Pull(from, to, Trace(came, goal));
            }

            var (cx, cz) = (current % _columns, current / _columns);
            foreach (var (sx, sz, step) in Steps)
            {
                var (nx, nz) = (cx + sx, cz + sz);
                if (nx < 0 || nz < 0 || nx >= _columns || nz >= _rows || _blocked[(nz * _columns) + nx]
                    || (sx != 0 && sz != 0 && (_blocked[(cz * _columns) + nx] || _blocked[(nz * _columns) + cx])))
                {
                    continue;
                }

                var next = (nz * _columns) + nx;
                var reached = cost[current] + step;
                if (!cost.TryGetValue(next, out var before) || reached < before)
                {
                    (cost[next], came[next]) = (reached, current);
                    var (gx, gz) = (goal % _columns, goal / _columns);
                    var (ax, az) = (Math.Abs(gx - nx), Math.Abs(gz - nz));
                    open.Enqueue(next, reached + Math.Max(ax, az) + (0.41421356f * Math.Min(ax, az)));
                }
            }
        }

        return null;
    }

    /// <summary>The cells from the start to a cell, in order.</summary>
    private List<int> Trace(Dictionary<int, int> came, int end)
    {
        var cells = new List<int> { end };
        while (came.TryGetValue(cells[^1], out var previous))
        {
            cells.Add(previous);
        }

        cells.Reverse();
        return cells;
    }

    /// <summary>Pulls a way of cells taut: from each point, on to the farthest that can be seen.</summary>
    private Vector2[] Pull(Vector2 from, Vector2 to, List<int> cells)
    {
        var points = new List<Vector2>(cells.Count + 1);
        foreach (var cell in cells)
        {
            points.Add(Centre(cell));
        }

        points.Add(to);
        var way = new List<Vector2>();
        var at = from;
        var i = 0;
        while (i < points.Count - 1)
        {
            var next = i;
            for (var j = points.Count - 1; j > i; j--)
            {
                if (Clear(at, points[j], from, to))
                {
                    next = j;
                    break;
                }
            }

            next = Math.Max(next, i + 1);
            way.Add(points[next]);
            at = points[next];
            i = next;
        }

        return [.. way];
    }

    /// <summary>Whether a straight line crosses no blocked cell, but for those at its two ends.</summary>
    private bool Clear(Vector2 a, Vector2 b, Vector2 from, Vector2 to)
    {
        var length = Vector2.Distance(a, b);
        var steps = Math.Max(1, (int)MathF.Ceiling(length / (_cell * 0.5f)));
        for (var k = 0; k <= steps; k++)
        {
            var point = Vector2.Lerp(a, b, (float)k / steps);
            if (IsBlocked(point) && Vector2.Distance(point, from) > _cell * 1.5f && Vector2.Distance(point, to) > _cell * 1.5f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The open cell nearest a cell, searching outwards; the cell itself when open.</summary>
    private int? Nearest(int cell)
    {
        if (cell < 0)
        {
            return null;
        }

        var (cx, cz) = (cell % _columns, cell / _columns);
        for (var ring = 0; ring < 8; ring++)
        {
            for (var z = cz - ring; z <= cz + ring; z++)
            {
                for (var x = cx - ring; x <= cx + ring; x++)
                {
                    if ((Math.Abs(x - cx) == ring || Math.Abs(z - cz) == ring) && x >= 0 && z >= 0 && x < _columns && z < _rows && !_blocked[(z * _columns) + x])
                    {
                        return (z * _columns) + x;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>The cell a point lies in, or -1 off the grid.</summary>
    private int CellOf(Vector2 point)
    {
        var x = (int)MathF.Floor((point.X - _origin.X) / _cell);
        var z = (int)MathF.Floor((point.Y - _origin.Y) / _cell);
        return x < 0 || z < 0 || x >= _columns || z >= _rows ? -1 : (z * _columns) + x;
    }

    /// <summary>The centre of a cell.</summary>
    private Vector2 Centre(int cell) => _origin + new Vector2(((cell % _columns) + 0.5f) * _cell, ((cell / _columns) + 0.5f) * _cell);
}

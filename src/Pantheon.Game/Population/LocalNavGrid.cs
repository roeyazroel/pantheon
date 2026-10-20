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

    // The grid's reach is rounded up to a multiple of this, in metres, so it keeps its frame (and its
    // terrain can be reused) while a village grows a few houses at a time (ADR-056).
    private const float ReachStep = 16f;

    // Half a bridge deck's width, in metres: walkers cross water on it (ADR-057).
    private const float DeckHalfWidth = 1.3f;

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

    // Which connected area each open cell lies in, so a way between two areas is known to be missing
    // without searching every cell of one (found by measuring: such searches cost most of a frame).
    private readonly int[] _area;

    // Reused by every search: the cost to reach a cell, where it was reached from, and which search
    // last reached and closed it.
    private float[]? _cost;
    private int[]? _came;
    private int[]? _reachedBy;
    private int[]? _closedBy;
    private int _search;

    private LocalNavGrid(Vector2 origin, float cell, int columns, int rows, bool[] blocked)
    {
        (_origin, _cell, _columns, _rows, _blocked) = (origin, cell, columns, rows, blocked);
        foreach (var closed in blocked)
        {
            Blocked += closed ? 1 : 0;
        }

        _area = Areas();
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
    /// <param name="land">
    /// The land under the grid from the last bake, reused when the grid keeps its frame, and updated
    /// (ADR-056: sampling the terrain under every cell cost most of a rebuild).
    /// </param>
    /// <param name="bridges">Bridges over the water, end to end, which walkers may cross (ADR-057).</param>
    /// <returns>The grid.</returns>
    public static LocalNavGrid Build(SettlementSite site, SettlementState settlement, TerrainField terrain, CitizenSettings settings, LocalNavLand? land = null, IReadOnlyList<(Vector3 From, Vector3 To)>? bridges = null)
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

        reach = MathF.Ceiling(reach / ReachStep) * ReachStep;
        var cell = settings.NavCellSize;
        var side = Math.Max(2, (int)MathF.Ceiling(2f * reach / cell));
        var origin = centre - new Vector2(side * cell / 2f);
        if (land is not null && !land.Fits(terrain, origin, cell, side))
        {
            land.Sample(terrain, origin, cell, side);
        }

        var blocked = land is { } kept ? (bool[])kept.Water.Clone() : LocalNavLand.SampleWater(terrain, origin, cell, side);
        foreach (var (from, to) in bridges ?? [])
        {
            Open(blocked, origin, cell, side, new Vector2(from.X, from.Z), new Vector2(to.X, to.Z), DeckHalfWidth);
        }

        var clearance = settings.NavClearance;
        foreach (var hut in site.Houses)
        {
            Block(blocked, origin, cell, side, hut.Position, (HutRadius * hut.Scale) + clearance);
        }

        foreach (var ruin in site.Ruins)
        {
            Block(blocked, origin, cell, side, ruin.Position, (HutRadius * ruin.Scale) + clearance);
        }

        if (site.Construction is { } construction)
        {
            Block(blocked, origin, cell, side, construction.House.Position, (HutRadius * construction.House.Scale) + clearance);
        }

        Block(blocked, origin, cell, side, site.Center, ShrineRadius + clearance);
        if (Simulation.Settlements.CivicModel.Has(settlement, BuildingKind.Temple))
        {
            Block(blocked, origin, cell, side, CivicLayout.Temple(site, terrain, settlement.Id).Position, TempleRadius + clearance);
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

    /// <summary>Opens the cells within a half-width of a segment, as a bridge deck does over water.</summary>
    private static void Open(bool[] blocked, Vector2 origin, float cell, int side, Vector2 from, Vector2 to, float halfWidth)
    {
        var (low, high) = (Vector2.Min(from, to) - new Vector2(halfWidth), Vector2.Max(from, to) + new Vector2(halfWidth));
        var (x0, z0) = (Math.Max(0, (int)MathF.Floor((low.X - origin.X) / cell)), Math.Max(0, (int)MathF.Floor((low.Y - origin.Y) / cell)));
        var (x1, z1) = (Math.Min(side - 1, (int)MathF.Ceiling((high.X - origin.X) / cell)), Math.Min(side - 1, (int)MathF.Ceiling((high.Y - origin.Y) / cell)));
        var along = to - from;
        var length = along.LengthSquared();
        for (var z = z0; z <= z1; z++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var point = origin + new Vector2((x + 0.5f) * cell, (z + 0.5f) * cell);
                var t = length > 0f ? Math.Clamp(Vector2.Dot(point - from, along) / length, 0f, 1f) : 0f;
                if (Vector2.DistanceSquared(point, from + (along * t)) <= halfWidth * halfWidth)
                {
                    blocked[(z * side) + x] = false;
                }
            }
        }
    }

    /// <summary>Marks the cells within a radius of a point blocked.</summary>
    private static void Block(bool[] blocked, Vector2 origin, float cell, int side, Vector3 point, float radius)
    {
        var (x0, z0) = ((int)MathF.Floor((point.X - radius - origin.X) / cell), (int)MathF.Floor((point.Z - radius - origin.Y) / cell));
        var (x1, z1) = ((int)MathF.Ceiling((point.X + radius - origin.X) / cell), (int)MathF.Ceiling((point.Z + radius - origin.Y) / cell));
        for (var z = Math.Max(0, z0); z <= Math.Min(side - 1, z1); z++)
        {
            for (var x = Math.Max(0, x0); x <= Math.Min(side - 1, x1); x++)
            {
                var (dx, dz) = (origin.X + ((x + 0.5f) * cell) - point.X, origin.Y + ((z + 0.5f) * cell) - point.Z);
                if ((dx * dx) + (dz * dz) <= radius * radius)
                {
                    blocked[(z * side) + x] = true;
                }
            }
        }
    }

    /// <summary>
    /// A* over the cells from the nearest open cell to the start to the nearest open cell to the goal,
    /// pulled taut; none when they lie in different areas.
    /// </summary>
    private Vector2[]? Search(Vector2 from, Vector2 to)
    {
        if (Nearest(CellOf(from)) is not { } start || Nearest(CellOf(to)) is not { } goal || _area[start] != _area[goal])
        {
            return null;
        }

        var cells = _blocked.Length;
        var (cost, came, reachedBy, closedBy) = (_cost ??= new float[cells], _came ??= new int[cells], _reachedBy ??= new int[cells], _closedBy ??= new int[cells]);
        var search = ++_search;
        (cost[start], came[start], reachedBy[start]) = (0f, -1, search);
        var open = new PriorityQueue<int, float>();
        open.Enqueue(start, 0f);
        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal)
            {
                return Pull(from, to, Trace(goal));
            }

            if (closedBy[current] == search)
            {
                continue;
            }

            closedBy[current] = search;
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
                if (reachedBy[next] != search || reached < cost[next])
                {
                    (cost[next], came[next], reachedBy[next]) = (reached, current, search);
                    var (gx, gz) = (goal % _columns, goal / _columns);
                    var (ax, az) = (Math.Abs(gx - nx), Math.Abs(gz - nz));
                    open.Enqueue(next, reached + Math.Max(ax, az) + (0.41421356f * Math.Min(ax, az)));
                }
            }
        }

        return null;
    }

    /// <summary>The cells from the start to a cell, in order.</summary>
    private List<int> Trace(int end)
    {
        var cells = new List<int> { end };
        while (_came![cells[^1]] is var previous && previous >= 0)
        {
            cells.Add(previous);
        }

        cells.Reverse();
        return cells;
    }

    /// <summary>
    /// Numbers the connected areas of open cells, side by side: a figure steps diagonally only past two
    /// open sides, so these are the areas it can walk within. Blocked cells are in none (-1).
    /// </summary>
    private int[] Areas()
    {
        var area = new int[_blocked.Length];
        Array.Fill(area, -1);
        var next = 0;
        var queue = new Queue<int>();
        for (var seed = 0; seed < area.Length; seed++)
        {
            if (_blocked[seed] || area[seed] >= 0)
            {
                continue;
            }

            area[seed] = next;
            queue.Enqueue(seed);
            while (queue.TryDequeue(out var cell))
            {
                var (x, z) = (cell % _columns, cell / _columns);
                Spread(x - 1, z);
                Spread(x + 1, z);
                Spread(x, z - 1);
                Spread(x, z + 1);
            }

            next++;
        }

        return area;

        void Spread(int x, int z)
        {
            if (x >= 0 && z >= 0 && x < _columns && z < _rows && (z * _columns) + x is var cell && !_blocked[cell] && area[cell] < 0)
            {
                area[cell] = next;
                queue.Enqueue(cell);
            }
        }
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

using System.Numerics;
using Pantheon.Game.Camera;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Edge scrolling maps the mouse position near the window border to pan axes.</summary>
public sealed class EdgeScrollTests
{
    private const float Margin = 12f;
    private static readonly Vector2 Viewport = new(1600f, 900f);

    [Fact]
    public void EdgeScroll_IsZeroAwayFromTheBorder()
    {
        Assert.Equal(Vector2.Zero, EdgeScroll.ComputeAxes(new Vector2(800f, 450f), Viewport, Margin));
    }

    [Theory]
    [InlineData(0f, 450f, -1f, 0f)]
    [InlineData(1600f, 450f, 1f, 0f)]
    [InlineData(800f, 0f, 0f, 1f)]
    [InlineData(800f, 900f, 0f, -1f)]
    [InlineData(6f, 450f, -0.5f, 0f)]
    [InlineData(0f, 0f, -1f, 1f)]
    public void EdgeScroll_RampsUpTowardsTheBorder(float x, float y, float expectedX, float expectedY)
    {
        var axes = EdgeScroll.ComputeAxes(new Vector2(x, y), Viewport, Margin);

        Assert.Equal(expectedX, axes.X, 4);
        Assert.Equal(expectedY, axes.Y, 4);
    }

    [Theory]
    [InlineData(-5f, 450f)]
    [InlineData(800f, 950f)]
    public void EdgeScroll_IsZeroOutsideTheViewport(float x, float y)
    {
        Assert.Equal(Vector2.Zero, EdgeScroll.ComputeAxes(new Vector2(x, y), Viewport, Margin));
    }

    [Fact]
    public void EdgeScroll_IsZeroWithoutAUsableMargin()
    {
        Assert.Equal(Vector2.Zero, EdgeScroll.ComputeAxes(Vector2.Zero, Viewport, 0f));
        Assert.Equal(Vector2.Zero, EdgeScroll.ComputeAxes(Vector2.Zero, new Vector2(20f, 20f), Margin));
    }
}

using Pantheon.Game.Presentation;
using Xunit;

namespace Pantheon.Tests.Worlds;

/// <summary>Floating labels give way to more important ones (spec §84, ADR-027).</summary>
public sealed class LabelDeclutterTests
{
    [Fact]
    public void LabelsApart_AreAllShownInFull() =>
        Assert.Equal(
            [LabelFit.Full, LabelFit.Full],
            LabelDeclutter.Fit([Label(100f, 100f, 50f), Label(400f, 100f, 60f)]));

    [Fact]
    public void ALesserLabel_GivesWay_ToItsName_AndThenAltogether()
    {
        // The big village's label fills 240 to 300 down the screen; the small one below it reaches up into it in full, not by name.
        var big = Label(300f, 300f, 500f);
        var near = Label(300f, 340f, 100f) with { FullHeight = 90f, ShortHeight = 30f };
        var onTop = Label(300f, 300f, 80f);

        Assert.Equal([LabelFit.Full, LabelFit.Short, LabelFit.Hidden], LabelDeclutter.Fit([big, near, onTop]));
    }

    [Fact]
    public void Priority_DecidesWhoKeepsThePlace_NotOrder()
    {
        var small = Label(300f, 300f, 20f);
        var big = Label(310f, 300f, 900f);

        Assert.Equal([LabelFit.Hidden, LabelFit.Full], LabelDeclutter.Fit([small, big]));
    }

    [Fact]
    public void EqualPriorities_KeepTheirOrder() =>
        Assert.Equal([LabelFit.Full, LabelFit.Hidden], LabelDeclutter.Fit([Label(0f, 0f, 1f), Label(5f, 5f, 1f)]));

    [Fact]
    public void LabelsTouchingAtAnEdge_DoNotOverlap() =>
        Assert.Equal([LabelFit.Full, LabelFit.Full], LabelDeclutter.Fit([Label(100f, 100f, 2f), Label(300f, 100f, 1f)]));

    /// <summary>A label 200 by 60 pixels in full and 100 by 20 by name.</summary>
    private static ScreenLabel Label(float x, float y, float priority) => new(x, y, 200f, 60f, 100f, 20f, priority);
}

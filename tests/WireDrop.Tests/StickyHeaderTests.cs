using System;
using System.Collections.Generic;
using WireDrop.UI;
using Xunit;

public class StickyHeaderTests
{
    const int RowH = 24, HeadH = 20;

    // A list shaped like the real one: heading, some rows, heading, some rows.
    // index: 0=head 1..3=rows 4=head 5..7=rows
    static readonly bool[] Kinds = { true, false, false, false, true, false, false, false };

    static int[] Offsets()
    {
        var o = new int[Kinds.Length + 1];
        for (int i = 0; i < Kinds.Length; i++) o[i + 1] = o[i] + (Kinds[i] ? HeadH : RowH);
        return o;
    }

    static StickyHeader At(int scroll) =>
        StickyHeader.Resolve(i => Kinds[i], Offsets(), Kinds.Length, scroll, HeadH);

    [Fact]
    public void FirstHeadingIsPinnedFromTheStart()
    {
        var s = At(0);
        Assert.True(s.Visible);
        Assert.Equal(0, s.Index);
        Assert.Equal(0, s.Offset);
    }

    [Fact]
    public void StaysPinnedWhileScrollingInsideItsBand()
    {
        var s = At(HeadH + RowH);   // inside the first band
        Assert.Equal(0, s.Index);
        Assert.Equal(0, s.Offset);
    }

    [Fact]
    public void OnlyOneIsEverPinned()
    {
        // Deep into the second band, the first heading must be gone, not stacked.
        var o = Offsets();
        var s = At(o[6]);
        Assert.Equal(4, s.Index);
        Assert.Equal(0, s.Offset);
    }

    [Fact]
    public void NextHeadingPushesTheCurrentOneOut()
    {
        var o = Offsets();
        // Scrolled so the second heading is 6px below the top: less than a header tall.
        var s = At(o[4] - 6);
        Assert.Equal(0, s.Index);
        Assert.Equal(6 - HeadH, s.Offset);
        Assert.True(s.Offset < 0, "the outgoing heading must ride upward");
    }

    [Fact]
    public void PushIsExactlyCompleteWhenTheNextHeadingReachesTheTop()
    {
        var o = Offsets();
        var justBefore = At(o[4] - 1);
        Assert.Equal(0, justBefore.Index);
        Assert.Equal(1 - HeadH, justBefore.Offset);   // fully pushed out

        var at = At(o[4]);
        Assert.Equal(4, at.Index);                     // handover
        Assert.Equal(0, at.Offset);
    }

    [Fact]
    public void NothingPinnedBeforeTheFirstHeading()
    {
        var kinds = new[] { false, false, true, false };
        var o = new int[kinds.Length + 1];
        for (int i = 0; i < kinds.Length; i++) o[i + 1] = o[i] + (kinds[i] ? HeadH : RowH);
        Assert.False(StickyHeader.Resolve(i => kinds[i], o, kinds.Length, 0, HeadH).Visible);
    }

    [Fact]
    public void EmptyListPinsNothing() =>
        Assert.False(StickyHeader.Resolve(i => false, new[] { 0 }, 0, 0, HeadH).Visible);

    [Fact]
    public void AListWithNoHeadingsPinsNothing()
    {
        var kinds = new[] { false, false, false };
        var o = new[] { 0, RowH, RowH * 2, RowH * 3 };
        Assert.False(StickyHeader.Resolve(i => kinds[i], o, kinds.Length, RowH, HeadH).Visible);
    }
}

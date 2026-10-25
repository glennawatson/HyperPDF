// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks where an annotation goes while it is dragged or resized.</summary>
public sealed class AnnotationEditTests
{
    /// <summary>The left of the annotation's bounds.</summary>
    private const float Left = 100F;

    /// <summary>The top of the annotation's bounds.</summary>
    private const float Top = 200F;

    /// <summary>The width of the annotation's bounds.</summary>
    private const float Width = 50F;

    /// <summary>The height of the annotation's bounds.</summary>
    private const float Height = 40F;

    /// <summary>How far inside the bounds the annotation is grabbed.</summary>
    private const float Inset = 5F;

    /// <summary>How far the pointer moves in each direction.</summary>
    private const float Drag = 10F;

    /// <summary>Dragging the annotation moves it by how far the pointer moved from where it grabbed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingShiftsTheBounds()
    {
        var bounds = new PageRect(Left, Top, Width, Height);
        var grabbed = new PagePoint(Left + Inset, Top + Inset);
        var pointer = new PagePoint(grabbed.X + Drag, grabbed.Y - Drag);

        var moved = AnnotationEdit.GetNextBounds(bounds, false, grabbed, pointer);

        await Assert.That(moved.Left).IsEqualTo(Left + Drag);
        await Assert.That(moved.Top).IsEqualTo(Top - Drag);
        await Assert.That(moved.Width).IsEqualTo(Width);
        await Assert.That(moved.Height).IsEqualTo(Height);
    }

    /// <summary>Resizing spans from the anchored corner to the pointer, whichever way it is dragged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResizingSpansAnchorToPointer()
    {
        var bounds = new PageRect(Left, Top, Width, Height);
        var anchor = new PagePoint(Left, Top);
        var pointer = new PagePoint(Left - Drag, Top + Height);

        var resized = AnnotationEdit.GetNextBounds(bounds, true, anchor, pointer);

        await Assert.That(resized.Left).IsEqualTo(Left - Drag);
        await Assert.That(resized.Top).IsEqualTo(Top);
        await Assert.That(resized.Width).IsEqualTo(Drag);
        await Assert.That(resized.Height).IsEqualTo(Height);
    }
}

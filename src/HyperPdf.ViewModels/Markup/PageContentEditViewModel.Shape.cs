// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The part of Edit mode that changes the shape and paint of the picked object: resize handles, turning, flipping and colours.</summary>
public sealed partial class PageContentEditViewModel
{
    /// <summary>What is said once an object is resized.</summary>
    private const string ResizedText = "Resized.";

    /// <summary>A quarter turn in degrees.</summary>
    private const float QuarterTurn = 90F;

    /// <summary>Half of a length.</summary>
    private const float HalfOf = 0.5F;

    /// <summary>How much larger a step of making an object larger is.</summary>
    private const float GrowFactor = 1.1F;

    /// <summary>The degrees in a half turn, for turning degrees into radians.</summary>
    private const float HalfTurn = 180F;

    /// <summary>The handle being dragged, or <see cref="PageObjectHandle.None"/>.</summary>
    private PageObjectHandle _resizeHandle;

    /// <summary>The box of the picked object when the resize began.</summary>
    private PageRect _resizeFrom;

    /// <summary>Gets how the picked object is painted, or the default when nothing is picked.</summary>
    [Reactive]
    public partial PageObjectStyle Style { get; private set; }

    /// <summary>Gets the box the picked object will have when a resize ends, so its outline follows the pointer; empty when no resize is going.</summary>
    [Reactive]
    public partial PageRect ResizeBounds { get; private set; }

    /// <summary>Gets a value indicating whether a handle is being dragged.</summary>
    public bool IsResizing => _resizeHandle != PageObjectHandle.None;

    /// <summary>Starts resizing the picked object from a handle.</summary>
    /// <param name="page">The page pressed.</param>
    /// <param name="point">The press in page space.</param>
    /// <param name="reach">How far from a handle's centre still counts as on it, in points.</param>
    /// <returns><see langword="true"/> when a handle was under the point and a resize began.</returns>
    public bool BeginResize(int page, PagePoint point, float reach)
    {
        if (!IsEditing || TextEdit is not null || Selected is not { } picked || picked.Page != page)
        {
            return false;
        }

        // Several picked objects have one box around them, and its handles resize them all together.
        var box = Group.Length > 0 ? GroupBounds() : picked.Object.Bounds;
        var handle = PageObjectHandles.HitTest(box, point, reach);
        if (handle == PageObjectHandle.None)
        {
            return false;
        }

        _resizeHandle = handle;
        _resizeFrom = box;
        ResizeBounds = _resizeFrom;
        MarksVersion++;
        return true;
    }

    /// <summary>Moves the outline of the object being resized with the pointer.</summary>
    /// <param name="point">The pointer in page space.</param>
    /// <param name="keepShape">Whether the box keeps its proportions.</param>
    public void ResizeTo(PagePoint point, bool keepShape)
    {
        if (!IsResizing)
        {
            return;
        }

        ResizeBounds = PageObjectHandles.Drag(_resizeFrom, _resizeHandle, point, keepShape);
        MarksVersion++;
    }

    /// <summary>Ends a resize and writes the new size to the page.</summary>
    /// <returns><see langword="true"/> when a resize was going.</returns>
    public bool EndResize()
    {
        if (!IsResizing)
        {
            return false;
        }

        var from = _resizeFrom;
        var to = ResizeBounds;
        _resizeHandle = PageObjectHandle.None;
        ResizeBounds = default;
        MarksVersion++;
        if (to != from && CanGroup(out var group))
        {
            ShapePicks(group, PageObjectHandles.Fit(from, to), UiText.Get(ResizedText));
        }
        else if (to != from && Selected is { } picked)
        {
            var change = PageObjectHandles.Fit(from, to);
            Track(Edit(picked, _ => Editor!.Transform(picked.Page, picked.Object.Index, change), to, UiText.Get(ResizedText)));
        }

        return true;
    }

    /// <summary>Turns the picked object about the middle of its box.</summary>
    /// <param name="degrees">How far to turn, clockwise as seen on the page.</param>
    /// <returns><see langword="true"/> when an object is picked, so the key that asked is used.</returns>
    public bool RotateSelected(float degrees)
    {
        if (CanGroup(out var group))
        {
            ShapePicks(group, Matrix3x2.CreateRotation(degrees * MathF.PI / HalfTurn, MiddleOf(PageObjectLayout.Union(BoxesOf(group)))), UiText.Get("Turned."));
            return true;
        }

        if (!CanShape(out var picked))
        {
            return false;
        }

        var middle = MiddleOf(picked.Object.Bounds);
        var change = Matrix3x2.CreateRotation(degrees * MathF.PI / HalfTurn, middle);
        Shape(picked, change, UiText.Get("Turned."));
        return true;
    }

    /// <summary>Flips the picked object.</summary>
    /// <param name="across">True to flip from left to right, false to flip from top to bottom.</param>
    /// <returns><see langword="true"/> when an object is picked.</returns>
    public bool FlipSelected(bool across)
    {
        if (CanGroup(out var group))
        {
            ShapePicks(group, Matrix3x2.CreateScale(across ? -1F : 1F, across ? 1F : -1F, MiddleOf(PageObjectLayout.Union(BoxesOf(group)))), UiText.Get("Flipped."));
            return true;
        }

        if (!CanShape(out var picked))
        {
            return false;
        }

        var middle = MiddleOf(picked.Object.Bounds);
        var change = Matrix3x2.CreateScale(across ? -1F : 1F, across ? 1F : -1F, middle);
        Shape(picked, change, UiText.Get("Flipped."));
        return true;
    }

    /// <summary>Scales the picked object, keeping its top left corner where it is.</summary>
    /// <param name="factor">The scale, 1 for none.</param>
    /// <returns><see langword="true"/> when an object is picked.</returns>
    public bool ScaleSelected(float factor)
    {
        if (CanGroup(out var group))
        {
            var around = PageObjectLayout.Union(BoxesOf(group));
            ShapePicks(group, Matrix3x2.CreateScale(factor, factor, new(around.Left, around.Top)), UiText.Get(ResizedText));
            return true;
        }

        if (!CanShape(out var picked))
        {
            return false;
        }

        var bounds = picked.Object.Bounds;
        var change = Matrix3x2.CreateScale(factor, factor, new(bounds.Left, bounds.Top));
        Shape(picked, change, UiText.Get(ResizedText));
        return true;
    }

    /// <summary>Changes the fill colour of the picked object.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when an object is picked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetFill(uint rgb) => Paint(new(true, rgb, false, 0), UiText.Get("Changed the fill colour."));

    /// <summary>Changes the stroke colour of the picked object.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when an object is picked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetStroke(uint rgb) => Paint(new(false, 0, true, rgb), UiText.Get("Changed the line colour."));

    /// <summary>Gets the middle of a box.</summary>
    /// <param name="bounds">The box.</param>
    /// <returns>The middle in page space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 MiddleOf(PageRect bounds) => new(bounds.Left + (bounds.Width * HalfOf), bounds.Top + (bounds.Height * HalfOf));

    /// <summary>Changes how the picked objects are drawn besides their colours.</summary>
    /// <param name="look">What to change.</param>
    /// <param name="done">What to say once it is made.</param>
    /// <returns><see langword="true"/> when an object is picked.</returns>
    private bool Look(PageObjectLook look, string done)
    {
        if (CanGroup(out var group))
        {
            LookPicks(group, look, done);
            return true;
        }

        if (!CanShape(out var picked))
        {
            return false;
        }

        Track(Edit(picked, _ => Editor!.SetLookMany(picked.Page, [picked.Object.Index], look), picked.Object.Bounds, done));
        return true;
    }

    /// <summary>Turns the picked object a quarter turn to the right.</summary>
    [ReactiveCommand]
    private void TurnRight() => _ = RotateSelected(QuarterTurn);

    /// <summary>Turns the picked object a quarter turn to the left.</summary>
    [ReactiveCommand]
    private void TurnLeft() => _ = RotateSelected(-QuarterTurn);

    /// <summary>Changes the fill colour of the picked object from a menu.</summary>
    /// <param name="rgb">The colour as six hexadecimal digits.</param>
    [ReactiveCommand]
    private void SetPickedFill(string rgb)
    {
        if (uint.TryParse(rgb, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var colour))
        {
            _ = SetFill(colour);
        }
    }

    /// <summary>Changes the line colour of the picked object from a menu.</summary>
    /// <param name="rgb">The colour as six hexadecimal digits.</param>
    [ReactiveCommand]
    private void SetPickedLine(string rgb)
    {
        if (uint.TryParse(rgb, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var colour))
        {
            _ = SetStroke(colour);
        }
    }

    /// <summary>Makes the picked objects more or less see-through from a menu.</summary>
    /// <param name="percent">How solid the objects are, as a whole number of percent.</param>
    [ReactiveCommand]
    private void SetPickedOpacity(string percent)
    {
        if (int.TryParse(percent, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= WholePercent)
        {
            _ = Look(PageObjectLook.OfOpacity((float)value / WholePercent), UiText.Get("Changed how solid it is."));
        }
    }

    /// <summary>Changes the dashes of the lines of the picked objects from a menu.</summary>
    /// <param name="name">The pattern: Solid, Dashed, Dotted or DashDot.</param>
    [ReactiveCommand]
    private void SetPickedDash(string name)
    {
        var dash = name switch
        {
            nameof(PageLineDash.Dashed) => PageLineDash.Dashed,
            nameof(PageLineDash.Dotted) => PageLineDash.Dotted,
            nameof(PageLineDash.DashDot) => PageLineDash.DashDot,
            _ => PageLineDash.Solid,
        };
        _lineDash = dash;
        _ = Look(PageObjectLook.OfLine(_lineDash, _lineCap, _lineJoin), UiText.Get("Changed the dashes."));
    }

    /// <summary>Changes the ends of the lines of the picked objects from a menu.</summary>
    /// <param name="name">The ends: Butt, Round or Square.</param>
    [ReactiveCommand]
    private void SetPickedCap(string name)
    {
        _lineCap = name switch
        {
            nameof(PageLineCap.Round) => PageLineCap.Round,
            nameof(PageLineCap.Square) => PageLineCap.Square,
            _ => PageLineCap.Butt,
        };
        _ = Look(PageObjectLook.OfLine(_lineDash, _lineCap, _lineJoin), UiText.Get("Changed the line ends."));
    }

    /// <summary>Changes the corners of the lines of the picked objects from a menu.</summary>
    /// <param name="name">The corners: Miter, Round or Bevel.</param>
    [ReactiveCommand]
    private void SetPickedJoin(string name)
    {
        _lineJoin = name switch
        {
            nameof(PageLineJoin.Round) => PageLineJoin.Round,
            nameof(PageLineJoin.Bevel) => PageLineJoin.Bevel,
            _ => PageLineJoin.Miter,
        };
        _ = Look(PageObjectLook.OfLine(_lineDash, _lineCap, _lineJoin), UiText.Get("Changed the line corners."));
    }

    /// <summary>Makes the picked object a tenth larger, keeping its top left corner where it is. For text this is a larger size.</summary>
    [ReactiveCommand]
    private void GrowPicked() => _ = ScaleSelected(GrowFactor);

    /// <summary>Makes the picked object a tenth smaller, keeping its top left corner where it is. For text this is a smaller size.</summary>
    [ReactiveCommand]
    private void ShrinkPicked() => _ = ScaleSelected(1F / GrowFactor);

    /// <summary>Flips the picked object from left to right about the middle of its box.</summary>
    [ReactiveCommand]
    private void FlipAcross() => _ = FlipSelected(across: true);

    /// <summary>Flips the picked object from top to bottom about the middle of its box.</summary>
    [ReactiveCommand]
    private void FlipDown() => _ = FlipSelected(across: false);

    /// <summary>Reads how the picked object is painted, so the properties bar shows it.</summary>
    /// <returns>A cold signal that ends when the style is read.</returns>
    private IObservable<RxVoid> LoadStyle()
    {
        if (Selected is not { } picked || Editor is not { } editor)
        {
            Style = default;
            return Signal.Return(RxVoid.Default);
        }

        return editor.ReadStyle(picked.Page, picked.Object.Index)
            .ThrowWhenCancelled(_lifetime)
            .SubscribeOn(TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(style => StyleRead(picked, style))
            .Catch<RxVoid, Exception>(StyleFailed);
    }

    /// <summary>Shows the style that was read, unless the pick moved on meanwhile.</summary>
    /// <param name="picked">The object the style was read for.</param>
    /// <param name="style">How the object is painted.</param>
    /// <returns>The completion value.</returns>
    private RxVoid StyleRead(PageObjectPick picked, PageObjectStyle style)
    {
        if (Selected is { } now && now.Page == picked.Page && now.Object.Index == picked.Object.Index)
        {
            Style = style;
            MarksVersion++;
        }

        return RxVoid.Default;
    }

    /// <summary>Clears the style when it could not be read, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends for an expected failure, or fails with the same error.</returns>
    private IObservable<RxVoid> StyleFailed(Exception error)
    {
        if (error is OperationCanceledException && _lifetime.IsCancellationRequested)
        {
            return Signal.Return(RxVoid.Default);
        }

        if (error is not (InvalidDataException or IOException or InvalidOperationException or ArgumentOutOfRangeException))
        {
            return Signal.Throw<RxVoid>(error);
        }

        Style = default;
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Tells whether an object is picked and Edit mode lets it be reshaped.</summary>
    /// <param name="picked">Receives the picked object.</param>
    /// <returns><see langword="true"/> when it can be reshaped.</returns>
    private bool CanShape(out PageObjectPick picked)
    {
        picked = Selected!;
        return IsEditing && TextEdit is null && Selected is not null && Group.Length == 0;
    }

    /// <summary>Writes a change of shape to the page.</summary>
    /// <param name="picked">The object.</param>
    /// <param name="change">The change in page space.</param>
    /// <param name="done">What to say once it is made.</param>
    private void Shape(PageObjectPick picked, Matrix3x2 change, string done)
    {
        var expected = PageObjectHandles.Carry(picked.Object.Bounds, change);
        Track(Edit(picked, _ => Editor!.Transform(picked.Page, picked.Object.Index, change), expected, done));
    }

    /// <summary>Writes a change of colour to the page.</summary>
    /// <param name="colors">What to change.</param>
    /// <param name="done">What to say once it is made.</param>
    /// <returns><see langword="true"/> when an object is picked.</returns>
    private bool Paint(PageObjectColors colors, string done)
    {
        if (CanGroup(out var group))
        {
            PaintPicks(group, colors, done);
            return true;
        }

        if (!CanShape(out var picked))
        {
            return false;
        }

        Track(Edit(picked, _ => Editor!.SetColors(picked.Page, picked.Object.Index, colors), picked.Object.Bounds, done));
        return true;
    }
}

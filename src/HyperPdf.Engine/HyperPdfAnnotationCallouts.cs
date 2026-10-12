// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Text.Unicode;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationCallouts annotation operations.</summary>
internal static class HyperPdfAnnotationCallouts
{
    /// <summary>The width of a callout's frame and leader line.</summary>
    internal const float CalloutLine = 1;

    /// <summary>The space between a callout's text and its frame.</summary>
    internal const float CalloutPadding = 4;

    /// <summary>How far a callout's text sits inside its frame's edge.</summary>
    internal const float CalloutInset = CalloutPadding + CalloutLine;

    /// <summary>The bytes the default appearance and the anchor take.</summary>
    internal const int EntryBytes = 128;

    /// <summary>The points of a leader line: the target, then where it meets the frame.</summary>
    internal const int LeaderPoints = 2;

    /// <summary>The largest colour channel value.</summary>
    internal const float ChannelMax = 255;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    internal const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    internal const int GreenShift = 8;

    /// <summary>One channel's bits.</summary>
    internal const uint ChannelMask = 0xFF;

    /// <summary>Writes text in a framed box with a line and arrow pointing at something on the page.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="target">The point the arrow points at.</param>
    /// <param name="location">The top-left corner of the text.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddCallout(HyperPdfAnnotations annotationState, int pageIndex, PagePoint target, PagePoint location, string text, float fontSize, uint color)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text) || fontSize <= 0)
        {
            return -1;
        }

        lock (annotationState.Gate)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page)
            {
                return -1;
            }

            var callout = PdfAnnotations.Create(annotationState.Store, KnownName.FreeText, default);
            callout.Set(KnownName.IT, PdfValue.FromName(annotationState.Names.Callout));
            callout.Set(KnownName.LE, PdfValue.FromName(KnownName.OpenArrow));
            return BuildCallout(annotationState, callout, text, new(fontSize, color), HyperPdfAnnotationReading.ToUser(page, target), HyperPdfAnnotationReading.ToUser(page, location))
                ? HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, callout, color, text, HyperPdfAnnotationKinds.GetCalloutSubject())
                : -1;
        }
    }

    /// <summary>Gets where a leader line leaves a frame: the middle of the side nearest the target.</summary>
    /// <param name="box">The frame.</param>
    /// <param name="target">The target.</param>
    /// <returns>The point on the frame.</returns>
    internal static Vector2 Attachment(PdfRectangle box, Vector2 target)
    {
        var middleX = (box.Left + box.Right) * HyperPdfAnnotationShapes.Half;
        var middleY = (box.Top + box.Bottom) * HyperPdfAnnotationShapes.Half;
        Span<Vector2> sides = [new(box.Left, middleY), new(box.Right, middleY), new(middleX, box.Top), new(middleX, box.Bottom)];
        var attach = sides[0];
        foreach (var side in sides)
        {
            if (Vector2.Distance(side, target) < Vector2.Distance(attach, target))
            {
                attach = side;
            }
        }

        return attach;
    }

    /// <summary>Lays a callout out again at a new text size, keeping its target and text corner.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="callout">The callout.</param>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The new size.</param>
    /// <param name="color">The colour.</param>
    internal static void RebuildCallout(HyperPdfAnnotations annotationState, PdfDictionary callout, string text, float fontSize, uint color)
    {
        var rectangle = PdfAnnotations.GetRectangle(callout);
        var target = new Vector2(rectangle.Left, rectangle.Bottom);
        var corner = TryReadAnchor(annotationState, callout, out var anchor) ? anchor : new(rectangle.Left, rectangle.Top);
        var path = default(PdfStrokeBuffer);
        try
        {
            if (PdfAnnotations.ReadPoints(callout, KnownName.CL, ref path) > 0)
            {
                target = path.Points[0];
            }
        }
        finally
        {
            path.Dispose();
        }

        _ = BuildCallout(annotationState, callout, text, new(fontSize, color), target, corner);
    }

    /// <summary>Draws a callout and records its rectangle, leader line, text, size, text corner and default appearance.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="callout">The callout.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">The text size and colour.</param>
    /// <param name="target">The point the arrow points at, in user space.</param>
    /// <param name="corner">The text's top-left corner, in user space.</param>
    /// <returns><see langword="false"/> when no line has a character the font can show.</returns>
    internal static bool BuildCallout(HyperPdfAnnotations annotationState, PdfDictionary callout, string text, CalloutText style, Vector2 target, Vector2 corner)
    {
        var origin = corner + new Vector2(CalloutInset, -CalloutInset);
        var ink = HyperPdfAnnotationText.MeasureLines(AppearanceFont.Helvetica, style.Size, text, origin);
        if (!ink.IsSet)
        {
            return false;
        }

        var box = ink.ToRectangle(CalloutPadding);
        var attach = Attachment(box, target);
        var (first, second) = PdfAppearances.OpenArrowHead(target, attach, HyperPdfAnnotationStrokes.ArrowHead(CalloutLine));
        var all = default(UserBounds);
        all.AddRectangle(box);
        all.Add(target);
        all.Add(first);
        all.Add(second);
        var rectangle = all.ToRectangle(CalloutLine);
        PdfAnnotations.SetRectangle(callout, rectangle);
        PdfAnnotations.SetPoints(callout, KnownName.CL, [target, attach]);
        WriteCalloutKeys(annotationState, callout, text, style, corner);
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            PdfAppearances.SetColors(ref builder, style.Color);
            HyperPdfAnnotationText.WriteLines(ref builder, new(AppearanceFont.Helvetica, style.Size), text, origin);
            PdfAppearances.SetRoundLine(ref builder, CalloutLine);
            builder.Rectangle(box.Left, box.Bottom, box.Width, box.Height);
            builder.Stroke();
            ReadOnlySpan<Vector2> leader = [attach, target, first, target, second];
            PdfAppearances.AddStrokes(ref builder, leader, [LeaderPoints, LeaderPoints + 1]);
            builder.Stroke();
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(
                annotationState.Store,
                callout,
                builder.ToFormXObject(
                    annotationState.Store,
                    rectangle,
                    HyperPdfAnnotationText.CreateFontResources(annotationState, AppearanceFont.Helvetica)));
        }
        finally
        {
            builder.Dispose();
        }

        return true;
    }

    /// <summary>Records a callout's text, size, text corner and default appearance.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="callout">The callout.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">The text size and colour.</param>
    /// <param name="corner">The text's top-left corner.</param>
    internal static void WriteCalloutKeys(HyperPdfAnnotations annotationState, PdfDictionary callout, string text, CalloutText style, Vector2 corner)
    {
        PdfAnnotations.SetText(callout, annotationState.Names.Text, text);
        PdfAnnotations.SetNumberText(callout, annotationState.Names.FontSize, style.Size);
        Span<byte> buffer = stackalloc byte[EntryBytes];
        var red = ((style.Color >> RedShift) & ChannelMask) / ChannelMax;
        var green = ((style.Color >> GreenShift) & ChannelMask) / ChannelMax;
        _ = Utf8.TryWrite(buffer, CultureInfo.InvariantCulture, $"/Helv {style.Size:0.###} Tf {red:0.###} {green:0.###} {((style.Color & ChannelMask) / ChannelMax):0.###} rg", out var written);
        callout.Set(KnownName.DA, PdfValue.FromString(buffer[..written].ToArray()));
        _ = Utf8.TryWrite(buffer, CultureInfo.InvariantCulture, $"/A [{corner.X:0.###} {corner.Y:0.###} ]", out written);
        callout.Set(annotationState.Names.Anchor, PdfValue.FromString(buffer[..written].ToArray()));
    }

    /// <summary>Reads a callout's text corner, kept as <c>/A [x y]</c> text the way the PDFium engine writes it.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="callout">The callout.</param>
    /// <param name="anchor">The corner.</param>
    /// <returns><see langword="true"/> when read.</returns>
    internal static bool TryReadAnchor(HyperPdfAnnotations annotationState, PdfDictionary callout, out Vector2 anchor)
    {
        anchor = default;
        var text = callout.GetStringBytes(annotationState.Names.Anchor);
        var open = text.IndexOf((byte)'[');
        if (open < 0)
        {
            return false;
        }

        var numbers = text[(open + 1)..].Trim(" ]"u8);
        var space = numbers.IndexOf((byte)' ');
        if (space < 0 || !PdfNumber.TryParseSingle(numbers[..space], out var x) || !PdfNumber.TryParseSingle(numbers[(space + 1)..].Trim((byte)' '), out var y))
        {
            return false;
        }

        anchor = new(x, y);
        return true;
    }

    /// <summary>Moves a callout's leader line and text corner with it.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation, which may not be a callout.</param>
    /// <param name="map">The move.</param>
    internal static void MoveCallout(HyperPdfAnnotations annotationState, PdfDictionary annotation, in RectangleMap map)
    {
        if (!HyperPdfAnnotationKinds.HasSubject(annotationState, annotation, HyperPdfAnnotationKinds.GetCalloutSubject()))
        {
            return;
        }

        var path = default(PdfStrokeBuffer);
        try
        {
            if (PdfAnnotations.ReadPoints(annotation, KnownName.CL, ref path) > 0)
            {
                map.Apply(path.Points);
                PdfAnnotations.SetPoints(annotation, KnownName.CL, path.Points);
            }
        }
        finally
        {
            path.Dispose();
        }

        if (!TryReadAnchor(annotationState, annotation, out var anchor))
        {
            return;
        }

        var moved = map.Apply(anchor);
        Span<byte> buffer = stackalloc byte[EntryBytes];
        _ = Utf8.TryWrite(buffer, CultureInfo.InvariantCulture, $"/A [{moved.X:0.###} {moved.Y:0.###} ]", out var written);
        annotation.Set(annotationState.Names.Anchor, PdfValue.FromString(buffer[..written].ToArray()));
    }
}

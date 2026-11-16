// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// Moves the clip that belongs to one object along with it. A clip made by <c>W n</c> just before an object, and shared with no other object, is
/// that object's own (a cropped picture, say), so moving the object without it would leave the object clipped to where it was and show nothing.
/// A clip that several objects share stays where it is, as the other objects still need it.
/// </summary>
internal static class PdfPageContentClipWriter
{
    /// <summary>How near to zero a matrix's skew may be for the matrix to keep a rectangle a rectangle.</summary>
    private const float SkewTolerance = 1e-6F;

    /// <summary>Writes the original content from the end of the last object to the start of this one, with the object's own clips moved with it.</summary>
    /// <param name="state">The owned parse or content state.</param>
    /// <param name="builder">The output.</param>
    /// <param name="item">The object about to be written.</param>
    /// <param name="index">The object's place among the page's objects.</param>
    /// <param name="position">The offset the content has been written to.</param>
    internal static void WriteGap(PdfPageContent state, ref PdfContentBuilder builder, PdfPageObject item, int index, int position)
    {
        var end = item.Source.Start;
        if (!item.IsTransformed || item.IsDeleted || item is PdfTextObject || item.ClipChain is null)
        {
            builder.WriteRaw(state.SourceBytes.AsSpan(position, end - position));
            return;
        }

        foreach (var node in FindOwnClips(state, item, index, position, end))
        {
            if (!TryGetMoved(node, item, out var matrix))
            {
                continue;
            }

            builder.WriteRaw(state.SourceBytes.AsSpan(position, node.Source.Start - position));
            WriteMoved(ref builder, node.Path, matrix);
            position = node.Source.End;
        }

        builder.WriteRaw(state.SourceBytes.AsSpan(position, end - position));
    }

    /// <summary>Finds the clips made between the last object and this one that no later object is clipped by.</summary>
    /// <param name="state">The owned parse or content state.</param>
    /// <param name="item">The object.</param>
    /// <param name="index">The object's place among the page's objects.</param>
    /// <param name="start">The offset the gap starts at.</param>
    /// <param name="end">The offset the gap ends at, where the object starts.</param>
    /// <returns>The clips, in the order they are in the content.</returns>
    private static List<ClipNode> FindOwnClips(PdfPageContent state, PdfPageObject item, int index, int start, int end)
    {
        var owned = new List<ClipNode>();
        for (var node = item.ClipChain; node is not null; node = node.Parent)
        {
            if (node.Source.Length > 0 && node.Source.Start >= start && node.Source.End <= end && !IsSharedLater(state, node, index))
            {
                owned.Insert(0, node);
            }
        }

        return owned;
    }

    /// <summary>Determines whether an object after this one is clipped by a clip too.</summary>
    /// <param name="state">The owned parse or content state.</param>
    /// <param name="clip">The clip.</param>
    /// <param name="index">The place of the object that was asked about.</param>
    /// <returns><see langword="true"/> when a later object is clipped by it.</returns>
    private static bool IsSharedLater(PdfPageContent state, ClipNode clip, int index)
    {
        for (var next = index + 1; next < state.ObjectItems.Count; next++)
        {
            for (var node = state.ObjectItems[next].ClipChain; node is not null; node = node.Parent)
            {
                if (ReferenceEquals(node, clip))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Gets the matrix that puts a clip's path where the object's move puts the object, in the clip's own coordinates.</summary>
    /// <param name="node">The clip.</param>
    /// <param name="item">The object.</param>
    /// <param name="matrix">Receives the matrix.</param>
    /// <returns><see langword="false"/> when the clip's coordinates cannot be inverted.</returns>
    private static bool TryGetMoved(ClipNode node, PdfPageObject item, out Matrix3x2 matrix)
    {
        if (!Matrix3x2.Invert(node.Path.Matrix, out var inverse))
        {
            matrix = Matrix3x2.Identity;
            return false;
        }

        matrix = node.Path.Matrix * item.Transformation * inverse;
        return true;
    }

    /// <summary>Writes a clipping path moved by a matrix, as a path that only clips.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="clip">The clip.</param>
    /// <param name="matrix">The matrix.</param>
    private static void WriteMoved(ref PdfContentBuilder builder, PdfClipPath clip, Matrix3x2 matrix)
    {
        builder.WriteRaw(PdfPageContentWriter.NewLine);
        foreach (var segment in clip.Segments)
        {
            WriteSegment(ref builder, segment, matrix);
        }

        PdfPageContentPathWriter.WriteClip(ref builder, clip.EvenOdd ? PdfClipMode.EvenOdd : PdfClipMode.NonZero);
        builder.EndPath();
        builder.WriteRaw(PdfPageContentWriter.NewLine);
    }

    /// <summary>Writes one segment of a path, moved by a matrix.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="matrix">The matrix.</param>
    private static void WriteSegment(ref PdfContentBuilder builder, PdfPathSegment segment, Matrix3x2 matrix)
    {
        var first = Vector2.Transform(new(segment.X1, segment.Y1), matrix);
        switch (segment.Kind)
        {
            case PdfPathSegmentKind.MoveTo:
                {
                    builder.MoveTo(first.X, first.Y);
                    break;
                }

            case PdfPathSegmentKind.LineTo:
                {
                    builder.LineTo(first.X, first.Y);
                    break;
                }

            case PdfPathSegmentKind.CurveTo:
                {
                    var second = Vector2.Transform(new(segment.X2, segment.Y2), matrix);
                    var third = Vector2.Transform(new(segment.X3, segment.Y3), matrix);
                    builder.CurveTo(first.X, first.Y, second.X, second.Y, third.X, third.Y);
                    break;
                }

            case PdfPathSegmentKind.Rectangle:
                {
                    WriteRectangle(ref builder, segment, matrix);
                    break;
                }

            case PdfPathSegmentKind.Close:
                {
                    builder.ClosePath();
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Writes a rectangle moved by a matrix: a rectangle while the matrix keeps it one, and its four corners when the matrix turns or skews it.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="segment">The rectangle, as its corner and its size.</param>
    /// <param name="matrix">The matrix.</param>
    private static void WriteRectangle(ref PdfContentBuilder builder, PdfPathSegment segment, Matrix3x2 matrix)
    {
        var lower = Vector2.Transform(new(segment.X1, segment.Y1), matrix);
        var upper = Vector2.Transform(new(segment.X1 + segment.X2, segment.Y1 + segment.Y2), matrix);
        if (MathF.Abs(matrix.M12) < SkewTolerance && MathF.Abs(matrix.M21) < SkewTolerance)
        {
            builder.Rectangle(lower.X, lower.Y, upper.X - lower.X, upper.Y - lower.Y);
            return;
        }

        var right = Vector2.Transform(new(segment.X1 + segment.X2, segment.Y1), matrix);
        var left = Vector2.Transform(new(segment.X1, segment.Y1 + segment.Y2), matrix);
        builder.MoveTo(lower.X, lower.Y);
        builder.LineTo(right.X, right.Y);
        builder.LineTo(upper.X, upper.Y);
        builder.LineTo(left.X, left.Y);
        builder.ClosePath();
    }
}

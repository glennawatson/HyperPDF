// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Finds the subpaths of a stroked path that are degenerate: a single point, or two or more points at the same place. With butt or
/// projecting square caps they paint nothing, because the cap has no direction (ISO 32000-2 8.5.3.2); only round caps paint a dot.
/// </summary>
internal static class DegenerateSubpaths
{
    /// <summary>The line cap style that is round.</summary>
    private const int RoundCap = 1;

    /// <summary>Gives the path to stroke: the path itself when no subpath is degenerate or the caps are round, otherwise a copy without them.</summary>
    /// <param name="path">The path.</param>
    /// <param name="lineCap">The line cap style: 0 butt, 1 round, 2 projecting square.</param>
    /// <returns>The path to stroke.</returns>
    internal static PdfPath Without(PdfPath path, int lineCap)
    {
        if (lineCap == RoundCap || !HasAny(path.Commands))
        {
            return path;
        }

        var commands = path.Commands;
        var kept = new List<PdfPathCommand>(commands.Length);
        var start = 0;
        while (start < commands.Length)
        {
            var end = start + 1;
            while (end < commands.Length && commands[end].Kind != PdfPathCommandKind.MoveTo)
            {
                end++;
            }

            if (!IsDegenerate(commands[start..end]))
            {
                kept.AddRange(commands[start..end]);
            }

            start = end;
        }

        return new([.. kept], path.FillRule);
    }

    /// <summary>Determines whether any subpath of the commands is degenerate.</summary>
    /// <param name="commands">The path's commands.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    private static bool HasAny(ReadOnlySpan<PdfPathCommand> commands)
    {
        var start = 0;
        while (start < commands.Length)
        {
            var end = start + 1;
            while (end < commands.Length && commands[end].Kind != PdfPathCommandKind.MoveTo)
            {
                end++;
            }

            if (commands[start].Kind == PdfPathCommandKind.MoveTo && end - start > 1 && IsDegenerate(commands[start..end]))
            {
                return true;
            }

            start = end;
        }

        return false;
    }

    /// <summary>Determines whether one subpath, a move followed by its segments, never leaves its first point and closes or has a segment.</summary>
    /// <param name="subpath">The subpath's commands, the move first.</param>
    /// <returns><see langword="true"/> when it is a degenerate closed subpath or has segments that all stay on the first point.</returns>
    private static bool IsDegenerate(ReadOnlySpan<PdfPathCommand> subpath)
    {
        if (subpath.Length < 2 || subpath[0].Kind != PdfPathCommandKind.MoveTo)
        {
            return false;
        }

        var origin = subpath[0].Point;
        for (var i = 1; i < subpath.Length; i++)
        {
            var command = subpath[i];
            if (command.Kind == PdfPathCommandKind.Close)
            {
                continue;
            }

            if (command.Point != origin || (command.Kind != PdfPathCommandKind.LineTo && (command.Control1 != origin || command.Control2 != origin)))
            {
                return false;
            }
        }

        return true;
    }
}

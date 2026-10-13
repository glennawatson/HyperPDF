// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Groups runs into lines. Reading order starts as content stream order. Consecutive runs that write in the same
/// direction along the same baseline belong to one line; a run starts a new line when its direction differs or its baseline
/// is more than half an em (of the smaller run) across from the line's first run. Half an em keeps superscripts and
/// subscripts (shifted by about a third of an em) on their line and separates lines, which are at least an em apart.
/// </summary>
internal static class TextLineGrouping
{
    /// <summary>The largest sine of the angle between two runs' directions for them to share a line, about three degrees.</summary>
    private const float MaxDirectionSine = 0.05F;

    /// <summary>The share of the smaller em that a baseline may be offset and still be the same line.</summary>
    private const float BaselineShare = 0.5F;

    /// <summary>Assembles every run of the state into lines, in content order.</summary>
    /// <param name="state">The build state, holding the runs the device collected.</param>
    internal static void Assemble(TextPageBuildState state)
    {
        var runs = state.Runs;
        for (var i = 0; i < runs.Count; i++)
        {
            state.Frames.Add(TextRunFrame.Of(runs[i]));
        }

        var first = 0;
        for (var i = 1; i <= runs.Count; i++)
        {
            if (i < runs.Count && SameLine(state, first, i))
            {
                continue;
            }

            TextLineAssembly.AppendLine(state, first, i - first);
            first = i;
        }
    }

    /// <summary>Determines whether a run continues the line that starts at another run.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="first">The first run of the line.</param>
    /// <param name="candidate">The run to test.</param>
    /// <returns><see langword="true"/> when the run belongs to the line.</returns>
    private static bool SameLine(TextPageBuildState state, int first, int candidate)
    {
        if (state.Runs[first].Font.IsVertical != state.Runs[candidate].Font.IsVertical)
        {
            return false;
        }

        var line = state.Frames[first];
        var other = state.Frames[candidate];
        if (Vector2.Dot(line.Axis, other.Axis) <= 0 || MathF.Abs((line.Axis.X * other.Axis.Y) - (line.Axis.Y * other.Axis.X)) > MaxDirectionSine)
        {
            return false;
        }

        var offset = MathF.Abs(Vector2.Dot(other.Start - line.Start, line.Normal));
        return offset <= BaselineShare * MathF.Min(line.SafeEm, other.SafeEm);
    }
}

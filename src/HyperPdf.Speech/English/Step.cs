// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.RegularExpressions;

namespace HyperPdf.Speech.English;

/// <summary>One step of normalizing text: a pattern, and how each match is written as words.</summary>
/// <param name="Pattern">Finds the text the step rewrites.</param>
/// <param name="Write">Writes the words for one match; <see langword="null"/> to leave the matches out.</param>
/// <param name="Starts">Finds candidate starts for a pattern anchored at the search offset, or null for an unanchored pattern.</param>
internal readonly record struct Step(Regex Pattern, MatchWriter? Write, SearchValues<char>? Starts)
{
    /// <summary>Initializes a new instance of the <see cref="Step"/> struct for an unanchored pattern.</summary>
    /// <param name="pattern">The unanchored pattern.</param>
    /// <param name="write">Writes a matched replacement, or null to remove it.</param>
    internal Step(Regex pattern, MatchWriter? write)
        : this(pattern, write, null)
    {
    }

    /// <summary>Rewrites every match in some text.</summary>
    /// <param name="input">The text.</param>
    /// <param name="british">Whether to say dates the British way.</param>
    /// <param name="output">Receives the whole rewritten text when there is a match.</param>
    /// <returns><see langword="true"/> when something matched and the output holds the rewritten text.</returns>
    internal bool Apply(ReadOnlySpan<char> input, bool british, SpokenText output)
    {
        if (Starts is { } starts)
        {
            return ApplyAtCandidates(input, british, output, starts);
        }

        var done = 0;
        var matched = false;
        foreach (var match in Pattern.EnumerateMatches(input))
        {
            matched = true;
            output.Append(input[done..match.Index]);
            Write?.Invoke(new(input.Slice(match.Index, match.Length), british, output));
            done = match.Index + match.Length;
        }

        if (!matched)
        {
            return false;
        }

        output.Append(input[done..]);
        return true;
    }

    /// <summary>Tests a search-anchored pattern only at candidates found by the span search.</summary>
    /// <param name="input">The full text, preserving the pattern's boundary checks.</param>
    /// <param name="british">Whether dates use British ordering.</param>
    /// <param name="output">Receives the rewritten text.</param>
    /// <param name="starts">Finds candidate start characters.</param>
    /// <returns>Whether any replacement was written.</returns>
    private bool ApplyAtCandidates(ReadOnlySpan<char> input, bool british, SpokenText output, SearchValues<char> starts)
    {
        var next = 0;
        var done = 0;
        var matched = false;
        while (next < input.Length)
        {
            var candidate = input[next..].IndexOfAny(starts);
            if (candidate < 0)
            {
                break;
            }

            next += candidate;
            var matches = Pattern.EnumerateMatches(input, next);
            if (!matches.MoveNext())
            {
                next++;
                continue;
            }

            var match = matches.Current;
            matched = true;
            output.Append(input[done..match.Index]);
            Write?.Invoke(new(input.Slice(match.Index, match.Length), british, output));
            done = match.Index + match.Length;
            next = done;
        }

        if (!matched)
        {
            return false;
        }

        output.Append(input[done..]);
        return true;
    }
}

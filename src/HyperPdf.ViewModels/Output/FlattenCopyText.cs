// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Flattening;

namespace HyperPdf.ViewModels.Output;

/// <summary>Words a flattened copy's result in plain language, one point per line.</summary>
internal static class FlattenCopyText
{
    /// <summary>The lines before the notes: where the copy was saved, the two counts and the form line.</summary>
    private const int HeadingLines = 4;

    /// <summary>Describes a finished flattened copy, one point per line.</summary>
    /// <param name="result">The result.</param>
    /// <param name="path">Where the copy was saved.</param>
    /// <returns>The lines.</returns>
    internal static string[] Lines(FlattenCopyResult result, string path)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Flattened)
        {
            var problems = new List<string>(result.Problems.Count + 1) { "The copy could not be flattened, so no file was saved. Here is why:" };
            foreach (var problem in result.Problems)
            {
                problems.Add($"• {problem.Description}");
            }

            return [.. problems];
        }

        var lines = new List<string>(result.Notes.Count + HeadingLines)
        {
            $"Saved as {Path.GetFileName(path)}. Your open file was not changed.",
            $"{Count(result.FormFieldsFlattened, "form field")} now part of the page and can no longer be filled in.",
            $"{Count(result.AnnotationsFlattened, "comment, highlight or other mark")} now part of the page and can no longer be edited.",
        };
        if (result.FormRemoved)
        {
            lines.Add("The form was removed, because every field was flattened.");
        }

        foreach (var note in result.Notes)
        {
            lines.Add($"• {note.Description}");
        }

        return [.. lines];
    }

    /// <summary>Words a count of things with the verb that follows them.</summary>
    /// <param name="count">How many.</param>
    /// <param name="noun">The singular noun.</param>
    /// <returns>The words, such as "3 form fields are".</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Count(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? " is" : "s are")}");
}

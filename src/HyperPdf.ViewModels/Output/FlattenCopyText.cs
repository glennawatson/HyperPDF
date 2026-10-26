// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
            var problems = new List<string>(result.Problems.Count + 1) { UiText.Get("The copy could not be flattened, so no file was saved. Here is why:") };
            foreach (var problem in result.Problems)
            {
                problems.Add($"• {problem.Description}");
            }

            return [.. problems];
        }

        var lines = new List<string>(result.Notes.Count + HeadingLines)
        {
            UiText.Format("Saved as {0}. Your open file was not changed.", Path.GetFileName(path)),
            UiText.Format("{0} now part of the page and can no longer be filled in.", Count(result.FormFieldsFlattened, UiText.Get("form field"))),
            UiText.Format("{0} now part of the page and can no longer be edited.", Count(result.AnnotationsFlattened, UiText.Get("comment, highlight or other mark"))),
        };
        if (result.FormRemoved)
        {
            lines.Add(UiText.Get("The form was removed, because every field was flattened."));
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
        $"{count} {noun}{(count == 1 ? UiText.Get(" is") : UiText.Get("s are"))}";
}

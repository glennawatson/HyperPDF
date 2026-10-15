// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Rendering;

/// <summary>Settings that change decoded images or recorded content shared by renderers of one document.</summary>
/// <param name="ImageCacheBytes">The decoded image cache limit.</param>
/// <param name="SimulateOverprint">Whether overprint is previewed.</param>
/// <param name="FormHighlight">The tint drawn over fillable form fields.</param>
/// <param name="HiddenColorants">The hidden colorant names, sorted and joined by a line feed; empty for none.</param>
/// <param name="Quality">How edges, thin lines and scaled images are drawn.</param>
internal readonly record struct PdfRenderCacheKey(long ImageCacheBytes, bool SimulateOverprint, PdfFormHighlight FormHighlight, string HiddenColorants, PdfRenderQuality Quality)
{
    /// <summary>The character that separates the hidden colorant names in the key.</summary>
    private const char Separator = '\n';

    /// <summary>Sorts the hidden colorants and drops repeats, so equal sets give equal keys.</summary>
    /// <param name="hidden">The names, or a default array for none.</param>
    /// <returns>The distinct names in ordinal order.</returns>
    internal static ImmutableArray<string> Normalize(ImmutableArray<string> hidden)
    {
        if (hidden.IsDefaultOrEmpty)
        {
            return [];
        }

        var names = new List<string>(hidden.Length);
        foreach (var name in hidden)
        {
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        for (var i = names.Count - 1; i > 0; i--)
        {
            if (string.Equals(names[i], names[i - 1], StringComparison.Ordinal))
            {
                names.RemoveAt(i);
            }
        }

        return [.. names];
    }

    /// <summary>Joins normalized names into the key's text.</summary>
    /// <param name="hidden">The normalized names.</param>
    /// <returns>The text; empty for none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Join(ImmutableArray<string> hidden) => hidden.IsDefaultOrEmpty ? string.Empty : string.Join(Separator, hidden.AsSpan());
}

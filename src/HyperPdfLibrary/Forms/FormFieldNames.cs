// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Forms;

/// <summary>Checks and splits field names. A fully qualified name is the partial names from the root, joined by periods (ISO 32000-2 12.7.3.2).</summary>
internal static class FormFieldNames
{
    /// <summary>The character between partial names.</summary>
    internal const char Separator = '.';

    /// <summary>The longest fully qualified name accepted.</summary>
    internal const int MaxLength = 1024;

    /// <summary>The first printable character; lower ones are control characters.</summary>
    private const char FirstPrintable = ' ';

    /// <summary>Checks a fully qualified name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>A plain reason the name is not allowed; <see langword="null"/> when it is.</returns>
    internal static string? Validate(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "A field needs a name.";
        }

        if (name.Length > MaxLength)
        {
            return "The field name is too long.";
        }

        var start = 0;
        for (var i = 0; i <= name.Length; i++)
        {
            if (i < name.Length && name[i] != Separator)
            {
                if (name[i] < FirstPrintable)
                {
                    return "A field name cannot hold control characters.";
                }

                continue;
            }

            if (i == start)
            {
                return "A field name cannot start or end with a period or hold two periods in a row.";
            }

            start = i + 1;
        }

        return null;
    }

    /// <summary>Checks one partial name.</summary>
    /// <param name="partial">The partial name.</param>
    /// <returns>A plain reason the name is not allowed; <see langword="null"/> when it is.</returns>
    internal static string? ValidatePartial(string? partial) =>
        partial is not null && partial.Contains(Separator, StringComparison.Ordinal)
            ? "A partial name cannot hold a period. Use the full name to move the field under another field."
            : Validate(partial);

    /// <summary>Gets the part of a name after its last period.</summary>
    /// <param name="fullName">The fully qualified name.</param>
    /// <returns>The partial name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetLeaf(string fullName) => fullName[(fullName.LastIndexOf(Separator) + 1)..];

    /// <summary>Gets the part of a name before its last period.</summary>
    /// <param name="fullName">The fully qualified name.</param>
    /// <returns>The parent's fully qualified name; empty for a root field.</returns>
    internal static string GetParent(string fullName)
    {
        var at = fullName.LastIndexOf(Separator);
        return at < 0 ? string.Empty : fullName[..at];
    }

    /// <summary>Joins a parent's name and a partial name.</summary>
    /// <param name="parent">The parent's fully qualified name; empty for a root field.</param>
    /// <param name="partial">The partial name.</param>
    /// <returns>The fully qualified name.</returns>
    internal static string Join(string parent, string partial) => parent.Length == 0 ? partial : $"{parent}.{partial}";

    /// <summary>Determines whether a name is inside another: it starts with it and a period.</summary>
    /// <param name="name">The name that may be inside.</param>
    /// <param name="ancestor">The possible ancestor.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> is below <paramref name="ancestor"/>.</returns>
    internal static bool IsBelow(string name, string ancestor) =>
        name.Length > ancestor.Length && name[ancestor.Length] == Separator && name.StartsWith(ancestor, StringComparison.Ordinal);
}

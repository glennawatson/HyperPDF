// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Adds content bytes and resources and checks for edits.</summary>
public static class PdfPageContentEditing
{
    /// <summary>Gets a value indicating whether any object changed or content was added.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <returns>The current value.</returns>
    public static bool IsModified(PdfPageContent state) =>
        state.Appended.Count > 0
        || state.Pending.Count > 0
        || PdfPageContentEditing.AnyObjectModified(state)
        || PdfPageContentEditing.AnyMarkScrubbed(state);

    /// <summary>Adds bytes to the end of the regenerated content, in a graphics state that is balanced and unclipped by the content before.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "content">The operators to add.</param>
    public static void Append(PdfPageContent state, ReadOnlySpan<byte> content)
    {
        _ = state.Appended.EnsureCapacity(state.Appended.Count + content.Length + 1);
        state.Appended.AddRange(content);
        state.Appended.Add((byte)'\n');
    }

    /// <summary>Makes a resource name that is free in the content's resources and in what has been added.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "category">The resource category.</param>
    /// <param name = "prefix">The start of the name.</param>
    /// <returns>The name.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "prefix"/> is <see langword="null"/>.</exception>
    public static PdfName AllocateName(PdfPageContent state, KnownName category, string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var names = state.Document.Objects.Names;
        var existing = state.Resources?.GetDictionary(category);
        var number = 1;
        var candidate = names.Intern(string.Create(CultureInfo.InvariantCulture, $"{prefix}{number}"));
        while ((existing is not null && existing.ContainsKey(candidate)) || PdfPageContentEditing.IsPending(state, category, candidate))
        {
            number++;
            candidate = names.Intern(string.Create(CultureInfo.InvariantCulture, $"{prefix}{number}"));
        }

        return candidate;
    }

    /// <summary>Adds a resource the appended content names. It is stored in the resources when the content is applied.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "category">The resource category, such as /Font or /XObject.</param>
    /// <param name = "name">The name from <see cref = "PdfPageContentEditing.AllocateName"/>.</param>
    /// <param name = "value">A stream or dictionary to store, or a reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddResource(PdfPageContent state, KnownName category, PdfName name, PdfValue value) => state.Pending.Add(new(category, name, value, null));

    /// <summary>Determines whether any object has a change to write.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    internal static bool AnyObjectModified(PdfPageContent state)
    {
        foreach (var item in state.ObjectItems)
        {
            if (item.IsModified)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether any marked-content sequence was scrubbed.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <returns><see langword="true"/> when one was.</returns>
    internal static bool AnyMarkScrubbed(PdfPageContent state)
    {
        foreach (var mark in state.Begins)
        {
            if (mark.IsScrubbed)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a name is used by a resource that is waiting to be stored.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "category">The category.</param>
    /// <param name = "name">The name.</param>
    /// <returns><see langword="true"/> when the name is taken.</returns>
    internal static bool IsPending(PdfPageContent state, KnownName category, PdfName name)
    {
        foreach (var resource in state.Pending)
        {
            if (resource.Category == category && resource.Name == name)
            {
                return true;
            }
        }

        return false;
    }
}

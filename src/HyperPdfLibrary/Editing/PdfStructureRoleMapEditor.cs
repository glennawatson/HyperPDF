// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Editing;

/// <summary>Adds, changes and removes entries of the structure tree's <c>/RoleMap</c> (ISO 32000-2 14.8.3), which tells a reader what a custom tag type means.</summary>
internal static class PdfStructureRoleMapEditor
{
    /// <summary>The longest custom type name accepted, in characters.</summary>
    private const int MaxNameLength = 127;

    /// <summary>The most UTF-8 bytes one UTF-16 character takes.</summary>
    private const int MaxUtf8BytesPerChar = 3;

    /// <summary>The most mappings followed when looking for a cycle.</summary>
    private const int MaxChain = 64;

    /// <summary>Maps a custom type to a standard type.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="type">The custom type; it must not be a standard type itself.</param>
    /// <param name="mapsTo">The standard type it stands for.</param>
    /// <exception cref="ArgumentException">A name is not allowed.</exception>
    /// <exception cref="InvalidOperationException">The mapping would make a cycle, or the file has no structure tree.</exception>
    internal static void Set(PdfObjectEdits edits, string type, string mapsTo)
    {
        CheckName(type);
        if (!IsStandard(mapsTo))
        {
            throw new ArgumentException($"'{mapsTo}' is not a standard tag type. Choose a standard type for the custom type to stand for.", nameof(mapsTo));
        }

        if (IsStandard(type))
        {
            throw new ArgumentException($"'{type}' is already a standard tag type, so it cannot be mapped to another.", nameof(type));
        }

        var root = Root(edits);
        var map = PdfEditDictionaries.Child(edits, root, KnownName.RoleMap);
        var names = edits.Store.Names;
        var key = names.Intern(type);
        var target = names.Intern(mapsTo);
        RequireNoCycle(map, key, target, type, mapsTo);
        map.Set(key, PdfValue.FromName(target));
    }

    /// <summary>Removes a custom type's mapping.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="type">The custom type.</param>
    /// <returns><see langword="true"/> when a mapping was removed.</returns>
    /// <exception cref="InvalidOperationException">The file has no structure tree.</exception>
    internal static bool Remove(PdfObjectEdits edits, string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var root = Root(edits);
        var key = edits.Store.Names.Intern(type);
        if (root.GetDictionary(KnownName.RoleMap) is not { } existing || existing.Get(key).IsNull)
        {
            return false;
        }

        return PdfEditDictionaries.Child(edits, root, KnownName.RoleMap).Remove(key);
    }

    /// <summary>Determines whether a name is a standard structure type in either standard namespace.</summary>
    /// <param name="text">The name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsStandard(string text)
    {
        if (text.Length > MaxNameLength)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[MaxNameLength * MaxUtf8BytesPerChar];
        return PdfStructureTypes.Find(buffer[..Encoding.UTF8.GetBytes(text, buffer)]) != PdfStructureType.Unknown;
    }

    /// <summary>Gets the editable structure tree root.</summary>
    /// <param name="edits">The edits.</param>
    /// <returns>The root.</returns>
    /// <exception cref="InvalidOperationException">The file has no structure tree.</exception>
    private static PdfDictionary Root(PdfObjectEdits edits)
    {
        if (edits.Catalog().GetRaw(KnownName.StructTreeRoot).IsNull)
        {
            throw new InvalidOperationException("This document has no tags, so there is no role map to change.");
        }

        return PdfEditDictionaries.Child(edits, edits.Catalog(), KnownName.StructTreeRoot);
    }

    /// <summary>Refuses a name that would make a bad key.</summary>
    /// <param name="type">The custom type.</param>
    /// <exception cref="ArgumentException">The name is empty, long, or has spaces or control characters.</exception>
    private static void CheckName(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.Length is 0 or > MaxNameLength || HasSpaceOrControl(type))
        {
            throw new ArgumentException("A custom tag type needs a name of up to 127 characters, with no spaces.", nameof(type));
        }
    }

    /// <summary>Determines whether text has a space or control character.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasSpaceOrControl(string text)
    {
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Refuses a mapping that leads back to the custom type.</summary>
    /// <param name="map">The role map.</param>
    /// <param name="key">The custom type's name.</param>
    /// <param name="target">The name it would map to.</param>
    /// <param name="type">The custom type's text, for the message.</param>
    /// <param name="mapsTo">The target's text, for the message.</param>
    /// <exception cref="InvalidOperationException">Following the mappings from the target reaches the custom type.</exception>
    private static void RequireNoCycle(PdfDictionary map, PdfName key, PdfName target, string type, string mapsTo)
    {
        var current = target;
        for (var i = 0; i < MaxChain; i++)
        {
            if (current == key)
            {
                throw new InvalidOperationException($"Mapping '{type}' to '{mapsTo}' would lead back to '{type}', so nothing changed.");
            }

            var next = map.GetName(current);
            if (next == default)
            {
                return;
            }

            current = next;
        }

        throw new InvalidOperationException("The role map is too deep to check, so nothing changed.");
    }
}

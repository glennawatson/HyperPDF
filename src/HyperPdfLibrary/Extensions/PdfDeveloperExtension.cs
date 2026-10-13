// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Extensions;

/// <summary>A developer extension the document declares in the catalog's <c>/Extensions</c> dictionary.</summary>
/// <param name="Prefix">The developer prefix, for example ADBE.</param>
/// <param name="BaseVersion">The PDF version the extension builds on, for example "1.7".</param>
/// <param name="ExtensionLevel">The extension level.</param>
/// <param name="Url">A URL describing the extension, or null.</param>
[DebuggerDisplay("PdfDeveloperExtension: {Prefix} {BaseVersion} level {ExtensionLevel}")]
public sealed record PdfDeveloperExtension(string Prefix, string BaseVersion, int ExtensionLevel, string? Url)
{
    /// <summary>
    /// Gets the extension's revision, such as <c>:2024</c> (the colon is part of the identifier), or <see langword="null"/>
    /// when it has none. ISO/TS 32003 and ISO/TS 32004 require one.
    /// </summary>
    public string? Revision { get; init; }

    /// <summary>Determines whether the catalog declares an extension of this prefix and level, as one dictionary or in an array.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="names">The name table.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal bool IsDeclaredIn(PdfDictionary catalog, PdfNameTable names)
    {
        var entry = catalog.GetDictionary(KnownName.Extensions)?.Get(names.Intern(Prefix)) ?? default;
        if (entry.AsArray() is not { } array)
        {
            return IsLevel(entry.AsDictionary(), names);
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (IsLevel(array.GetDictionary(i), names))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an extension dictionary has this extension's level.</summary>
    /// <param name="extension">The dictionary, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private bool IsLevel(PdfDictionary? extension, PdfNameTable names) =>
        extension?.GetInteger(names.Intern("ExtensionLevel")) == ExtensionLevel;
}

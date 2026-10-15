// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Owns a resource dictionary for one set of appearance fonts and an optional icon.</summary>
internal sealed class FormResourceSet
{
    /// <summary>The fonts in resource insertion order.</summary>
    private readonly FormFont[] _fonts;

    /// <summary>The optional icon.</summary>
    private readonly PdfValue _icon;

    /// <summary>Initializes a new instance of the <see cref="FormResourceSet"/> class.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="fonts">The fonts.</param>
    /// <param name="icon">The optional icon.</param>
    internal FormResourceSet(PdfObjectStore store, ReadOnlySpan<FormFont> fonts, PdfValue icon)
    {
        _fonts = fonts.ToArray();
        _icon = icon;
        Resources = FormAppearanceResources.CreateResources(store, fonts, icon);
    }

    /// <summary>Gets the shared resource dictionary.</summary>
    internal PdfDictionary Resources { get; }

    /// <summary>Checks that the resource names, fonts and icon are unchanged.</summary>
    /// <param name="fonts">The requested fonts.</param>
    /// <param name="icon">The requested icon.</param>
    /// <returns>Whether these resources match.</returns>
    internal bool Matches(ReadOnlySpan<FormFont> fonts, PdfValue icon)
    {
        if (fonts.Length != _fonts.Length || !icon.Equals(_icon))
        {
            return false;
        }

        for (var i = 0; i < fonts.Length; i++)
        {
            if (!ReferenceEquals(fonts[i], _fonts[i]))
            {
                return false;
            }
        }

        return true;
    }
}

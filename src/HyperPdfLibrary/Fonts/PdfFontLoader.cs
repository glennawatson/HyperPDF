// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The library's font factory: picks the font kind by /Subtype (ISO 32000-2, 9.6.1). /Type0 is
/// composite, /Type3 draws content streams, /TrueType is TrueType, and every other subtype (/Type1, /MMType1 or a
/// missing one) loads as Type 1.
/// </summary>
public static class PdfFontLoader
{
    /// <summary>Loads a font, generating any missing predefined resources on demand.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="cancellationToken">Cancels source I/O and generation.</param>
    /// <returns>The font, or null when the dictionary cannot define one.</returns>
    public static async ValueTask<PdfFont?> LoadAsync(PdfDictionary dictionary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var resources = PdfFontResources.For(dictionary);
        try
        {
            await FontDataPrefetcher.PrepareFontAsync(dictionary, resources, cancellationToken).ConfigureAwait(false);
            return LoadPrepared(dictionary, resources, cancellationToken);
        }
        finally
        {
            if (dictionary.Owner?.Context is null)
            {
                resources.CloseDownloadClient();
            }
        }
    }

    /// <summary>Loads a font from its dictionary.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The font, or <see langword="null"/> when a composite font lacks its descendant or encoding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFont? Load(PdfDictionary dictionary) =>
        LoadPrepared(dictionary, null, CancellationToken.None);

    /// <summary>Loads a font using the selected resources.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="selectedResources">Resources already selected by an async prefetch, if any.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>The font, or null.</returns>
    private static PdfFont? LoadPrepared(PdfDictionary dictionary, PdfFontResources? selectedResources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        cancellationToken.ThrowIfCancellationRequested();
        var subtype = dictionary.GetName(KnownName.Subtype);
        var resources = selectedResources ?? PdfFontResources.For(dictionary);
        if (subtype.Is(KnownName.Type0))
        {
            return CompositeFontLoader.Load(dictionary, resources, cancellationToken);
        }

        return subtype.Is(KnownName.Type3) ? new Type3Font(dictionary, resources) : SimpleFontLoader.Load(dictionary, subtype.Is(KnownName.TrueType), resources, cancellationToken);
    }
}

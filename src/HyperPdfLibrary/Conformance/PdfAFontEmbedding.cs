// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Embeds the fonts a document uses but does not embed, which every PDF/A level requires (ISO 19005-1 §6.3.4; ISO 19005-2
/// and -3 §6.2.11.4.1; ISO 19005-4:2020 §6.2.10.4.1). A font is replaced by the library's open font with the same metrics
/// (Arimo for Helvetica and Arial, Tinos for Times, Cousine for Courier) when every character it shows exists in that font
/// and keeps its width. Fonts used only for invisible text, such as an OCR layer, are left alone, as PDF/A exempts them.
/// </summary>
internal static class PdfAFontEmbedding
{
    /// <summary>The styles of each standard text family.</summary>
    private const int StylesPerFamily = 4;

    /// <summary>The bold style's place in a family.</summary>
    private const int BoldStyle = 1;

    /// <summary>The bold slanted style's place in a family.</summary>
    private const int BoldSlantedStyle = 2;

    /// <summary>The slanted style's place in a family.</summary>
    private const int SlantedStyle = 3;

    /// <summary>Makes sure the open fonts a conversion will embed are on this computer, downloading them when needed.</summary>
    /// <param name="document">The document to convert.</param>
    /// <param name="cancellationToken">Cancels the downloads.</param>
    /// <returns>A task that completes when the fonts are ready or cannot be had.</returns>
    internal static async ValueTask PrepareAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        var resources = PdfFontResources.For(document.Catalog);
        foreach (var face in FacesNeeded(document, cancellationToken))
        {
            try
            {
                await BundledFaces.EnsureAsync(resources, face.Family, face.Bold, face.Italic, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                // The conversion reports a font it cannot embed in plain words; a failed download is one cause.
            }
        }
    }

    /// <summary>Embeds the missing fonts, or refuses with the fonts that cannot be embedded.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        var graph = PdfObjectGraph.Collect(session.Store, false, session.CancellationToken);
        var targets = new List<int>();
        for (var number = 1; number <= graph.Count; number++)
        {
            if (graph.GetValue(number).Kind == PdfKind.Dictionary && IsNotEmbedded(graph.GetValue(number).AsDictionary()!))
            {
                targets.Add(graph.GetOldNumber(number));
            }
        }

        if (targets.Count == 0)
        {
            return;
        }

        var usage = PdfAConversionTextUse.Collect(session);
        var embedded = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var number in targets)
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            EmbedOne(session, new(number, 0), usage, embedded);
        }

        if (embedded.Count > 0)
        {
            session.Changed(
                string.Create(CultureInfo.InvariantCulture, $"Embedded matching open fonts for {string.Join(", ", embedded)}, which the document did not include."),
                PdfARule.FontEmbedded);
        }
    }

    /// <summary>Determines whether a dictionary is a font that should carry an embedded program but does not.</summary>
    /// <param name="font">The dictionary.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsNotEmbedded(PdfDictionary font)
    {
        if (!font.IsName(KnownName.Type, KnownName.Font) && !font.ContainsKey(KnownName.BaseFont))
        {
            return false;
        }

        var owner = font.GetName(KnownName.Subtype).ToKnownName() switch
        {
            KnownName.Type0 => font.GetArray(KnownName.DescendantFonts)?.GetDictionary(0),
            KnownName.Type1 or KnownName.MMType1 or KnownName.TrueType => font,
            _ => null,
        };
        return owner is not null && !FontDescriptor.Read(owner.GetDictionary(KnownName.FontDescriptor)).IsEmbedded;
    }

    /// <summary>Gets the standard font a font dictionary names, when the library has an open font with its metrics.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="font">The font dictionary.</param>
    /// <returns>The standard font, or <see cref="StandardFont.None"/>.</returns>
    internal static StandardFont Substitutable(PdfObjectStore store, PdfDictionary font)
    {
        if (font.GetName(KnownName.Subtype).ToKnownName() is not (KnownName.Type1 or KnownName.MMType1 or KnownName.TrueType))
        {
            return StandardFont.None;
        }

        var standard = StandardFonts.Find(Encoding.UTF8.GetBytes(store.Names.GetString(font.GetName(KnownName.BaseFont))));
        return standard is StandardFont.Symbol or StandardFont.ZapfDingbats ? StandardFont.None : standard;
    }

    /// <summary>Gets the open face that stands in for a standard font.</summary>
    /// <param name="standard">The standard font.</param>
    /// <returns>The face.</returns>
    internal static PdfAConversionFace FaceOf(StandardFont standard)
    {
        // The twelve text fonts come in families of four, each ordered regular, bold, bold slanted, slanted.
        var slot = (int)standard - (int)StandardFont.Courier;
        var family = (slot / StylesPerFamily) switch
        {
            0 => BundledFamily.Fixed,
            1 => BundledFamily.Sans,
            _ => BundledFamily.Serif,
        };
        var style = slot % StylesPerFamily;
        return new(family, style is BoldStyle or BoldSlantedStyle, style is BoldSlantedStyle or SlantedStyle);
    }

    /// <summary>Lists the open faces the document's missing fonts need.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The faces.</returns>
    private static HashSet<PdfAConversionFace> FacesNeeded(PdfDocument document, CancellationToken cancellationToken)
    {
        var store = document.Objects;
        var graph = PdfObjectGraph.Collect(store, false, cancellationToken);
        var faces = new HashSet<PdfAConversionFace>();
        for (var number = 1; number <= graph.Count; number++)
        {
            if (graph.GetValue(number).Kind == PdfKind.Dictionary
                && graph.GetValue(number).AsDictionary() is { } font
                && IsNotEmbedded(font)
                && Substitutable(store, font) is not StandardFont.None and var standard)
            {
                _ = faces.Add(FaceOf(standard));
            }
        }

        return faces;
    }

    /// <summary>
    /// Embeds one font unless no content draws with it or it only draws invisible text, or refuses with the reason it
    /// cannot be embedded.
    /// </summary>
    /// <param name="session">The conversion.</param>
    /// <param name="id">The font object.</param>
    /// <param name="usage">The codes each font shows, or <see langword="null"/> when the content could not be run.</param>
    /// <param name="embedded">Receives the names of the fonts embedded.</param>
    private static void EmbedOne(PdfAConversionSession session, PdfObjectId id, Dictionary<PdfDictionary, PdfAConversionTextCodes>? usage, SortedSet<string> embedded)
    {
        var store = session.Store;
        var font = StoreReading.GetObject(store, id).AsDictionary()!;
        var codes = usage?.GetValueOrDefault(font);

        // PDF/A needs only the fonts used to draw embedded, so a font no content draws, such as one named only in a form's
        // default resources, is left as it is; one that draws only invisible text needs no program either.
        if ((usage is not null && codes is null) || codes is { Visible.Count: 0, Invisible: true })
        {
            return;
        }

        var name = store.Names.GetString(font.GetName(KnownName.BaseFont));
        if (PdfAConversionSubstitute.Embed(session, id, font, codes?.Visible, usage is null) is { } reason)
        {
            session.Refuse(string.Create(CultureInfo.InvariantCulture, $"The font {name} is not embedded, and {reason}"), PdfARule.FontEmbedded);
            return;
        }

        _ = embedded.Add(name);
    }
}

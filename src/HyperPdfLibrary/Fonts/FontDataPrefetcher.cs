// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;
namespace HyperPdfLibrary.Fonts;

/// <summary>Prepares only the font data referenced by a page's resources.</summary>
internal static class FontDataPrefetcher
{
    /// <summary>Prepares only fonts selected by a page's content and appearances.</summary>
    /// <param name="document">The document owning the page snapshot.</param>
    /// <param name="page">The requested page.</param>
    /// <param name="cancellationToken">Cancels downloads and traversal.</param>
    /// <returns>A task completing when the needed assets are cached.</returns>
    internal static ValueTask PrepareAsync(PdfDocument document, PdfPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        var prepared = Volatile.Read(ref document.State.FontPreparations);
        if (prepared is null)
        {
            var created = new ConditionalWeakTable<PdfPage, FontDataPreparation>();
            prepared = Interlocked.CompareExchange(ref document.State.FontPreparations, created, null) ?? created;
        }

        var state = prepared.GetValue(page, static _ => new());
        if (state.Ready)
        {
            return ValueTask.CompletedTask;
        }

        Task pending;
        CancellationToken ownerToken;
        lock (state.Gate)
        {
            if (state.Ready)
            {
                return ValueTask.CompletedTask;
            }

            if (state.Pending is not { IsCompleted: false })
            {
                state.PendingCancellation = cancellationToken;
                state.Pending = WalkAsync(page, state, cancellationToken);
            }

            pending = state.Pending;
            ownerToken = state.PendingCancellation;
        }

        return WaitForPreparationAsync(document, page, pending, ownerToken, cancellationToken);
    }

    /// <summary>Retries a shared attempt cancelled by another caller.</summary>
    /// <param name="document">The document owning the page snapshot.</param>
    /// <param name="page">The requested page.</param>
    /// <param name="pending">The shared preparation.</param>
    /// <param name="ownerToken">The token controlling the shared attempt.</param>
    /// <param name="cancellationToken">The waiting caller's token.</param>
    /// <returns>A task completing when preparation succeeds for this caller.</returns>
    internal static async ValueTask WaitForPreparationAsync(PdfDocument document, PdfPage page, Task pending, CancellationToken ownerToken, CancellationToken cancellationToken)
    {
        try
        {
            await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ownerToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            await PrepareAsync(document, page, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Prepares a single font dictionary before loading it.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="cancellationToken">Cancels source I/O.</param>
    /// <returns>A task completing when the needed assets are cached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask PrepareFontAsync(PdfDictionary font, CancellationToken cancellationToken) =>
        PrepareFontAsync(font, PdfFontResources.For(font), cancellationToken);

    /// <summary>Prepares a font using resources selected by the caller.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="cancellationToken">Cancels source I/O.</param>
    /// <returns>A task completing when the needed assets are cached.</returns>
    internal static async ValueTask PrepareFontAsync(PdfDictionary font, PdfFontResources resources, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var composite = font.GetName(KnownName.Subtype).Is(KnownName.Type0);
        var source = composite && font.GetArray(KnownName.DescendantFonts) is { Count: 1 } descendants
            ? descendants.GetDictionary(0) ?? font
            : font;
        if (composite && ReferenceEquals(source, font))
        {
            return;
        }

        var collection = composite ? await PrepareEncodingAsync(resources, font, source, cancellationToken).ConfigureAwait(false) : CjkScript.None;
        var descriptor = FontDescriptor.Read(source.GetDictionary(KnownName.FontDescriptor));
        if (NeedsUnicode(font, collection, descriptor))
        {
            await CidToUnicodeTable.EnsureAsync(resources, collection, cancellationToken).ConfigureAwait(false);
        }

        if (descriptor.IsEmbedded || font.GetName(KnownName.Subtype).Is(KnownName.Type3))
        {
            return;
        }

        var baseFont = PdfNames.BaseFontOf(source);
        var standard = StandardFonts.Find(Encoding.UTF8.GetBytes(baseFont));
        await SystemFontMatcher.For(font, resources).EnsureAsync(new(baseFont, standard, descriptor.Flags, descriptor.Weight, collection), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks whether a font needs the collection's Unicode fallback.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="collection">The Adobe collection.</param>
    /// <param name="descriptor">The font descriptor.</param>
    /// <returns>Whether the fallback is needed.</returns>
    private static bool NeedsUnicode(PdfDictionary font, CjkScript collection, FontDescriptor descriptor)
    {
        if (collection == CjkScript.None)
        {
            return false;
        }

        if (!descriptor.IsEmbedded)
        {
            return true;
        }

        if (ToUnicodeLoader.Load(font) is not null || !font.Get(KnownName.Encoding).TryGetName(out var name))
        {
            return false;
        }

        var encoding = PdfNames.Spell(font, name);
        return encoding.IndexOf("UCS2"u8) < 0 && encoding.IndexOf("UTF16"u8) < 0 && encoding.IndexOf("UTF32"u8) < 0;
    }

    /// <summary>Prepares a composite font's encoding and its base CMaps.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="font">The Type 0 dictionary.</param>
    /// <param name="source">The descendant font.</param>
    /// <param name="cancellationToken">Cancels source I/O.</param>
    /// <returns>The character collection.</returns>
    private static async ValueTask<CjkScript> PrepareEncodingAsync(PdfFontResources resources, PdfDictionary font, PdfDictionary source, CancellationToken cancellationToken)
    {
        var collection = CjkScript.None;
        if (font.Get(KnownName.Encoding).TryGetName(out var name))
        {
            var spelling = PdfNames.Spell(font, name).ToArray();
            await PredefinedCMaps.EnsureAsync(resources, Encoding.ASCII.GetString(spelling), cancellationToken).ConfigureAwait(false);
            collection = PredefinedCMaps.Find(resources, spelling)?.Collection ?? CjkScript.None;
        }
        else if (font.GetStream(KnownName.Encoding) is { } embedded)
        {
            var parsed = ReadEmbedded(embedded, cancellationToken);
            if (parsed is { HasUseCMap: true })
            {
                await PredefinedCMaps.EnsureAsync(resources, Encoding.ASCII.GetString(parsed.UseCMap.Span), cancellationToken).ConfigureAwait(false);
            }
        }

        return collection == CjkScript.None ? CollectionOf(source) : collection;
    }

    /// <summary>Treats a damaged embedded encoding as missing, as the font loader does.</summary>
    /// <param name="stream">The encoding stream.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The parsed encoding, or null.</returns>
    private static CMapContent? ReadEmbedded(PdfStream stream, CancellationToken cancellationToken)
    {
        try
        {
            return CMapParser.Parse(stream.DecodeToArray(cancellationToken));
        }
        catch (Exception exception) when (FontLoadErrors.IsDamagedData(exception))
        {
            return null;
        }
    }

    /// <summary>Reads a font's Adobe collection for Identity and embedded CMaps.</summary>
    /// <param name="font">The descendant font.</param>
    /// <returns>The script, or None.</returns>
    private static CjkScript CollectionOf(PdfDictionary font)
    {
        var ordering = font.GetDictionary(KnownName.CIDSystemInfo) is { } info ? info.GetStringBytes(KnownName.Ordering) : ReadOnlySpan<byte>.Empty;
        if (ordering.SequenceEqual("Japan1"u8))
        {
            return CjkScript.Japanese;
        }

        if (ordering.SequenceEqual("GB1"u8))
        {
            return CjkScript.SimplifiedChinese;
        }

        if (ordering.SequenceEqual("CNS1"u8))
        {
            return CjkScript.TraditionalChinese;
        }

        return ordering.SequenceEqual("Korea1"u8) ? CjkScript.Korean : CjkScript.None;
    }

    /// <summary>Prepares a page's selected fonts and publishes readiness only after success.</summary>
    /// <param name="page">The requested page.</param>
    /// <param name="state">The preparation state.</param>
    /// <param name="cancellationToken">Cancels I/O and traversal.</param>
    /// <returns>A task completing when the page is ready.</returns>
    private static async Task WalkAsync(PdfPage page, FontDataPreparation state, CancellationToken cancellationToken)
    {
        foreach (var font in FontDataDemand.Collect(page, cancellationToken))
        {
            await PrepareFontAsync(font, cancellationToken).ConfigureAwait(false);
        }

        state.MarkReady();
    }
}

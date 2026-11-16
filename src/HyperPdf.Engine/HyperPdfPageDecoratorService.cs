// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;
using ReactiveUI.Primitives;

namespace HyperPdf.Engine;

/// <summary>Adds watermarks, headers, footers, page and Bates numbers, a background colour and a crop to the pages of an open document, each as one undo step.</summary>
internal sealed class HyperPdfPageDecoratorService : IPageDecorator
{
    /// <summary>The number of fonts of one family: regular, bold, slanted and both.</summary>
    private const int LooksPerFamily = 4;

    /// <summary>Added to the font number of a family for bold.</summary>
    private const int BoldStep = 1;

    /// <summary>Added to the font number of a family for slanted letters.</summary>
    private const int ItalicStep = 2;

    /// <summary>The largest value of a colour channel, which an 8 bit channel is divided by.</summary>
    private const float ChannelMaximum = 255F;

    /// <summary>The bits a channel is shifted by to reach the red, green and blue of a colour.</summary>
    private const int RedShift = 16;

    /// <summary>The bits a channel is shifted by to reach the green of a colour.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of one 8 bit channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The document.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageDecoratorService"/> class.</summary>
    /// <param name="owner">The document.</param>
    internal HyperPdfPageDecoratorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public IObservable<RxVoid> StampText(ReadOnlyMemory<int> pages, PageTextStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        return LibrarySignals.Run(token => StampTextAsync(pages, stamp, token));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> FillBackground(ReadOnlyMemory<int> pages, uint argb) => LibrarySignals.Run(token => FillBackgroundAsync(pages, argb, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Crop(ReadOnlyMemory<int> pages, PageMargins margins) => LibrarySignals.Run(token => CropAsync(pages, margins, token));

    /// <summary>Stamps text on pages, awaiting the library.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="stamp">The words and how they are drawn.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the pages have changed.</returns>
    internal async ValueTask StampTextAsync(ReadOnlyMemory<int> pages, PageTextStamp stamp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        var selected = await LoadAsync(pages, cancellationToken).ConfigureAwait(false);
        var style = StyleOf(stamp);
        var options = OptionsOf(stamp);
        var today = TimeProvider.System.GetLocalNow();
        Edit("Add text to pages", () =>
        {
            for (var order = 0; order < selected.Length; order++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var index = selected[order];
                var label = PdfDocumentLabels.GetPageLabel(_owner.Document, index) ?? (index + 1).ToString(CultureInfo.InvariantCulture);
                var words = HyperPdfStampText.For(stamp, label, _owner.Document.PageCount, order, today);
                if (words.Length > 0)
                {
                    PdfDocumentWatermarks.AddText(_owner.Document, [index], words, style, options, cancellationToken);
                }
            }
        });
    }

    /// <summary>Fills the background of pages, awaiting the library.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="argb">The colour as an opaque ARGB value.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the pages have changed.</returns>
    internal async ValueTask FillBackgroundAsync(ReadOnlyMemory<int> pages, uint argb, CancellationToken cancellationToken)
    {
        var selected = await LoadAsync(pages, cancellationToken).ConfigureAwait(false);
        var contents = new PdfPageContent[selected.Length];
        for (var i = 0; i < contents.Length; i++)
        {
            contents[i] = await PdfPageContentReader.ReadAsync(_owner.Document, selected[i], cancellationToken).ConfigureAwait(false);
        }

        Edit("Fill the page background", () =>
        {
            foreach (var content in contents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfPageContentEditing.Prepend(content, Fill(content, argb));
                PdfPageContentApplication.Apply(content);
            }
        });
    }

    /// <summary>Crops pages, awaiting the library.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="margins">What to take off each side.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the pages have changed.</returns>
    internal async ValueTask CropAsync(ReadOnlyMemory<int> pages, PageMargins margins, CancellationToken cancellationToken)
    {
        var selected = await LoadAsync(pages, cancellationToken).ConfigureAwait(false);
        Edit("Crop pages", () => PdfDocumentPageOperations.CropPages(_owner.Document, selected, margins.Left, margins.Top, margins.Right, margins.Bottom));
    }

    /// <summary>Makes the library's text style from a stamp.</summary>
    /// <param name="stamp">The stamp.</param>
    /// <returns>The style.</returns>
    private static PdfTextWatermarkStyle StyleOf(PageTextStamp stamp) => new()
    {
        Font = FontFor(stamp),
        FontSize = stamp.FontSize,
        Red = Channel(stamp.Color, RedShift),
        Green = Channel(stamp.Color, GreenShift),
        Blue = Channel(stamp.Color, 0),
    };

    /// <summary>Makes the library's placement options from a stamp.</summary>
    /// <param name="stamp">The stamp.</param>
    /// <returns>The options.</returns>
    private static PdfWatermarkOptions OptionsOf(PageTextStamp stamp) => new()
    {
        Anchor = AnchorFor(stamp.Anchor),
        Margin = stamp.Margin,
        RotationDegrees = stamp.RotationDegrees,
        Opacity = stamp.Opacity,
        Behind = stamp.Behind,
    };

    /// <summary>Gets the font of a stamp.</summary>
    /// <param name="stamp">The stamp.</param>
    /// <returns>The standard font of the stamp's look.</returns>
    private static AppearanceFont FontFor(PageTextStamp stamp) =>
        (AppearanceFont)(((int)stamp.Family * LooksPerFamily) + (stamp.Bold ? BoldStep : 0) + (stamp.Italic ? ItalicStep : 0));

    /// <summary>Gets one channel of an opaque ARGB colour as a share of full strength.</summary>
    /// <param name="argb">The colour.</param>
    /// <param name="shift">How far the channel is shifted from the low end.</param>
    /// <returns>The channel from 0 to 1.</returns>
    private static float Channel(uint argb, int shift) => ((argb >> shift) & ChannelMask) / ChannelMaximum;

    /// <summary>Gets the library's anchor for a stamp's anchor.</summary>
    /// <param name="anchor">The stamp's anchor.</param>
    /// <returns>The library's anchor.</returns>
    private static PdfWatermarkAnchor AnchorFor(PageStampAnchor anchor) => anchor switch
    {
        PageStampAnchor.TopLeft => PdfWatermarkAnchor.TopLeft,
        PageStampAnchor.TopCenter => PdfWatermarkAnchor.TopCenter,
        PageStampAnchor.TopRight => PdfWatermarkAnchor.TopRight,
        PageStampAnchor.BottomLeft => PdfWatermarkAnchor.BottomLeft,
        PageStampAnchor.BottomCenter => PdfWatermarkAnchor.BottomCenter,
        PageStampAnchor.BottomRight => PdfWatermarkAnchor.BottomRight,
        PageStampAnchor.MiddleLeft => PdfWatermarkAnchor.MiddleLeft,
        PageStampAnchor.MiddleRight => PdfWatermarkAnchor.MiddleRight,
        _ => PdfWatermarkAnchor.Center,
    };

    /// <summary>Writes the content that fills a page's crop box with a colour.</summary>
    /// <param name="content">The page.</param>
    /// <param name="argb">The colour.</param>
    /// <returns>The content stream text, which saves the state, fills the box and restores the state.</returns>
    private static byte[] Fill(PdfPageContent content, uint argb)
    {
        var box = content.Page!.CropBox;
        var red = Channel(argb, RedShift);
        var green = Channel(argb, GreenShift);
        var blue = Channel(argb, 0);
        return System.Text.Encoding.ASCII.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"q\n{red:0.###} {green:0.###} {blue:0.###} rg\n{box.Left:0.###} {box.Bottom:0.###} {box.Right - box.Left:0.###} {box.Top - box.Bottom:0.###} re\nf\nQ\n"));
    }

    /// <summary>Loads the pages an edit works on, and checks them.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>The pages, copied.</returns>
    /// <exception cref="ArgumentException">No page is chosen.</exception>
    private async ValueTask<int[]> LoadAsync(ReadOnlyMemory<int> pages, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(pages.Length, nameof(pages));
        var selected = pages.ToArray();
        foreach (var index in selected)
        {
            _ = await PdfDocumentPages.GetPageAsync(_owner.Document, index, cancellationToken).ConfigureAwait(false);
        }

        return selected;
    }

    /// <summary>Runs an edit under the document's edit lock as one undo step.</summary>
    /// <param name="label">What the undo step is called.</param>
    /// <param name="edit">The work.</param>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    private void Edit(string label, Action edit)
    {
        lock (_owner.EditGate)
        {
            ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
            using var access = HyperPdfNavigation.EnterPageWrite(_owner);
            using var transaction = HyperPdfEditTransactions.Begin(_owner.Document, label);
            edit();
            transaction.Commit();
            HyperPdfNavigation.PagesEdited(_owner);
        }
    }
}

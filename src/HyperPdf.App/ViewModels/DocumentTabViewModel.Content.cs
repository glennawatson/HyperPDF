// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Documents;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <content>Tells the reader, once, about content that cannot be shown or run, instead of silently showing fallback pages.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>Gets what the document holds that cannot be shown or run, once checked.</summary>
    [Reactive]
    public partial UnsupportedContent UnsupportedContent { get; private set; }

    /// <summary>Gets the message about content that cannot be shown, until dismissed.</summary>
    [Reactive]
    public partial string? ContentWarning { get; private set; }

    /// <summary>Gets the application services, for the parts of the tab that need settings or desktop services.</summary>
    internal AppServices Services => _services;

    /// <summary>Checks the whole document on a background thread, a page at a time so pages keep drawing meanwhile.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    internal async Task CheckContentAsync(IDocument document)
    {
        if (DocumentFeature<IContentCheck>.Get(document) is not
            {
            } check)
        {
            return;
        }

        var attachments = Attachments.Items.Count;
        var found = await Task.Run(() => Check(check, document, attachments)).ConfigureAwait(true);
        UnsupportedContent = found;
        ContentWarning = ContentWarnings.Describe(found);

        // The scan above read every page's annotations, which may have shown damage the open did not.
        CheckRepairs(document);
    }

    /// <summary>Checks the document and each page.</summary>
    /// <param name="check">The content check.</param>
    /// <param name="document">The document.</param>
    /// <param name="attachments">The attached files.</param>
    /// <returns>What cannot be shown or run.</returns>
    private static UnsupportedContent Check(IContentCheck check, IDocument document, int attachments)
    {
        var content = check.CheckDocument();
        var pages = document.GetPageSizes().Length;
        if (pages > 0 && attachments > 0 && ContentWarnings.LooksLikePortfolio(attachments, document.GetText(0, 0, document.GetCharacterCount(0))))
        {
            content |= UnsupportedContent.Portfolio;
        }

        // Once a page has shown every kind a page can hold, the rest of the pages cannot add anything.
        const UnsupportedContent pageKinds = UnsupportedContent.Multimedia | UnsupportedContent.ThreeD | UnsupportedContent.JavaScript;
        for (var page = 0; page < pages && !document.IsDisposed && (content & pageKinds) != pageKinds; page++)
        {
            content |= check.CheckPage(page);
        }

        return content;
    }

    /// <summary>Puts the content warning away.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DismissContentWarning() => ContentWarning = null;
}

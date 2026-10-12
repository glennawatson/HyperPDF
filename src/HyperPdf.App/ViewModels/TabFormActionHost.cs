// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The tab's part of running a form's actions: it moves between pages, follows links through the tab's own link handling,
/// and refuses to send or import form data, saying so in the tab's notice. Sending data off the computer is never done
/// without the user, and the viewer does not offer it.
/// </summary>
/// <param name="owner">The tab.</param>
/// <param name="allowNavigation">
/// Whether actions may move the reader or open links. Actions that run on their own, such as a page's open action, may
/// not, so a page cannot send the reader somewhere by itself.
/// </param>
[DebuggerDisplay("TabFormActionHost: {owner.FileName}")]
internal sealed class TabFormActionHost(DocumentTabViewModel owner, bool allowNavigation) : IFormActionHost
{
    /// <inheritdoc/>
    public ValueTask<bool> RunNamedAsync(string name, int sourcePage, CancellationToken cancellationToken)
    {
        if (!allowNavigation)
        {
            return ValueTask.FromResult(false);
        }

        var page = sourcePage >= 0 ? sourcePage : owner.CurrentPageIndex;
        int? target = name switch
        {
            "NextPage" => page + 1,
            "PrevPage" => page - 1,
            "FirstPage" => 0,
            "LastPage" => owner.PageCount - 1,
            _ => null,
        };
        if (target is not { } index)
        {
            return ValueTask.FromResult(false);
        }

        owner.GoToPage(index);
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> OpenAsync(LinkTarget target, CancellationToken cancellationToken)
    {
        if (!allowNavigation)
        {
            return ValueTask.FromResult(false);
        }

        owner.Navigate(target);
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> SubmitAsync(FormSubmission submission, CancellationToken cancellationToken)
    {
        owner.Notice = string.IsNullOrEmpty(submission.Url)
            ? "This form asked to send your answers. HyperPDF does not send form data."
            : $"This form asked to send your answers to {submission.Url}. HyperPDF does not send form data.";
        return ValueTask.FromResult(false);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ImportAsync(string? file, CancellationToken cancellationToken)
    {
        owner.Notice = "This form asked to load answers from a file. HyperPDF does not import form data.";
        return ValueTask.FromResult(false);
    }
}

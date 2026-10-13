// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Forms;

/// <summary>Async operations over shared form state.</summary>
public static class PdfFormAsync
{
    /// <summary>Rebuilds the appearance of a text, choice or push button field from its value and appearance characteristics.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "index">The widget's index in the page's annotations.</param>
    /// <param name = "cancellationToken">A token that cancels the drawing; it is checked for every widget, line and row.</param>
    /// <returns><see langword="true"/> when the widget is a text, choice or push button field.</returns>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<bool> RegenerateAppearanceAsync(PdfForm form, int pageIndex, int index, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(PdfFormEditing.RegenerateAppearance(form, pageIndex, index, cancellationToken));
    }

    /// <summary>Resets form fields to their default values.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "action">The action.</param>
    /// <param name = "cancellationToken">A token that cancels the reset before it starts.</param>
    /// <returns>The number of fields reset.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "action"/> is <see langword="null"/>.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<int> ResetAsync(PdfForm form, ResetFormAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(PdfFormActions.Reset(form, action, cancellationToken));
    }

    /// <summary>Shows or hides the fields a hide action names.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "action">The action.</param>
    /// <param name = "cancellationToken">A token that cancels the change before it starts.</param>
    /// <returns>The number of annotations changed.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "action"/> is <see langword="null"/>.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<int> SetHiddenAsync(PdfForm form, HideAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(PdfFormActions.SetHidden(form, action, cancellationToken));
    }

    /// <summary>Collects the data a submit-form action would send. Nothing is sent.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "action">The action.</param>
    /// <param name = "cancellationToken">A token that cancels the collection before it starts.</param>
    /// <returns>The submission.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "action"/> is <see langword="null"/>.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfFormSubmission> CreateSubmissionAsync(PdfForm form, SubmitFormAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(PdfFormActions.CreateSubmission(form, action, cancellationToken));
    }

    /// <summary>Reads the order calculated fields are recalculated in.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "cancellationToken">A token that cancels the read before it starts.</param>
    /// <returns>The field names in calculation order.</returns>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<string[]> GetCalculationOrderAsync(PdfForm form, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<string> names = [];
        PdfFormOrder.GetCalculationOrder(form, names, cancellationToken);
        return ValueTask.FromResult(names.ToArray());
    }

    /// <summary>Reads the order the Tab key visits a page's form widgets.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "cancellationToken">A token that cancels the read before it starts.</param>
    /// <returns>The widget indexes in tab order.</returns>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<int[]> GetTabSequenceAsync(PdfForm form, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<int> order = [];
        PdfFormOrder.GetTabSequence(form, pageIndex, order, cancellationToken);
        return ValueTask.FromResult(order.ToArray());
    }
}

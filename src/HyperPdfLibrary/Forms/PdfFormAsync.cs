// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Async operations over shared form state.</summary>
public static class PdfFormAsync
{
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
        using var scope = PdfCancellation.Enter(cancellationToken);
        return ValueTask.FromResult(PdfFormActions.Reset(form, action));
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
        using var scope = PdfCancellation.Enter(cancellationToken);
        return ValueTask.FromResult(PdfFormActions.SetHidden(form, action));
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
        using var scope = PdfCancellation.Enter(cancellationToken);
        return ValueTask.FromResult(PdfFormActions.CreateSubmission(form, action));
    }

    /// <summary>Reads the order calculated fields are recalculated in.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "cancellationToken">A token that cancels the read before it starts.</param>
    /// <returns>The field names in calculation order.</returns>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<string[]> GetCalculationOrderAsync(PdfForm form, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var scope = PdfCancellation.Enter(cancellationToken);
        List<string> names = [];
        PdfFormOrder.GetCalculationOrder(form, names);
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
        using var scope = PdfCancellation.Enter(cancellationToken);
        List<int> order = [];
        PdfFormOrder.GetTabSequence(form, pageIndex, order);
        return ValueTask.FromResult(order.ToArray());
    }
}

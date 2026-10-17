// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Editing;

/// <summary>What flattening does with one annotation and, when it does not draw it, why.</summary>
/// <param name="Action">What happens to the annotation.</param>
/// <param name="HasReason">Whether the annotation is not drawn for a reason worth reporting.</param>
/// <param name="Reason">The reason, when <paramref name="HasReason"/> is set.</param>
[DebuggerDisplay("PdfFlattenDecision: {Action} {Reason}")]
internal readonly record struct PdfFlattenDecision(PdfFlattenAction Action, bool HasReason, PdfFlattenSkipReason Reason)
{
    /// <summary>Gets the decision to draw the annotation.</summary>
    internal static PdfFlattenDecision Draw => new(PdfFlattenAction.Draw, false, default);

    /// <summary>Gets the decision to leave an annotation alone that flattening was not asked to touch.</summary>
    internal static PdfFlattenDecision OutOfScope => new(PdfFlattenAction.Keep, false, default);

    /// <summary>Makes the decision to leave an annotation on the page.</summary>
    /// <param name="reason">Why it is not drawn.</param>
    /// <returns>The decision.</returns>
    internal static PdfFlattenDecision Keep(PdfFlattenSkipReason reason) => new(PdfFlattenAction.Keep, true, reason);

    /// <summary>Makes the decision to remove an annotation without drawing it.</summary>
    /// <param name="reason">Why it is not drawn.</param>
    /// <returns>The decision.</returns>
    internal static PdfFlattenDecision Remove(PdfFlattenSkipReason reason) => new(PdfFlattenAction.Remove, true, reason);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Annotations;

/// <summary>The result of checking one page's trap network.</summary>
/// <param name="Problems">What is wrong with the network.</param>
/// <param name="Touched">Whether an edit since opening changed the page, its annotations, or an object the network names.</param>
[DebuggerDisplay("PdfTrapNetworkCheck: {Problems}, touched {Touched}")]
internal readonly record struct PdfTrapNetworkCheck(PdfTrapNetworkProblems Problems, bool Touched)
{
    /// <summary>The problems that mean the network no longer matches its page or cannot be checked, so it is removed.</summary>
    internal const PdfTrapNetworkProblems RemovalProblems = PdfTrapNetworkProblems.MultipleNetworks | PdfTrapNetworkProblems.MissingAppearance
        | PdfTrapNetworkProblems.MissingChangeTracking | PdfTrapNetworkProblems.ContentChanged | PdfTrapNetworkProblems.AnnotationsChanged
        | PdfTrapNetworkProblems.PageNewer | PdfTrapNetworkProblems.ReferencesLost;

    /// <summary>Gets a value indicating whether the network must be removed rather than fixed.</summary>
    internal bool MustRemove => (Problems & RemovalProblems) != 0;
}

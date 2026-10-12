// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The state of the PDF/A text walk over a whole document: the fonts found and the streams already read.</summary>
[DebuggerDisplay("PdfATextWalkState: {Fonts.Count} fonts, {Visited.Count} streams")]
internal sealed class PdfATextWalkState
{
    /// <summary>Initializes a new instance of the <see cref="PdfATextWalkState"/> class.</summary>
    /// <param name="validation">The validation the walk serves.</param>
    internal PdfATextWalkState(PdfAValidationContext validation)
    {
        Validation = validation;
        Store = validation.Document.Objects;
    }

    /// <summary>Gets the validation.</summary>
    internal PdfAValidationContext Validation { get; }

    /// <summary>Gets the document's objects.</summary>
    internal PdfObjectStore Store { get; }

    /// <summary>Gets or sets the resources of the page being walked; names missing from a stream's own resources resolve here.</summary>
    internal PdfDictionary? PageResources { get; set; }

    /// <summary>Gets or sets the nesting depth of forms, patterns and glyph procedures.</summary>
    internal int Depth { get; set; }

    /// <summary>Gets each dictionary looked up as a font, with its use, or null when it is not a font the rules know.</summary>
    internal Dictionary<PdfDictionary, PdfAFontUse?> Fonts { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Gets the fonts in the order the walk first showed text with them.</summary>
    internal List<PdfAFontUse> Used { get; } = [];

    /// <summary>Gets the streams already read with a given starting state.</summary>
    internal HashSet<PdfAContentVisit> Visited { get; } = [];

    /// <summary>Gets the page resources and the own resources of every stream read.</summary>
    internal HashSet<PdfDictionary> Resources { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Gets or sets a value indicating whether a marked-content <c>/ActualText</c> holds a private use character.</summary>
    internal bool PrivateUseActualText { get; set; }
}

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
    /// <summary>The number of latest resources a new one is compared with before it is recorded.</summary>
    private const int RecentWindow = 8;

    /// <summary>The resources read, in order, without repeats of the latest few.</summary>
    private readonly List<PdfDictionary> _read = [];

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

    /// <summary>Gets or sets a value indicating whether a marked-content <c>/ActualText</c> holds a private use character.</summary>
    internal bool PrivateUseActualText { get; set; }

    /// <summary>
    /// Gets the page resources and the own resources of every stream read, each once, in the order first read. The set is
    /// built when asked for: giving each new dictionary an identity hash is costly, and a walk that only reports font
    /// facts never needs the set.
    /// </summary>
    /// <returns>A new set.</returns>
    internal HashSet<PdfDictionary> GetDistinctResources()
    {
        var distinct = new HashSet<PdfDictionary>(_read.Count, ReferenceEqualityComparer.Instance);
        foreach (var resources in _read)
        {
            _ = distinct.Add(resources);
        }

        return distinct;
    }

    /// <summary>Records resources the walk read.</summary>
    /// <param name="resources">The resource dictionary.</param>
    internal void AddResources(PdfDictionary resources)
    {
        // Pages and streams that share a dictionary come close together, so a repeat of a recent one is dropped here; the
        // set drops the rest.
        var start = Math.Max(0, _read.Count - RecentWindow);
        for (var i = _read.Count - 1; i >= start; i--)
        {
            if (ReferenceEquals(_read[i], resources))
            {
                return;
            }
        }

        _read.Add(resources);
    }
}

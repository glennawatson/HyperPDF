// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The state of the PDF/A colour and image walk. The walk visits objects in the order veraPDF does, and some rules read
/// values an earlier object set, such as the output intent's colour space, so the state holds them.
/// </summary>
[DebuggerDisplay("PdfAColourState: part {Context.Part}, {Visited.Count} objects visited")]
internal sealed class PdfAColourState
{
    /// <summary>Initializes a new instance of the <see cref="PdfAColourState"/> class.</summary>
    /// <param name="context">The validation.</param>
    internal PdfAColourState(PdfAValidationContext context)
    {
        Context = context;
        Store = context.Document.Objects;
        Names = PdfAColourNames.Create(Store.Names);
    }

    /// <summary>Gets the validation.</summary>
    internal PdfAValidationContext Context { get; }

    /// <summary>Gets the document's objects.</summary>
    internal PdfObjectStore Store { get; }

    /// <summary>Gets the names the checks use.</summary>
    internal PdfAColourNames Names { get; }

    /// <summary>Gets the objects visited, so each is checked once in each role.</summary>
    internal HashSet<PdfAVisitKey> Visited { get; } = [];

    /// <summary>Gets the Separation spaces seen for each colourant name.</summary>
    internal Dictionary<PdfName, List<PdfAColourSpace>> Separations { get; } = [];

    /// <summary>Gets the colourant names whose Separation spaces disagree.</summary>
    internal HashSet<PdfName> InconsistentSeparations { get; } = [];

    /// <summary>Gets or sets the colour space of the last PDF/A output intent profile seen, for parts 1 to 3.</summary>
    internal string? OutputSpace { get; set; }

    /// <summary>Gets or sets the colour space of the document's PDF/A output intent, for part 4.</summary>
    internal string? DocumentOutputSpace { get; set; }

    /// <summary>Gets or sets the colour space of the current page's PDF/A output intent, for part 4.</summary>
    internal string? PageOutputSpace { get; set; }

    /// <summary>Gets or sets the type of the current transparency blending space, for part 4.</summary>
    internal string? TransparencySpace { get; set; }

    /// <summary>Gets or sets the current transparency blending colour space.</summary>
    internal PdfAColourSpace? CurrentBlendingSpace { get; set; }

    /// <summary>Gets or sets the destination profile of the last output intent seen.</summary>
    internal PdfStream? OutputProfile { get; set; }

    /// <summary>Gets or sets the object of the last output intent's destination profile, when it is indirect.</summary>
    internal PdfObjectId OutputProfileId { get; set; }

    /// <summary>Gets or sets how deeply colour spaces are nested, to stop cycles.</summary>
    internal int Depth { get; set; }

    /// <summary>Gets or sets how deeply forms are nested, to stop runaway nesting.</summary>
    internal int FormDepth { get; set; }

    /// <summary>Gets or sets how many operators have been read since the last cancellation check.</summary>
    internal int OperatorsSinceCheck { get; set; }
}

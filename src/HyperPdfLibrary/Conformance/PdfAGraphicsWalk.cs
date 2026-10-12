// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The state of one walk over the pages, content streams and the graphics objects they use. Like veraPDF, it checks each
/// indirect content stream and graphics object once, the first time the walk reaches it.
/// </summary>
[DebuggerDisplay("PdfAGraphicsWalk: {_checked.Count} checked, depth {Depth}")]
internal sealed class PdfAGraphicsWalk
{
    /// <summary>The objects already checked, by identity.</summary>
    private readonly HashSet<object> _checked = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Initializes a new instance of the <see cref="PdfAGraphicsWalk"/> class.</summary>
    /// <param name="context">The validation.</param>
    internal PdfAGraphicsWalk(PdfAValidationContext context)
    {
        Context = context;
        Store = context.Document.Objects;
        Names = PdfAGraphicsNames.Create(Store.Names);
    }

    /// <summary>Gets the validation.</summary>
    internal PdfAValidationContext Context { get; }

    /// <summary>Gets the document's objects.</summary>
    internal PdfObjectStore Store { get; }

    /// <summary>Gets the names the rules read.</summary>
    internal PdfAGraphicsNames Names { get; }

    /// <summary>Gets or sets how deeply nested content the walk is reading.</summary>
    internal int Depth { get; set; }

    /// <summary>Records an object as checked.</summary>
    /// <param name="item">The object.</param>
    /// <returns><see langword="true"/> the first time; <see langword="false"/> when it was already checked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool FirstVisit(object item) => _checked.Add(item);
}

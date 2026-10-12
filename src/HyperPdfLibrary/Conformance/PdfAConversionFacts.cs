// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>What a PDF/A conversion learned from reading the document's content and objects before changing them.</summary>
[DebuggerDisplay("PdfAConversionFacts: RGB {UsesRgb}, CMYK {UsesCmyk}, gray {UsesGray}")]
internal sealed class PdfAConversionFacts
{
    /// <summary>Gets or sets a value indicating whether device RGB colour is used without a default colour space.</summary>
    internal bool UsesRgb { get; set; }

    /// <summary>Gets or sets a value indicating whether device CMYK colour is used without a default colour space.</summary>
    internal bool UsesCmyk { get; set; }

    /// <summary>Gets or sets a value indicating whether device gray is used without a default colour space.</summary>
    internal bool UsesGray { get; set; }

    /// <summary>Gets or sets the number of operators the PDF specification does not define.</summary>
    internal int UndefinedOperators { get; set; }

    /// <summary>Gets or sets the deepest nesting of <c>q</c> operators.</summary>
    internal int MaxSaveDepth { get; set; }

    /// <summary>Gets or sets the number of <c>ri</c> operators with a rendering intent the specification does not define.</summary>
    internal int UnknownIntents { get; set; }

    /// <summary>Gets or sets the number of inline images compressed with LZW or a crypt filter.</summary>
    internal int InlineForbiddenFilters { get; set; }

    /// <summary>Gets or sets the number of inline images that ask for interpolation.</summary>
    internal int InlineInterpolated { get; set; }

    /// <summary>Gets or sets the number of content streams that could not be read.</summary>
    internal int UnreadableStreams { get; set; }
}

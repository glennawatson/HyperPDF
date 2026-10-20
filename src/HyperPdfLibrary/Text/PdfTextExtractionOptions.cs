// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>Opt-in choices for page text extraction. The defaults read the page as it is drawn, and join hyphenated words.</summary>
/// <param name="SkipNonVisibleText">Whether text that cannot be seen is left out: text clipped away, fully transparent, or in a hidden group.</param>
/// <param name="StripFormattingControls">Whether invisible formatting controls such as zero-width joiners are removed from the text.</param>
/// <param name="HyphenMode">What to do with a hyphen that ends a line inside a word.</param>
/// <param name="Order">The order to read the page in.</param>
[DebuggerDisplay("SkipNonVisibleText: {SkipNonVisibleText}, StripFormattingControls: {StripFormattingControls}, {HyphenMode}, {Order}")]
public readonly record struct PdfTextExtractionOptions(bool SkipNonVisibleText, bool StripFormattingControls, PdfTextHyphenMode HyphenMode, PdfTextOrder Order)
{
    /// <summary>Initializes a new instance of the <see cref="PdfTextExtractionOptions"/> struct with the default hyphen mode and order.</summary>
    /// <param name="skipNonVisibleText">Whether text that cannot be seen is left out.</param>
    /// <param name="stripFormattingControls">Whether invisible formatting controls are removed from the text.</param>
    public PdfTextExtractionOptions(bool skipNonVisibleText, bool stripFormattingControls)
        : this(skipNonVisibleText, stripFormattingControls, PdfTextHyphenMode.Join, PdfTextOrder.Content)
    {
    }
}

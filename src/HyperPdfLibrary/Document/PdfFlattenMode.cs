// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Which annotation flags decide what flattening draws (ISO 32000-2 12.5.3).</summary>
public enum PdfFlattenMode
{
    /// <summary>Draws what a viewer shows on screen: annotations without the Hidden and NoView flags.</summary>
    Display = 0,

    /// <summary>Draws what a printer prints: annotations with the Print flag and without the Hidden flag.</summary>
    Print = 1,
}

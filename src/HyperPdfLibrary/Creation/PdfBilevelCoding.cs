// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How a one-bit image is compressed with the CCITTFaxDecode filter (ISO 32000-2 7.4.6).</summary>
public enum PdfBilevelCoding
{
    /// <summary>Group 4 (Recommendation T.6, <c>/K -1</c>): every row is coded against the row above it. Usually the smallest.</summary>
    Group4 = 0,

    /// <summary>Group 3 one-dimensional (Recommendation T.4, <c>/K 0</c>): every row is coded alone, as runs of white and black.</summary>
    Group3OneDimensional = 1,

    /// <summary>Group 3 mixed (Recommendation T.4, <c>/K 4</c>): every fourth row is coded alone and the rows between are coded against the row above.</summary>
    Group3TwoDimensional = 2,
}

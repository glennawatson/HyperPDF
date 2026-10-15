// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>The text colour a default appearance sets; the default value is black.</summary>
/// <param name="Kind">The colour operator.</param>
/// <param name="First">The grey level, red or cyan.</param>
/// <param name="Second">The green or magenta.</param>
/// <param name="Third">The blue or yellow.</param>
/// <param name="Fourth">The black of a CMYK colour.</param>
[DebuggerDisplay("DaColor: {Kind} {First} {Second} {Third} {Fourth}")]
internal readonly record struct DaColor(DaColorKind Kind, float First, float Second, float Third, float Fourth)
{
    /// <summary>Sets the colour as the fill colour.</summary>
    /// <param name="builder">The content.</param>
    internal void WriteFill(ref PdfContentBuilder builder)
    {
        switch (Kind)
        {
            case DaColorKind.Rgb:
            {
                builder.SetFillRgb(First, Second, Third);
                break;
            }

            case DaColorKind.Cmyk:
            {
                builder.SetFillCmyk(First, Second, Third, Fourth);
                break;
            }

            default:
            {
                builder.SetFillGray(First);
                break;
            }
        }
    }

    /// <summary>Sets the colour as the stroke colour.</summary>
    /// <param name="builder">The content.</param>
    internal void WriteStroke(ref PdfContentBuilder builder)
    {
        switch (Kind)
        {
            case DaColorKind.Rgb:
            {
                builder.SetStrokeRgb(First, Second, Third);
                break;
            }

            case DaColorKind.Cmyk:
            {
                builder.SetStrokeCmyk(First, Second, Third, Fourth);
                break;
            }

            default:
            {
                builder.SetStrokeGray(First);
                break;
            }
        }
    }
}

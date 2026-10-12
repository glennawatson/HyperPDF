// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Records the device colour a content operator uses, unless a default colour space replaces it.</summary>
internal static class PdfAConversionOperatorColours
{
    /// <summary>Records an operator's colour use.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="op">The operator.</param>
    /// <param name="first">Its first operand.</param>
    /// <param name="defaults">The default colour spaces in force.</param>
    /// <param name="facts">The facts.</param>
    internal static void Mark(PdfNameTable names, ContentOperator op, in ContentOperand first, PdfAConversionDefaultSpaces defaults, PdfAConversionFacts facts)
    {
        switch (op)
        {
            case ContentOperator.SetFillRgb or ContentOperator.SetStrokeRgb:
                {
                    facts.UsesRgb |= !defaults.Rgb;
                    break;
                }

            case ContentOperator.SetFillCmyk or ContentOperator.SetStrokeCmyk:
                {
                    facts.UsesCmyk |= !defaults.Cmyk;
                    break;
                }

            case ContentOperator.SetFillGray or ContentOperator.SetStrokeGray:
                {
                    facts.UsesGray |= !defaults.Gray;
                    break;
                }

            case ContentOperator.SetFillColorSpace or ContentOperator.SetStrokeColorSpace:
                {
                    PdfAConversionSpaceColours.MarkName(first.Name.ToKnownName(), defaults, facts);
                    break;
                }

            case ContentOperator.SetRenderingIntent:
                {
                    facts.UnknownIntents += PdfAConversionSpaceColours.IsKnownIntent(names.GetString(first.Name)) ? 0 : 1;
                    break;
                }

            default:
                {
                    break;
                }
        }
    }
}

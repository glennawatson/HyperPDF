// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks a use of DeviceGray, DeviceRGB or DeviceCMYK. Parts 1 to 3 need a PDF/A output intent of the same family
/// (any output intent for grey). Part 4 also accepts a page output intent or a matching transparency blending space.
/// </summary>
internal static class PdfADeviceChecks
{
    /// <summary>The PDF/A part whose rules read page output intents and blending spaces.</summary>
    private const int Part4 = 4;

    /// <summary>The blending space type of a CalRGB group, which part 4 accepts for DeviceRGB.</summary>
    private const string CalRgbText = "CalRGB";

    /// <summary>Checks a use of a device colour space.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="family">The device family.</param>
    internal static void Check(PdfAColourState state, KnownName family)
    {
        if (state.Context.Part == Part4)
        {
            CheckPart4(state, family);
            return;
        }

        var output = state.OutputSpace;
        switch (family)
        {
            case KnownName.DeviceRGB:
            {
                state.Context.Require(output == PdfAIccProfiles.RgbText, PdfARule.DeviceRgb, null);
                break;
            }

            case KnownName.DeviceCMYK:
            {
                state.Context.Require(output == PdfAIccProfiles.CmykText, PdfARule.DeviceCmyk, null);
                break;
            }

            default:
            {
                state.Context.Require(output is not null, PdfARule.DeviceGray, null);
                break;
            }
        }
    }

    /// <summary>Checks a use of a device colour space against the part 4 rules.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="family">The device family.</param>
    private static void CheckPart4(PdfAColourState state, KnownName family)
    {
        var output = state.PageOutputSpace ?? state.DocumentOutputSpace;
        var blending = state.TransparencySpace;
        switch (family)
        {
            case KnownName.DeviceRGB:
            {
                var holds = output == PdfAIccProfiles.RgbText || blending == PdfAIccProfiles.RgbText || blending == CalRgbText;
                state.Context.Require(holds, PdfARule.DeviceRgbPdfA4, null);
                break;
            }

            case KnownName.DeviceCMYK:
            {
                var holds = output == PdfAIccProfiles.CmykText || blending == PdfAIccProfiles.CmykText;
                state.Context.Require(holds, PdfARule.DeviceCmykPdfA4, null);
                break;
            }

            default:
            {
                state.Context.Require(output is not null, PdfARule.DeviceGrayPdfA4, null);
                break;
            }
        }
    }
}

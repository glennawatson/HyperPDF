// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks the colour space a JPEG 2000 image declares when its dictionary has no <c>/ColorSpace</c>.</summary>
internal static class PdfAJpxSpaceChecks
{
    /// <summary>The PDF/A part whose rules compare ICC profiles with the output intent.</summary>
    private const int Part4 = 4;

    /// <summary>The components of a CMYK space.</summary>
    private const int CmykComponents = 4;

    /// <summary>Visits a declared space: CMYK is device CMYK (or DefaultCMYK), an embedded profile is an ICCBased space.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The declared space.</param>
    /// <param name="scope">The resources of the content that paints the image.</param>
    internal static void Visit(PdfAColourState state, PdfAJpxSpace space, in PdfAColourResourceScope scope)
    {
        if (space.Family == KnownName.DeviceCMYK)
        {
            PdfAColourChecks.Visit(state, PdfAResources.ColourSpace(state, scope, KnownName.DeviceCMYK));
            return;
        }

        if (space.Family != KnownName.ICCBased || (space.Components == CmykComponents && !PdfAColourChecks.FirstDirectCmykVisit(state)))
        {
            return;
        }

        if (space.Profile is not { } profile)
        {
            return;
        }

        if (space.Components == CmykComponents && state.Context.Part == Part4)
        {
            PdfAColourChecks.CheckProfileReuse(state, PdfAIccProfiles.Identity(profile), default);
        }

        PdfAIccChecks.CheckInputProfile(state, PdfAIccProfiles.Header(profile), PdfValue.FromInteger(space.Components));
    }
}

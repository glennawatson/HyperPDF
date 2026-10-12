// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks ICC profiles: the profiles of ICCBased colour spaces and the destination profiles of output intents.</summary>
internal static class PdfAIccChecks
{
    /// <summary>The first PDF/A part, which allows only ICC version 2 profiles.</summary>
    private const int Part1 = 1;

    /// <summary>The PDF/A part with its own output intent rules.</summary>
    private const int Part4 = 4;

    /// <summary>The first ICC major version part 1 rejects.</summary>
    private const int Part1VersionLimit = 3;

    /// <summary>The first ICC major version parts 2 to 4 reject.</summary>
    private const int VersionLimit = 5;

    /// <summary>The components of a grey profile.</summary>
    private const long GrayComponents = 1;

    /// <summary>The components of an RGB or Lab profile.</summary>
    private const long RgbComponents = 3;

    /// <summary>The components of a CMYK profile.</summary>
    private const long CmykComponents = 4;

    /// <summary>Checks the profile of an ICCBased colour space.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="profile">The profile stream.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckInputProfile(PdfAColourState state, PdfStream profile) =>
        CheckInputProfile(state, PdfAIccProfiles.ReadHeader(state, profile), profile.Dictionary.Get(KnownName.N));

    /// <summary>Checks an input profile from its header and declared component count.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="header">The profile header.</param>
    /// <param name="components">The <c>/N</c> value; null when missing.</param>
    internal static void CheckInputProfile(PdfAColourState state, in PdfAIccHeader header, PdfValue components)
    {
        var holds = IsInputClass(header.DeviceClass) && IsInputSpace(header.ColourSpace) && IsVersionAllowed(state, header);
        state.Context.Require(holds, state.Context.Part == Part1 ? PdfARule.IccInputProfilePdfA1 : PdfARule.IccInputProfile, null);
        CheckComponents(state, components, header);
    }

    /// <summary>Checks the destination profile of an output intent and records its colour space.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="profile">The profile stream.</param>
    /// <param name="isPdfAIntent">Whether the output intent is a PDF/A one.</param>
    internal static void CheckOutputProfile(PdfAColourState state, PdfStream profile, bool isPdfAIntent)
    {
        var header = PdfAIccProfiles.ReadHeader(state, profile);
        var holds = header.DeviceClass is PdfAIccProfiles.Printer or PdfAIccProfiles.Monitor
            && header.ColourSpace is PdfAIccProfiles.Rgb or PdfAIccProfiles.Cmyk or PdfAIccProfiles.Gray
            && IsVersionAllowed(state, header);
        state.Context.Require(holds, state.Context.Part == Part1 ? PdfARule.IccOutputProfilePdfA1 : PdfARule.IccOutputProfile, null);
        CheckComponents(state, profile.Dictionary.Get(KnownName.N), header);
        if (isPdfAIntent && state.Context.Part != Part4)
        {
            state.OutputSpace = PdfAIccProfiles.SpaceText(header.ColourSpace);
        }
    }

    /// <summary>Checks, for part 1, that the profile's <c>/N</c> matches its colour space.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="components">The <c>/N</c> value; null when missing.</param>
    /// <param name="header">The profile header.</param>
    private static void CheckComponents(PdfAColourState state, PdfValue components, in PdfAIccHeader header)
    {
        if (state.Context.Part != Part1)
        {
            return;
        }

        var holds = components.IsNumber && components.AsInteger() switch
        {
            GrayComponents => header.ColourSpace == PdfAIccProfiles.Gray,
            RgbComponents => header.ColourSpace is PdfAIccProfiles.Rgb or PdfAIccProfiles.Lab,
            CmykComponents => header.ColourSpace == PdfAIccProfiles.Cmyk,
            _ => false,
        };
        state.Context.Require(holds, PdfARule.IccProfileComponents, null);
    }

    /// <summary>Determines whether a device class is one an input profile may have.</summary>
    /// <param name="deviceClass">The class signature.</param>
    /// <returns><see langword="true"/> for printer, monitor, scanner and colour space profiles.</returns>
    private static bool IsInputClass(uint deviceClass) =>
        deviceClass is PdfAIccProfiles.Printer or PdfAIccProfiles.Monitor or PdfAIccProfiles.Scanner or PdfAIccProfiles.ColourSpaceClass;

    /// <summary>Determines whether a data colour space is one an input profile may have.</summary>
    /// <param name="space">The colour space signature.</param>
    /// <returns><see langword="true"/> for RGB, CMYK, grey and Lab.</returns>
    private static bool IsInputSpace(uint space) =>
        space is PdfAIccProfiles.Rgb or PdfAIccProfiles.Cmyk or PdfAIccProfiles.Gray or PdfAIccProfiles.Lab;

    /// <summary>Determines whether the profile version is one the part allows. A header too short to hold a version passes, as in veraPDF.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="header">The header.</param>
    /// <returns><see langword="true"/> when the version is allowed.</returns>
    private static bool IsVersionAllowed(PdfAColourState state, in PdfAIccHeader header) =>
        header.MajorVersion < (state.Context.Part == Part1 ? Part1VersionLimit : VersionLimit);
}

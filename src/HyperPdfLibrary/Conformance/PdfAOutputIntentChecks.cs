// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks an <c>/OutputIntents</c> array and its output intents and destination profiles (ISO 19005-2 6.2.3).</summary>
internal static class PdfAOutputIntentChecks
{
    /// <summary>The PDF/A part with its own output intent rules.</summary>
    private const int Part4 = 4;

    /// <summary>Checks an output intents array and visits each output intent's profile.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="intents">The array.</param>
    internal static void Check(PdfAColourState state, PdfArray intents)
    {
        CheckArray(state, intents);
        for (var i = 0; i < intents.Count; i++)
        {
            if (intents.GetDictionary(i) is { } intent)
            {
                CheckIntent(state, intent);
            }
        }
    }

    /// <summary>Gets the colour space of the first PDF/A output intent that has a destination profile.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="intents">The array, or null.</param>
    /// <returns>The profile's data colour space, or null.</returns>
    internal static string? OutputSpace(PdfAColourState state, PdfArray? intents)
    {
        for (var i = 0; intents is not null && i < intents.Count; i++)
        {
            if (intents.GetDictionary(i) is { } intent && IsPdfA(state, intent)
                && intent.GetStream(state.Names.DestOutputProfile) is { } profile
                && PdfAIccProfiles.SpaceText(PdfAIccProfiles.ReadHeader(state, profile).ColourSpace) is { } space)
            {
                return space;
            }
        }

        return null;
    }

    /// <summary>Checks that the output intents with an indirect destination profile share one profile object.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="intents">The array.</param>
    private static void CheckArray(PdfAColourState state, PdfArray intents)
    {
        var shared = default(PdfObjectId);
        var same = true;
        for (var i = 0; i < intents.Count; i++)
        {
            var profile = intents.GetDictionary(i)?.GetRaw(state.Names.DestOutputProfile) ?? default;
            if (!profile.IsReference)
            {
                continue;
            }

            same &= !shared.IsValid || shared == profile.AsReference();
            shared = shared.IsValid ? shared : profile.AsReference();
        }

        state.Context.Require(same, PdfARule.OutputIntentSameProfile, null);
    }

    /// <summary>Checks one output intent and its destination profile, and records the profile for later rules.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="intent">The output intent.</param>
    private static void CheckIntent(PdfAColourState state, PdfDictionary intent)
    {
        var hasReference = intent.ContainsKey(state.Names.DestOutputProfileRef);
        if (state.Context.Part == Part4)
        {
            state.Context.Require(!hasReference, PdfARule.OutputIntentProfileReference, null);
        }
        else
        {
            state.Context.Require(!hasReference || intent.Get(KnownName.S).AsName() != state.Names.PdfXIntent, PdfARule.OutputIntentProfileReferencePdfX, null);
        }

        var raw = intent.GetRaw(state.Names.DestOutputProfile);
        var profile = StoreReading.Resolve(state.Store, raw).AsStream();
        state.OutputProfile = profile;
        state.OutputProfileId = raw.IsReference ? raw.AsReference() : default;
        if (profile is not null)
        {
            PdfAIccChecks.CheckOutputProfile(state, profile, IsPdfA(state, intent));
        }
    }

    /// <summary>Determines whether an output intent is a PDF/A one.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="intent">The output intent.</param>
    /// <returns><see langword="true"/> when its <c>/S</c> is <c>/GTS_PDFA1</c>.</returns>
    private static bool IsPdfA(PdfAColourState state, PdfDictionary intent) => intent.Get(KnownName.S).AsName() == state.Names.PdfAIntent;
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Gives a converted file one PDF/A output intent that covers its device colour (ISO 19005-1 §6.2.2; ISO 19005-2 and -3
/// §6.2.3; ISO 19005-4:2020 §6.2.3). An existing valid PDF/A intent is kept. Otherwise a CMYK profile another intent
/// already carries is reused for CMYK content, and anything else gets a generated sRGB profile. Other intents that point
/// at a different profile are removed, as all intents must share one.
/// </summary>
internal static class PdfAConversionOutputIntent
{
    /// <summary>The colour space signature of an RGB profile.</summary>
    internal const string Rgb = "RGB ";

    /// <summary>The colour space signature of a CMYK profile.</summary>
    private const string Cmyk = "CMYK";

    /// <summary>The size of an ICC profile header.</summary>
    private const int IccHeaderSize = 128;

    /// <summary>The offset of the major version byte in an ICC header.</summary>
    private const int VersionOffset = 8;

    /// <summary>The offset of the device class in an ICC header.</summary>
    private const int ClassOffset = 12;

    /// <summary>The offset of the colour space in an ICC header.</summary>
    private const int SpaceOffset = 16;

    /// <summary>The newest ICC major version PDF/A-1 accepts.</summary>
    private const int Pdf1MaxVersion = 2;

    /// <summary>The newest ICC major version the later parts accept.</summary>
    private const int MaxVersion = 4;

    /// <summary>The length of an ICC signature.</summary>
    private const int SignatureLength = 4;

    /// <summary>The entries of an output intent dictionary.</summary>
    private const int IntentEntries = 6;

    /// <summary>The components of an RGB profile.</summary>
    private const int RgbComponents = 3;

    /// <summary>Gets the output intent type of PDF/A.</summary>
    private static ReadOnlySpan<byte> PdfAIntent => "GTS_PDFA1"u8;

    /// <summary>Gets the key of an output intent's profile.</summary>
    private static ReadOnlySpan<byte> ProfileKey => "DestOutputProfile"u8;

    /// <summary>Gives the file its output intent.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="facts">What the scan found.</param>
    /// <returns>The colour space of the PDF/A intent's profile, such as "RGB ", or <see langword="null"/> when none could be set.</returns>
    internal static string? Apply(PdfAConversionSession session, PdfAConversionFacts facts)
    {
        var store = session.Store;
        var key = store.Names.Intern("OutputIntents"u8);
        var intents = StoreReading.Resolve(store, store.Catalog.GetRaw(key)).AsArray();
        var pdfa = FindPdfAIntent(session, intents, out var profileRef, out var space);
        if (pdfa is null || !Covers(facts, space))
        {
            // A missing intent, or one that cannot describe the colour the pages use, is replaced by a matching one.
            pdfa = NewIntent(session, facts, intents, out profileRef, out space);
        }

        if (pdfa is null)
        {
            return null;
        }

        WriteIntents(session, key, intents, pdfa, profileRef);
        return space;
    }

    /// <summary>Creates an intent for the colour the pages use, or refuses when no profile can describe it.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="facts">What the scan found.</param>
    /// <param name="intents">The old intents, or <see langword="null"/>.</param>
    /// <param name="profileRef">Receives the profile reference.</param>
    /// <param name="space">Receives the profile's colour space.</param>
    /// <returns>The intent, or <see langword="null"/> when refused.</returns>
    private static PdfDictionary? NewIntent(PdfAConversionSession session, PdfAConversionFacts facts, PdfArray? intents, out PdfValue profileRef, out string? space)
    {
        profileRef = default;
        space = null;
        if (facts.UsesCmyk && facts.UsesRgb)
        {
            session.Refuse(
                "The pages use both RGB and CMYK colour with no colour profile, and PDF/A allows only one of them.",
                PdfARule.DeviceRgb,
                PdfARule.DeviceRgbPdfA4,
                PdfARule.DeviceCmyk,
                PdfARule.DeviceCmykPdfA4);
            return null;
        }

        if (facts.UsesCmyk)
        {
            profileRef = FindCmykProfile(session, intents);
            if (profileRef.IsNull)
            {
                session.Refuse("The pages use CMYK colour, and the file has no CMYK colour profile to describe it.", PdfARule.DeviceCmyk, PdfARule.DeviceCmykPdfA4);
                return null;
            }

            space = Cmyk;
            return CreateIntent(session, profileRef, space);
        }

        profileRef = PdfValue.FromReference(StoreEditing.Add(session.Store, PdfValue.FromStream(SrgbProfileStream(session.Store))));
        space = Rgb;
        return CreateIntent(session, profileRef, space);
    }

    /// <summary>Finds the first PDF/A output intent whose profile PDF/A accepts.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="intents">The output intents, or <see langword="null"/>.</param>
    /// <param name="profileRef">Receives its /DestOutputProfile value.</param>
    /// <param name="space">Receives the profile's colour space.</param>
    /// <returns>The intent, or <see langword="null"/>.</returns>
    private static PdfDictionary? FindPdfAIntent(PdfAConversionSession session, PdfArray? intents, out PdfValue profileRef, out string? space)
    {
        profileRef = default;
        space = null;
        var names = session.Store.Names;
        var gtsPdfA = names.Intern(PdfAIntent);
        for (var i = 0; intents is not null && i < intents.Count; i++)
        {
            if (intents.GetDictionary(i) is not { } intent || intent.GetName(KnownName.S) != gtsPdfA)
            {
                continue;
            }

            var profile = intent.GetRaw(names.Intern(ProfileKey));
            if (ReadProfileSpace(session, profile) is not { } found)
            {
                continue;
            }

            profileRef = profile;
            space = found;
            return intent;
        }

        return null;
    }

    /// <summary>Finds a CMYK output profile another output intent carries.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="intents">The output intents, or <see langword="null"/>.</param>
    /// <returns>The profile reference, or a null value.</returns>
    private static PdfValue FindCmykProfile(PdfAConversionSession session, PdfArray? intents)
    {
        var key = session.Store.Names.Intern(ProfileKey);
        for (var i = 0; intents is not null && i < intents.Count; i++)
        {
            var profile = intents.GetDictionary(i)?.GetRaw(key) ?? default;
            if (profile.IsReference && ReadProfileSpace(session, profile) == Cmyk)
            {
                return profile;
            }
        }

        return default;
    }

    /// <summary>Reads an output profile's colour space when PDF/A accepts the profile.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="profile">The /DestOutputProfile value.</param>
    /// <returns>"RGB ", "CMYK" or "GRAY", or <see langword="null"/> when the profile is missing or not acceptable.</returns>
    private static string? ReadProfileSpace(PdfAConversionSession session, PdfValue profile)
    {
        var bytes = ReadProfile(session, profile);
        if (bytes.Length < IccHeaderSize || bytes[VersionOffset] > (session.Part == PdfAClaim.Part1 ? Pdf1MaxVersion : MaxVersion))
        {
            return null;
        }

        var deviceClass = Encoding.ASCII.GetString(bytes, ClassOffset, SignatureLength);
        var space = Encoding.ASCII.GetString(bytes, SpaceOffset, SignatureLength);
        return deviceClass is "prtr" or "mntr" && space is Rgb or Cmyk or "GRAY" ? space : null;
    }

    /// <summary>Reads a profile stream's bytes.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="profile">The /DestOutputProfile value.</param>
    /// <returns>The bytes; empty when the profile is missing, direct or unreadable.</returns>
    private static byte[] ReadProfile(PdfAConversionSession session, PdfValue profile)
    {
        if (!profile.IsReference || StoreReading.Resolve(session.Store, profile).AsStream() is not { } stream)
        {
            return [];
        }

        try
        {
            return stream.DecodeToArray(session.CancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>Determines whether an intent's colour space describes the device colour the pages use.</summary>
    /// <param name="facts">What the scan found.</param>
    /// <param name="space">The intent's colour space.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Covers(PdfAConversionFacts facts, string? space) => (!facts.UsesRgb || space == Rgb) && (!facts.UsesCmyk || space == Cmyk);

    /// <summary>Creates a PDF/A output intent.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="profileRef">The profile reference.</param>
    /// <param name="space">The profile's colour space.</param>
    /// <returns>The intent.</returns>
    private static PdfDictionary CreateIntent(PdfAConversionSession session, PdfValue profileRef, string space)
    {
        var store = session.Store;
        var names = store.Names;
        var rgb = space == Rgb;
        var condition = rgb ? PdfASrgbProfile.Name : "CMYK output condition of the original file";
        var intent = new PdfDictionary(store, IntentEntries);
        intent.Set(KnownName.Type, PdfValue.FromName(names.Intern("OutputIntent"u8)));
        intent.Set(KnownName.S, PdfValue.FromName(names.Intern(PdfAIntent)));
        intent.Set(names.Intern("OutputConditionIdentifier"u8), PdfValue.FromString(PdfText.Encode(condition)));
        intent.Set(names.Intern("Info"u8), PdfValue.FromString(PdfText.Encode(condition)));
        if (rgb)
        {
            intent.Set(names.Intern("RegistryName"u8), PdfValue.FromString("http://www.color.org"u8.ToArray()));
        }

        intent.Set(names.Intern(ProfileKey), profileRef);
        session.Changed(
            rgb ? "Added an sRGB output intent, which says how the colours should look." : "Added a PDF/A output intent using the file's CMYK colour profile.",
            PdfARule.IccOutputProfile,
            PdfARule.IccOutputProfilePdfA1);
        return intent;
    }

    /// <summary>Builds the generated sRGB profile stream.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <returns>The stream.</returns>
    private static PdfStream SrgbProfileStream(PdfObjectStore store)
    {
        var dictionary = new PdfDictionary(store, 1);
        dictionary.Set(KnownName.N, PdfValue.FromInteger(RgbComponents));
        return new(dictionary, PdfASrgbProfile.GetBytes());
    }

    /// <summary>
    /// Writes the intents: the PDF/A intent first, then other intents that share its profile or carry none, without a
    /// /DestOutputProfileRef (ISO 19005-2 §6.2.3; ISO 19005-4:2020 §6.2.3).
    /// </summary>
    /// <param name="session">The conversion.</param>
    /// <param name="key">The /OutputIntents name.</param>
    /// <param name="intents">The old intents, or <see langword="null"/>.</param>
    /// <param name="pdfa">The PDF/A intent.</param>
    /// <param name="profileRef">Its profile.</param>
    private static void WriteIntents(PdfAConversionSession session, PdfName key, PdfArray? intents, PdfDictionary pdfa, PdfValue profileRef)
    {
        var store = session.Store;
        var refKey = store.Names.Intern("DestOutputProfileRef"u8);
        var kept = new PdfArray(store, (intents?.Count ?? 0) + 1);
        kept.Add(PdfValue.FromDictionary(WithoutProfileRef(pdfa, refKey)));
        var dropped = 0;
        for (var i = 0; intents is not null && i < intents.Count; i++)
        {
            if (intents.GetDictionary(i) is not { } intent || ReferenceEquals(intent, pdfa))
            {
                continue;
            }

            if (!SharesProfile(store, intent, profileRef))
            {
                dropped++;
                continue;
            }

            kept.Add(PdfValue.FromDictionary(WithoutProfileRef(intent, refKey)));
        }

        if (dropped > 0)
        {
            session.Changed("Removed other output intents whose colour profile differs from the PDF/A one.", PdfARule.OutputIntentSameProfile, PdfARule.OutputIntentCount);
        }

        var catalog = store.Catalog.Clone();
        catalog.Set(key, PdfValue.FromArray(kept));
        PdfAConversionRewriter.ReplaceCatalog(store, catalog);
    }

    /// <summary>Determines whether another intent may stay: it is not a PDF/A intent and has no profile or the same one.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="intent">The intent.</param>
    /// <param name="profileRef">The PDF/A intent's profile.</param>
    /// <returns><see langword="true"/> when it may stay.</returns>
    private static bool SharesProfile(PdfObjectStore store, PdfDictionary intent, PdfValue profileRef)
    {
        if (intent.GetName(KnownName.S) == store.Names.Intern(PdfAIntent))
        {
            return false;
        }

        var profile = intent.GetRaw(store.Names.Intern(ProfileKey));
        return profile.IsNull || (profile.IsReference && profile.AsReference() == profileRef.AsReference());
    }

    /// <summary>Copies an intent without /DestOutputProfileRef.</summary>
    /// <param name="intent">The intent.</param>
    /// <param name="refKey">The key.</param>
    /// <returns>The intent, or a copy without the key.</returns>
    private static PdfDictionary WithoutProfileRef(PdfDictionary intent, PdfName refKey)
    {
        if (!intent.ContainsKey(refKey))
        {
            return intent;
        }

        var copy = intent.Clone();
        _ = copy.Remove(refKey);
        return copy;
    }
}

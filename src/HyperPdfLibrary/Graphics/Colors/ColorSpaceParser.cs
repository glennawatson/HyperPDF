// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Parses colour space names and arrays (PDF 32000 section 8.6). Invalid input gives <see langword="null"/>.</summary>
internal static class ColorSpaceParser
{
    /// <summary>The deepest nesting of names and base spaces followed.</summary>
    private const int MaxDepth = 8;

    /// <summary>The array index of a family's first parameter.</summary>
    private const int FirstParameter = 1;

    /// <summary>The array index of an Indexed space's /HiVal, or the alternate of a Separation or DeviceN space.</summary>
    private const int SecondParameter = 2;

    /// <summary>The array index of an Indexed space's lookup, or the tint transform of a Separation or DeviceN space.</summary>
    private const int ThirdParameter = 3;

    /// <summary>The fewest components of an ICCBased space without a device fallback.</summary>
    private const int MinWideIcc = 2;

    /// <summary>The most components of an ICCBased space without a device fallback.</summary>
    private const int MaxWideIcc = 8;

    /// <summary>Parses a colour space.</summary>
    /// <param name="value">A name or an array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The colour space, or <see langword="null"/> when unknown or invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfColorSpace? Parse(PdfValue value, PdfDictionary? resources, int depth) => ParseCore(value, resources, depth, false);

    /// <summary>Finds a dictionary value by a key that the library does not intern.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="spelling">The key's UTF-8 spelling.</param>
    /// <returns>The value, or the null value when absent.</returns>
    internal static PdfValue FindByName(PdfDictionary dictionary, ReadOnlySpan<byte> spelling)
    {
        var names = dictionary.Owner?.Names;
        for (var i = 0; names is not null && i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            if (!key.IsKnown && names.NameEquals(key, spelling))
            {
                return dictionary.Get(key);
            }
        }

        return default;
    }

    /// <summary>Parses a colour space with the current default-space recursion state.</summary>
    /// <param name="value">A name or an array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or null.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The colour space, or null.</returns>
    private static PdfColorSpace? ParseCore(PdfValue value, PdfDictionary? resources, int depth, bool parsingDefault)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        if (value.TryGetName(out var name))
        {
            var family = name.ToKnownName();
            var device = Device(family);
            return device is null ? ParseResource(name, resources, depth, parsingDefault) : ApplyDefault(device, family, resources, depth, parsingDefault);
        }

        var array = value.AsArray();
        return array is null || array.Count == 0 ? null : ParseArray(array, resources, depth, parsingDefault);
    }

    /// <summary>Replaces a device space with the resources' /DefaultGray, /DefaultRGB or /DefaultCMYK space when one fits.</summary>
    /// <param name="device">The device space (or Pattern).</param>
    /// <param name="family">The family name that selected it.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The default space, or <paramref name="device"/>.</returns>
    private static PdfColorSpace ApplyDefault(PdfColorSpace device, KnownName family, PdfDictionary? resources, int depth, bool parsingDefault)
    {
        var key = DefaultKey(family);
        if (resources is null || parsingDefault || key.IsEmpty)
        {
            return device;
        }

        var value = FindByName(resources, key);
        var replacement = ParseCore(value, resources, depth + 1, true);
        return replacement is not null && replacement.Components == device.Components && replacement.Kind != PdfColorSpaceKind.Pattern ? replacement : device;
    }

    /// <summary>Gets the resource key of the default colour space for a device family.</summary>
    /// <param name="family">The family name.</param>
    /// <returns>The key's UTF-8 spelling, or empty for other families.</returns>
    private static ReadOnlySpan<byte> DefaultKey(KnownName family) => family switch
    {
        KnownName.DeviceGray or KnownName.G => "DefaultGray"u8,
        KnownName.DeviceRGB or KnownName.RGB => "DefaultRGB"u8,
        KnownName.DeviceCMYK or KnownName.CMYK => "DefaultCMYK"u8,
        _ => default,
    };

    /// <summary>Gets a device space, or the Pattern space, by family name.</summary>
    /// <param name="family">The family name.</param>
    /// <returns>The shared space, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfColorSpace? Device(KnownName family) => OutputIntentColors.Apply(DeviceCore(family));

    /// <summary>Gets a device space, or the Pattern space, by family name, without any output intent substitute.</summary>
    /// <param name="family">The family name.</param>
    /// <returns>The shared space, or <see langword="null"/>.</returns>
    private static PdfColorSpace? DeviceCore(KnownName family) => family switch
    {
        KnownName.DeviceGray or KnownName.G => DeviceGrayColorSpace.Instance,
        KnownName.DeviceRGB or KnownName.RGB => DeviceRgbColorSpace.Instance,
        KnownName.DeviceCMYK or KnownName.CMYK => DeviceCmykColorSpace.Instance,
        KnownName.Pattern => PatternColorSpace.Colored,
        _ => null,
    };

    /// <summary>Looks a name up in the resource dictionary.</summary>
    /// <param name="name">The name.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The colour space, or <see langword="null"/>.</returns>
    private static PdfColorSpace? ParseResource(PdfName name, PdfDictionary? resources, int depth, bool parsingDefault) =>
        resources is null || name.IsNone ? null : ParseCore(resources.Get(name), resources, depth + 1, parsingDefault);

    /// <summary>Parses a colour space array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The colour space, or <see langword="null"/>.</returns>
    private static PdfColorSpace? ParseArray(PdfArray array, PdfDictionary? resources, int depth, bool parsingDefault)
    {
        var family = array.GetName(0).ToKnownName();
        if (family == KnownName.Pattern)
        {
            return array.Count > FirstParameter ? new PatternColorSpace(ParseCore(array.Get(FirstParameter), resources, depth + 1, parsingDefault)) : PatternColorSpace.Colored;
        }

        return Device(family) ?? ParseCalibrated(family, array) ?? ParseSpecial(family, array, resources, depth, parsingDefault);
    }

    /// <summary>Parses the CIE-based families that need no other space.</summary>
    /// <param name="family">The family name.</param>
    /// <param name="array">The array.</param>
    /// <returns>The colour space, or <see langword="null"/> for other families.</returns>
    private static PdfColorSpace? ParseCalibrated(KnownName family, PdfArray array) => family switch
    {
        KnownName.CalGray => new CalGrayColorSpace(),
        KnownName.CalRGB => CalRgbColorSpace.Parse(array.GetDictionary(FirstParameter)),
        KnownName.Lab => LabColorSpace.Parse(array.GetDictionary(FirstParameter)),
        _ => null,
    };

    /// <summary>Parses the families built on other spaces.</summary>
    /// <param name="family">The family name.</param>
    /// <param name="array">The array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The colour space, or <see langword="null"/>.</returns>
    private static PdfColorSpace? ParseSpecial(KnownName family, PdfArray array, PdfDictionary? resources, int depth, bool parsingDefault) => family switch
    {
        KnownName.ICCBased => ParseIccBased(array, resources, depth, parsingDefault),
        KnownName.Indexed or KnownName.I => ParseIndexed(array, resources, depth, parsingDefault),
        KnownName.Separation => ParseSeparation(array, resources, depth, parsingDefault),
        KnownName.DeviceN => ParseDeviceN(array, resources, depth, parsingDefault),
        _ => null,
    };

    /// <summary>Parses an ICCBased space: the /Alternate when it fits /N, else the device space with /N components.</summary>
    /// <param name="array">The array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The colour space, or <see langword="null"/>.</returns>
    private static IccBasedColorSpace? ParseIccBased(PdfArray array, PdfDictionary? resources, int depth, bool parsingDefault)
    {
        var stream = array.Get(FirstParameter).AsStream();
        var dictionary = stream?.Dictionary ?? array.GetDictionary(FirstParameter);
        if (dictionary is null)
        {
            return null;
        }

        var alternate = ResolveIccAlternate(dictionary, resources, depth, parsingDefault);
        if (alternate is null)
        {
            return stream is null ? null : ParseWideIcc(stream, dictionary.GetInt32(KnownName.N));
        }

        var transform = stream is null ? null : IccProfile.Get(stream, alternate.Components);

        // An sRGB profile converts like device RGB, which keeps the vectorised row path.
        return transform?.IsSrgb == true
            ? new(PdfColorSpace.DeviceRgb, PdfColorSpace.DeviceRgb.GetDefaultDecode(PdfColorSpace.ByteBits), null)
            : new(alternate, ReadRange(dictionary.GetArray(KnownName.Range), alternate, transform?.DefaultRange), transform);
    }

    /// <summary>Parses an ICCBased space of two or five to eight components, which has no device space to fall back to.</summary>
    /// <param name="stream">The profile stream.</param>
    /// <param name="components">The /N value.</param>
    /// <returns>The colour space, or <see langword="null"/> when /N is out of range or the profile cannot convert.</returns>
    private static IccBasedColorSpace? ParseWideIcc(PdfStream stream, int components)
    {
        if (components is < MinWideIcc or > MaxWideIcc)
        {
            return null;
        }

        var transform = IccProfile.Get(stream, components);
        if (transform is null)
        {
            return null;
        }

        var range = new float[PdfColorSpace.PairSize * components];
        for (var i = 1; i < range.Length; i += PdfColorSpace.PairSize)
        {
            range[i] = 1;
        }

        return new(null, range, transform, components);
    }

    /// <summary>Gets the space an ICC profile falls back to: its /Alternate when it fits /N, else the device space with /N components.</summary>
    /// <param name="dictionary">The profile stream dictionary.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The space, or <see langword="null"/> when /N is not 1, 3 or 4 and the alternate is unusable.</returns>
    private static PdfColorSpace? ResolveIccAlternate(PdfDictionary dictionary, PdfDictionary? resources, int depth, bool parsingDefault)
    {
        var components = dictionary.GetInt32(KnownName.N);
        var alternate = ParseCore(dictionary.Get(KnownName.Alternate), resources, depth + 1, parsingDefault);
        if (FitsIcc(alternate, components))
        {
            return alternate;
        }

        return components is 1 or PdfColorSpace.RgbComponents or DeviceCmykColorSpace.ComponentCount ? PdfColorSpace.FromComponents(components) : null;
    }

    /// <summary>Determines whether an ICC /Alternate is usable for a profile with /N components.</summary>
    /// <param name="alternate">The parsed alternate, or <see langword="null"/>.</param>
    /// <param name="components">The /N value, or zero when missing.</param>
    /// <returns><see langword="true"/> when usable.</returns>
    private static bool FitsIcc([NotNullWhen(true)] PdfColorSpace? alternate, int components) =>
        alternate is not null && !IsSpecial(alternate) && (components <= 0 || alternate.Components == components);

    /// <summary>Reads an ICC /Range array, defaulting to the alternate's ranges.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="alternate">The alternate space.</param>
    /// <param name="profileRange">The range the profile's colour space needs, used when the array is absent; or <see langword="null"/>.</param>
    /// <returns>Min/max pairs.</returns>
    private static float[] ReadRange(PdfArray? array, PdfColorSpace alternate, float[]? profileRange)
    {
        var range = FunctionReader.ReadIntervals(array);
        if (range?.Length == PdfColorSpace.PairSize * alternate.Components)
        {
            return range;
        }

        return profileRange?.Length == PdfColorSpace.PairSize * alternate.Components ? profileRange : alternate.GetDefaultDecode(PdfColorSpace.ByteBits);
    }

    /// <summary>Parses an Indexed space.</summary>
    /// <param name="array">The array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The colour space, or <see langword="null"/>.</returns>
    private static IndexedColorSpace? ParseIndexed(PdfArray array, PdfDictionary? resources, int depth, bool parsingDefault)
    {
        var baseSpace = ParseCore(array.Get(FirstParameter), resources, depth + 1, parsingDefault);
        if (baseSpace is null || baseSpace.Kind is PdfColorSpaceKind.Pattern or PdfColorSpaceKind.Indexed)
        {
            return null;
        }

        var lookup = array.Get(ThirdParameter);
        var stream = lookup.AsStream();
        return new(baseSpace, array.GetInt32(SecondParameter), stream is null ? lookup.AsStringBytes() : DecodeLookup(stream));
    }

    /// <summary>Decodes a lookup stream, giving an empty palette when the data is damaged.</summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The bytes.</returns>
    private static byte[] DecodeLookup(PdfStream stream)
    {
        try
        {
            return stream.DecodeToArray();
        }
        catch (InvalidDataException)
        {
            return [];
        }
    }

    /// <summary>Parses a Separation space.</summary>
    /// <param name="array">The array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The colour space.</returns>
    private static SeparationColorSpace ParseSeparation(PdfArray array, PdfDictionary? resources, int depth, bool parsingDefault)
    {
        var isNone = IsNone(array.GetName(FirstParameter), array.Owner);
        return new(ParseTint(array, resources, depth, 1, parsingDefault), isNone);
    }

    /// <summary>Parses a DeviceN space.</summary>
    /// <param name="array">The array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The colour space, or <see langword="null"/> when the names are invalid.</returns>
    private static DeviceNColorSpace? ParseDeviceN(PdfArray array, PdfDictionary? resources, int depth, bool parsingDefault)
    {
        var names = array.GetArray(FirstParameter);
        if (names is null || names.Count == 0 || names.Count > PdfColorSpace.MaxComponents)
        {
            return null;
        }

        var allNone = true;
        for (var i = 0; i < names.Count && allNone; i++)
        {
            allNone = IsNone(names.GetName(i), names.Owner);
        }

        return new(names.Count, ParseTint(array, resources, depth, names.Count, parsingDefault), allNone);
    }

    /// <summary>Parses the alternate space and tint transform of a Separation or DeviceN space.</summary>
    /// <param name="array">The array.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="tints">The number of tints.</param>
    /// <param name="parsingDefault">Whether a default colour space is being resolved.</param>
    /// <returns>The tint transform.</returns>
    private static TintTransform ParseTint(PdfArray array, PdfDictionary? resources, int depth, int tints, bool parsingDefault)
    {
        var alternate = ParseCore(array.Get(SecondParameter), resources, depth + 1, parsingDefault);
        return alternate is null || IsSpecial(alternate)
            ? new(DeviceGrayColorSpace.Instance, null)
            : TintTransform.Create(alternate, PdfFunction.Parse(array.Get(ThirdParameter)), tints);
    }

    /// <summary>Determines whether a space may not serve as an alternate: Pattern, Indexed, Separation or DeviceN.</summary>
    /// <param name="space">The space.</param>
    /// <returns><see langword="true"/> for the special families.</returns>
    private static bool IsSpecial(PdfColorSpace space) =>
        space.Kind is PdfColorSpaceKind.Pattern or PdfColorSpaceKind.Indexed or PdfColorSpaceKind.Separation or PdfColorSpaceKind.DeviceN;

    /// <summary>Determines whether a colorant name is /None, which the library does not intern.</summary>
    /// <param name="name">The name.</param>
    /// <param name="owner">The document whose name table spells the name.</param>
    /// <returns><see langword="true"/> for /None.</returns>
    private static bool IsNone(PdfName name, PdfObjectStore? owner) =>
        !name.IsNone && !name.IsKnown && owner is not null && owner.Names.NameEquals(name, "None"u8);
}

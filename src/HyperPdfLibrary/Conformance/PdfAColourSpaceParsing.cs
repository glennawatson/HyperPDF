// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads colour spaces the way veraPDF does for PDF/A: device names are replaced by a DefaultGray, DefaultRGB or
/// DefaultCMYK resource when one is set, other names are looked up in the resources, and arrays are kept whole.
/// </summary>
internal static class PdfAColourSpaceParsing
{
    /// <summary>The deepest a colour space may refer to others before the walk stops, to end cycles.</summary>
    private const int MaxDepth = 16;

    /// <summary>The index of the colour space family name in a colour space array.</summary>
    private const int FamilyIndex = 0;

    /// <summary>The value at the second position of a colour space array.</summary>
    private const int SecondIndex = 1;

    /// <summary>Gets the family of a colour space.</summary>
    /// <param name="space">The space.</param>
    /// <returns>The family name, <see cref="KnownName.Pattern"/> for pattern dictionaries, or <see cref="KnownName.None"/>.</returns>
    internal static KnownName Family(in PdfAColourSpace space)
    {
        var value = space.Value;
        if (value.TryGetName(out var name))
        {
            return name.ToKnownName();
        }

        if (value.AsArray() is { } array)
        {
            return array.GetName(FamilyIndex).ToKnownName();
        }

        return value.AsDictionary() is null ? KnownName.None : KnownName.Pattern;
    }

    /// <summary>Gets a colour space from a resource dictionary by name, as a resource lookup does.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="resources">The resources.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="isDefault">Whether the name is a Default space being substituted.</param>
    /// <returns>The space, or null.</returns>
    internal static PdfAColourSpace? FromResources(PdfAColourState state, PdfDictionary resources, PdfName name, bool isDefault)
    {
        var raw = Entry(resources, KnownName.ColorSpace, name);
        return StoreReading.Resolve(state.Store, raw).IsNull
            ? Parse(state, PdfValue.FromName(name), resources, isDefault)
            : Parse(state, raw, resources, isDefault);
    }

    /// <summary>Reads a colour space value.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="raw">The value, possibly a reference.</param>
    /// <param name="resources">The resources names in it are looked up in, or null.</param>
    /// <param name="wasDefault">Whether it is reached through a Default space.</param>
    /// <returns>The space, or null when the value is not a colour space.</returns>
    internal static PdfAColourSpace? Parse(PdfAColourState state, PdfValue raw, PdfDictionary? resources, bool wasDefault)
    {
        if (state.Depth >= MaxDepth)
        {
            return null;
        }

        state.Depth++;
        try
        {
            var id = raw.IsReference ? raw.AsReference() : default;
            var value = StoreReading.Resolve(state.Store, raw);
            return value.Kind switch
            {
                PdfKind.Name => ParseName(state, value.AsName(), resources, wasDefault),
                PdfKind.Array => ParseArray(state, value, id, resources, wasDefault),
                PdfKind.Dictionary or PdfKind.Stream => ParsePattern(value, id, resources, wasDefault),
                _ => null,
            };
        }
        finally
        {
            state.Depth--;
        }
    }

    /// <summary>Gets the raw value of a resource entry.</summary>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="category">The resource category.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The value, possibly a reference; null when missing.</returns>
    internal static PdfValue Entry(PdfDictionary? resources, KnownName category, PdfName name) =>
        resources?.GetDictionary(category)?.GetRaw(name) ?? default;

    /// <summary>Gets the Default space a device name is replaced with in some resources.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="name">The device name.</param>
    /// <returns>The Default resource name, or none when no replacement applies.</returns>
    internal static PdfName DefaultFor(PdfAColourState state, PdfDictionary? resources, PdfName name)
    {
        var defaultName = name.ToKnownName() switch
        {
            KnownName.DeviceGray => state.Names.DefaultGray,
            KnownName.DeviceRGB => state.Names.DefaultRgb,
            KnownName.DeviceCMYK => state.Names.DefaultCmyk,
            _ => default,
        };
        return !defaultName.IsNone && !StoreReading.Resolve(state.Store, Entry(resources, KnownName.ColorSpace, defaultName)).IsNull ? defaultName : default;
    }

    /// <summary>Gets the underlying space of an uncoloured pattern space such as <c>[/Pattern /DeviceRGB]</c>.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The pattern space.</param>
    /// <returns>The underlying space, or null.</returns>
    internal static PdfAColourSpace? Underlying(PdfAColourState state, in PdfAColourSpace space) =>
        space.Value.AsArray() is { Count: > SecondIndex } array ? Parse(state, array.GetRaw(SecondIndex), space.Resources, false) : null;

    /// <summary>Reads a colour space given by name.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="name">The name.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="wasDefault">Whether it is reached through a Default space.</param>
    /// <returns>The space, or null.</returns>
    private static PdfAColourSpace? ParseName(PdfAColourState state, PdfName name, PdfDictionary? resources, bool wasDefault)
    {
        var defaultName = wasDefault || resources is null ? default : DefaultFor(state, resources, name);
        if (!defaultName.IsNone)
        {
            return FromResources(state, resources!, defaultName, true);
        }

        if (name.ToKnownName() is KnownName.DeviceGray or KnownName.DeviceRGB or KnownName.DeviceCMYK or KnownName.Pattern)
        {
            return new(PdfValue.FromName(name), default, resources, wasDefault);
        }

        var raw = Entry(resources, KnownName.ColorSpace, name);
        return resources is not null && !StoreReading.Resolve(state.Store, raw).IsNull ? Parse(state, raw, resources, false) : null;
    }

    /// <summary>Reads a colour space array.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="value">The array.</param>
    /// <param name="id">The object it came from.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="wasDefault">Whether it is reached through a Default space.</param>
    /// <returns>The space, or null.</returns>
    private static PdfAColourSpace? ParseArray(PdfAColourState state, PdfValue value, PdfObjectId id, PdfDictionary? resources, bool wasDefault)
    {
        var array = value.AsArray()!;
        return array.Count switch
        {
            0 => null,
            1 => Parse(state, array.GetRaw(FamilyIndex), resources, wasDefault),
            _ => ParseFamily(state, value, id, resources, wasDefault),
        };
    }

    /// <summary>Reads a colour space array of two or more entries by its family name.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="value">The array.</param>
    /// <param name="id">The object it came from.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="wasDefault">Whether it is reached through a Default space.</param>
    /// <returns>The space, or null when the family is unknown.</returns>
    private static PdfAColourSpace? ParseFamily(PdfAColourState state, PdfValue value, PdfObjectId id, PdfDictionary? resources, bool wasDefault)
    {
        var family = value.AsArray()!.GetName(FamilyIndex);
        return family.ToKnownName() switch
        {
            KnownName.CalGray or
            KnownName.CalRGB or
            KnownName.Lab or
            KnownName.ICCBased or
            KnownName.Separation or
            KnownName.DeviceN or
            KnownName.Pattern => new PdfAColourSpace(value, id, resources, wasDefault),
            KnownName.Indexed => new PdfAColourSpace(value, id, resources, false),
            _ when family == state.Names.CalCmyk => ParseName(state, KnownName.DeviceCMYK, resources, wasDefault),
            _ => null,
        };
    }

    /// <summary>Reads a pattern given as a dictionary or stream.</summary>
    /// <param name="value">The pattern.</param>
    /// <param name="id">The object it came from.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="wasDefault">Whether it is reached through a Default space.</param>
    /// <returns>The pattern, or null when it has no valid <c>/PatternType</c>.</returns>
    private static PdfAColourSpace? ParsePattern(PdfValue value, PdfObjectId id, PdfDictionary? resources, bool wasDefault) =>
        value.AsDictionary()!.Get(KnownName.PatternType).AsInteger(-1) is PdfAPatterns.Tiling or PdfAPatterns.Shading
            ? new PdfAColourSpace(value, id, resources, wasDefault)
            : null;
}

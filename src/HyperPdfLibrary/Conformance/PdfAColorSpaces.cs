// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Classifies colour spaces the way veraPDF resolves them for content streams: names through a resource dictionary and
/// its Default colour spaces, families by their first array element, and patterns by their <c>/PatternType</c>.
/// </summary>
internal static class PdfAColorSpaces
{
    /// <summary>The pattern type of a tiling pattern.</summary>
    internal const int TilingPattern = 1;

    /// <summary>The pattern type of a shading pattern.</summary>
    private const int ShadingPattern = 2;

    /// <summary>How many names a colour space may refer through before the chain is treated as broken.</summary>
    private const int MaxNameChain = 16;

    /// <summary>Gets the Default colour space that replaces a device space in a resource dictionary.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="name">The colour space name.</param>
    /// <returns>The Default colour space name, or none when the name is not a device space or has no replacement.</returns>
    internal static PdfName DefaultFor(PdfAGraphicsWalk walk, PdfDictionary? resources, PdfName name)
    {
        var names = walk.Names;
        var replacement = name.ToKnownName() switch
        {
            KnownName.DeviceGray => names.DefaultGray,
            KnownName.DeviceRGB => names.DefaultRgb,
            KnownName.DeviceCMYK => names.DefaultCmyk,
            _ => default,
        };

        return !replacement.IsNone && Has(resources, replacement) ? replacement : default;
    }

    /// <summary>Resolves a colour space name through a resource dictionary.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="name">The name.</param>
    /// <param name="wasDefault">Whether the name already replaced a device space, so Default spaces are not applied again.</param>
    /// <returns>The kind of colour space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfAColorSpaceKind FromResource(PdfAGraphicsWalk walk, PdfDictionary? resources, PdfName name, bool wasDefault) =>
        FromResource(walk, resources, name, wasDefault, 0);

    /// <summary>Determines whether a pattern resource is a pattern veraPDF accepts.</summary>
    /// <param name="value">The resolved resource.</param>
    /// <param name="cancellationToken">Cancels resource traversal.</param>
    /// <returns><see langword="true"/> for the Pattern space, a pattern colour space array, or a tiling or shading pattern.</returns>
    /// <exception cref="OperationCanceledException">Traversal was cancelled.</exception>
    internal static bool IsPattern(PdfValue value, CancellationToken cancellationToken)
    {
        for (var depth = 0; depth < MaxNameChain; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.IsName(KnownName.Pattern))
            {
                return true;
            }

            if (value.AsArray() is not { Count: > 0 } array)
            {
                return value.AsDictionary() is { } dictionary && dictionary.Get(KnownName.PatternType).AsInteger(0) is TilingPattern or ShadingPattern;
            }

            if (array.Count != 1)
            {
                return array.Get(0).IsName(KnownName.Pattern);
            }

            value = array.Get(0);
        }

        return false;
    }

    /// <summary>Resolves a colour space name through a resource dictionary, following at most a few names.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="name">The name.</param>
    /// <param name="wasDefault">Whether Default spaces were already applied.</param>
    /// <param name="depth">How many names were followed.</param>
    /// <returns>The kind of colour space.</returns>
    /// <exception cref="OperationCanceledException">Traversal was cancelled.</exception>
    private static PdfAColorSpaceKind FromResource(PdfAGraphicsWalk walk, PdfDictionary? resources, PdfName name, bool wasDefault, int depth)
    {
        walk.Context.CancellationToken.ThrowIfCancellationRequested();
        if (depth > MaxNameChain)
        {
            return PdfAColorSpaceKind.Invalid;
        }

        var raw = resources?.GetDictionary(KnownName.ColorSpace)?.GetRaw(name) ?? default;
        return raw.IsNull
            ? FromName(walk, resources, name, wasDefault, depth + 1)
            : FromValue(walk, resources, StoreReading.Resolve(walk.Store, raw), wasDefault, depth + 1);
    }

    /// <summary>Classifies a colour space value: a name, a family array, or a pattern dictionary.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="resources">The resources names resolve through, or null.</param>
    /// <param name="value">The resolved value.</param>
    /// <param name="wasDefault">Whether Default spaces were already applied.</param>
    /// <param name="depth">How many names were followed.</param>
    /// <returns>The kind of colour space.</returns>
    /// <exception cref="OperationCanceledException">Traversal was cancelled.</exception>
    private static PdfAColorSpaceKind FromValue(PdfAGraphicsWalk walk, PdfDictionary? resources, PdfValue value, bool wasDefault, int depth)
    {
        walk.Context.CancellationToken.ThrowIfCancellationRequested();
        if (depth > MaxNameChain)
        {
            return PdfAColorSpaceKind.Invalid;
        }

        if (value.TryGetName(out var name))
        {
            return FromName(walk, resources, name, wasDefault, depth);
        }

        if (value.AsArray() is { } array)
        {
            return FromArray(walk, resources, array, wasDefault, depth);
        }

        return value.AsDictionary() is { } dictionary && dictionary.Get(KnownName.PatternType).AsInteger(0) is TilingPattern or ShadingPattern
            ? PdfAColorSpaceKind.Pattern
            : PdfAColorSpaceKind.Invalid;
    }

    /// <summary>Classifies a colour space array by its family name.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="array">The array.</param>
    /// <param name="wasDefault">Whether Default spaces were already applied.</param>
    /// <param name="depth">How many names were followed.</param>
    /// <returns>The kind of colour space.</returns>
    private static PdfAColorSpaceKind FromArray(PdfAGraphicsWalk walk, PdfDictionary? resources, PdfArray array, bool wasDefault, int depth)
    {
        if (array.Count == 0)
        {
            return PdfAColorSpaceKind.Invalid;
        }

        if (array.Count == 1)
        {
            return FromValue(walk, resources, array.Get(0), wasDefault, depth + 1);
        }

        var family = array.Get(0).AsName();
        return family == walk.Names.CalCmyk ? FromName(walk, resources, KnownName.DeviceCMYK, wasDefault, depth) : FamilyKind(family);
    }

    /// <summary>Classifies a colour space family name.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The kind of colour space.</returns>
    private static PdfAColorSpaceKind FamilyKind(PdfName family) => family.ToKnownName() switch
    {
        KnownName.Pattern => PdfAColorSpaceKind.Pattern,
        KnownName.CalGray or KnownName.CalRGB or KnownName.Lab or KnownName.ICCBased
            or KnownName.Separation or KnownName.DeviceN or KnownName.Indexed => PdfAColorSpaceKind.Other,
        _ => PdfAColorSpaceKind.Invalid,
    };

    /// <summary>Classifies a colour space name: a Default replacement, a device space, the Pattern space, or another resource.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="name">The name.</param>
    /// <param name="wasDefault">Whether Default spaces were already applied.</param>
    /// <param name="depth">How many names were followed.</param>
    /// <returns>The kind of colour space.</returns>
    private static PdfAColorSpaceKind FromName(PdfAGraphicsWalk walk, PdfDictionary? resources, PdfName name, bool wasDefault, int depth)
    {
        var replacement = DefaultFor(walk, resources, name);
        if (!wasDefault && !replacement.IsNone)
        {
            return FromResource(walk, resources, replacement, true, depth);
        }

        switch (name.ToKnownName())
        {
            case KnownName.DeviceGray or KnownName.DeviceRGB or KnownName.DeviceCMYK:
            {
                return PdfAColorSpaceKind.Device;
            }

            case KnownName.Pattern:
            {
                return PdfAColorSpaceKind.Pattern;
            }

            default:
            {
                return Has(resources, name) ? FromResource(walk, resources, name, false, depth) : PdfAColorSpaceKind.Invalid;
            }
        }
    }

    /// <summary>Determines whether a resource dictionary defines a colour space name.</summary>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Has(PdfDictionary? resources, PdfName name) => !(resources?.GetDictionary(KnownName.ColorSpace)?.GetRaw(name) ?? default).IsNull;
}

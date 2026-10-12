// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks Separation and DeviceN colour spaces: consistent Separations, DeviceN colourant limits and colourant definitions.</summary>
internal static class PdfASeparationChecks
{
    /// <summary>The index of the colourant name or names.</summary>
    private const int NamesIndex = 1;

    /// <summary>The index of the alternate space.</summary>
    private const int AlternateIndex = 2;

    /// <summary>The index of the tint transform.</summary>
    private const int TintIndex = 3;

    /// <summary>The index of a DeviceN space's attributes dictionary.</summary>
    private const int AttributesIndex = 4;

    /// <summary>The most colourants a DeviceN space may have in PDF/A-1.</summary>
    private const int Part1MaxColourants = 8;

    /// <summary>The most colourants a DeviceN space may have in PDF/A-2 and PDF/A-3.</summary>
    private const int MaxColourants = 32;

    /// <summary>The first PDF/A part.</summary>
    private const int Part1 = 1;

    /// <summary>The PDF/A part without a colourant limit rule.</summary>
    private const int Part4 = 4;

    /// <summary>Visits a Separation space: records it, checks it against others of the same name and visits its alternate.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    internal static void VisitSeparation(PdfAColourState state, in PdfAColourSpace space)
    {
        if (!PdfAColourChecks.FirstVisit(state, space))
        {
            return;
        }

        var array = space.Value.AsArray()!;
        var alternate = PdfAColourSpaceParsing.Parse(state, array.GetRaw(AlternateIndex), space.Resources, space.WasDefault);
        if (StoreReading.Resolve(state.Store, array.GetRaw(NamesIndex)).TryGetName(out var name))
        {
            state.Context.Require(IsConsistent(state, name, space, alternate), PdfARule.SeparationConsistent, null);
        }

        PdfAColourChecks.Visit(state, alternate);
    }

    /// <summary>Visits a DeviceN space: checks its colourants and visits its alternate, colourant and process spaces.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    internal static void VisitDeviceN(PdfAColourState state, in PdfAColourSpace space)
    {
        if (!PdfAColourChecks.FirstVisit(state, space))
        {
            return;
        }

        var array = space.Value.AsArray()!;
        var names = StoreReading.Resolve(state.Store, array.GetRaw(NamesIndex)).AsArray();
        var attributes = StoreReading.Resolve(state.Store, array.GetRaw(AttributesIndex)).AsDictionary();
        CheckColourants(state, names, attributes);
        PdfAColourChecks.Visit(state, PdfAColourSpaceParsing.Parse(state, array.GetRaw(AlternateIndex), space.Resources, space.WasDefault));
        VisitColourantSpaces(state, space, attributes);
        if (attributes?.GetDictionary(KnownName.Process) is { } process)
        {
            PdfAColourChecks.Visit(state, PdfAColourSpaceParsing.Parse(state, process.GetRaw(KnownName.ColorSpace), space.Resources, space.WasDefault));
        }
    }

    /// <summary>Checks the number of colourants and that each spot colourant is defined in <c>/Colorants</c> or <c>/Process</c>.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="names">The colourant names, or null.</param>
    /// <param name="attributes">The attributes dictionary, or null.</param>
    private static void CheckColourants(PdfAColourState state, PdfArray? names, PdfDictionary? attributes)
    {
        var count = names?.Count ?? 0;
        if (state.Context.Part == Part1)
        {
            state.Context.Require(count <= Part1MaxColourants, PdfARule.DeviceNComponentsPdfA1, null);
            return;
        }

        if (state.Context.Part != Part4)
        {
            state.Context.Require(count <= MaxColourants, PdfARule.DeviceNComponents, null);
        }

        state.Context.Require(ColourantsPresent(state, names, attributes), PdfARule.DeviceNColorants, null);
    }

    /// <summary>Determines whether every spot colourant has an entry in the attributes.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="names">The colourant names, or null.</param>
    /// <param name="attributes">The attributes dictionary, or null.</param>
    /// <returns><see langword="true"/> when they all do.</returns>
    private static bool ColourantsPresent(PdfAColourState state, PdfArray? names, PdfDictionary? attributes)
    {
        var colourants = attributes?.GetDictionary(KnownName.Colorants);
        var components = attributes?.GetDictionary(KnownName.Process)?.GetArray(KnownName.Components);
        for (var i = 0; names is not null && i < names.Count; i++)
        {
            // A colourant that is not a name can never be defined, so it fails.
            if (!names.Get(i).TryGetName(out var name) || !IsDefined(state, name, colourants, components))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether a colourant needs no definition or has one.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="name">The colourant.</param>
    /// <param name="colourants">The <c>/Colorants</c> dictionary, or null.</param>
    /// <param name="components">The <c>/Process</c> components, or null.</param>
    /// <returns><see langword="true"/> for None, process colourants and colourants with an entry.</returns>
    private static bool IsDefined(PdfAColourState state, PdfName name, PdfDictionary? colourants, PdfArray? components) =>
        name.Is(KnownName.NoneName) || Array.IndexOf(state.Names.ProcessColourants, name) >= 0
        || colourants?.ContainsKey(name) == true || Contains(components, name);

    /// <summary>Determines whether an array holds a name.</summary>
    /// <param name="array">The array, or null.</param>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Contains(PdfArray? array, PdfName name)
    {
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            if (array.GetName(i) == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Visits the Separation spaces of a DeviceN space's <c>/Colorants</c> dictionary.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The DeviceN space.</param>
    /// <param name="attributes">The attributes dictionary, or null.</param>
    private static void VisitColourantSpaces(PdfAColourState state, in PdfAColourSpace space, PdfDictionary? attributes)
    {
        var colourants = attributes?.GetDictionary(KnownName.Colorants);
        for (var i = 0; colourants is not null && i < colourants.Count; i++)
        {
            var colourant = PdfAColourSpaceParsing.Parse(state, colourants.GetValueAt(i), space.Resources, false);
            if (colourant is { } separation && PdfAColourSpaceParsing.Family(separation) == KnownName.Separation)
            {
                PdfAColourChecks.Visit(state, separation);
            }
        }
    }

    /// <summary>
    /// Records a Separation and checks it against the others with the same colourant name: their alternate spaces and tint
    /// transforms must be equivalent. Once a name is inconsistent, every later Separation of that name fails too.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <param name="name">The colourant name.</param>
    /// <param name="space">The space.</param>
    /// <param name="alternate">Its alternate space, or null.</param>
    /// <returns><see langword="true"/> when it is consistent.</returns>
    private static bool IsConsistent(PdfAColourState state, PdfName name, in PdfAColourSpace space, PdfAColourSpace? alternate)
    {
        ref var seen = ref CollectionsMarshal.GetValueRefOrAddDefault(state.Separations, name, out _);
        seen ??= [];
        seen.Add(space);
        if (state.InconsistentSeparations.Contains(name))
        {
            return false;
        }

        var tint = space.Value.AsArray()!.GetRaw(TintIndex);
        foreach (var other in seen)
        {
            if (Matches(state, other, alternate, tint))
            {
                continue;
            }

            _ = state.InconsistentSeparations.Add(name);
            return false;
        }

        return true;
    }

    /// <summary>Determines whether a recorded Separation has an equivalent alternate space and tint transform.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="other">The recorded Separation.</param>
    /// <param name="alternate">The alternate space to compare with, or null.</param>
    /// <param name="tint">The tint transform to compare with.</param>
    /// <returns><see langword="true"/> when both are equivalent.</returns>
    private static bool Matches(PdfAColourState state, in PdfAColourSpace other, PdfAColourSpace? alternate, PdfValue tint)
    {
        var otherArray = other.Value.AsArray()!;
        var otherAlternate = PdfAColourSpaceParsing.Parse(state, otherArray.GetRaw(AlternateIndex), other.Resources, other.WasDefault);
        return PdfAEquivalence.AreEquivalent(state, otherAlternate?.Value ?? default, alternate?.Value ?? default)
            && PdfAEquivalence.AreEquivalent(state, otherArray.GetRaw(TintIndex), tint);
    }
}

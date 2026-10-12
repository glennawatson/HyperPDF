// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Looks up the resources a content stream names, as veraPDF does: the stream's own resources first, then the page's.</summary>
internal static class PdfAResources
{
    /// <summary>Gets the colour space a content stream names, replacing device names with a Default space when one is set.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="scope">The resources.</param>
    /// <param name="name">The name.</param>
    /// <returns>The space, or null when the name is not defined.</returns>
    internal static PdfAColourSpace? ColourSpace(PdfAColourState state, in PdfAColourResourceScope scope, PdfName name)
    {
        var defaults = scope.Own ?? scope.Page;
        var defaultName = PdfAColourSpaceParsing.DefaultFor(state, defaults, name);
        if (!defaultName.IsNone)
        {
            return PdfAColourSpaceParsing.FromResources(state, defaults!, defaultName, true);
        }

        if (scope.Own is { } own && PdfAColourSpaceParsing.FromResources(state, own, name, false) is { } found)
        {
            return found;
        }

        return scope.Page is { } page
            ? PdfAColourSpaceParsing.FromResources(state, page, name, false)
            : PdfAColourSpaceParsing.Parse(state, PdfValue.FromName(name), null, false);
    }

    /// <summary>Finds a named resource of a category.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="scope">The resources.</param>
    /// <param name="category">The category, such as <c>/XObject</c>.</param>
    /// <param name="name">The name.</param>
    /// <returns>The resource, possibly a reference, and the resources it was found in; null when missing.</returns>
    internal static PdfAResource Find(PdfAColourState state, in PdfAColourResourceScope scope, KnownName category, PdfName name)
    {
        var own = PdfAColourSpaceParsing.Entry(scope.Own, category, name);
        if (!StoreReading.Resolve(state.Store, own).IsNull)
        {
            return new(own, scope.Own);
        }

        var page = PdfAColourSpaceParsing.Entry(scope.Page, category, name);
        return StoreReading.Resolve(state.Store, page).IsNull ? default : new(page, scope.Page);
    }

    /// <summary>Gets the scope of a stream with its own resources painted from a scope.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="scope">The scope it is painted from.</param>
    /// <param name="stream">The stream dictionary.</param>
    /// <returns>The scope for its content.</returns>
    internal static PdfAColourResourceScope Extend(PdfAColourState state, in PdfAColourResourceScope scope, PdfDictionary stream) =>
        new(scope.Page, StoreReading.Resolve(state.Store, stream.GetRaw(KnownName.Resources)).AsDictionary());
}

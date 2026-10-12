// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Resolves the names a content stream uses, as veraPDF does, and records the PDF/A failures for names that are missing
/// (ISO 19005-4, 6.2.2) or found only in resources the stream does not own (ISO 19005-2, 6.2.2).
/// </summary>
internal static class PdfAResourceLookup
{
    /// <summary>Looks up a named resource.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The content stream.</param>
    /// <param name="category">The resource category, such as <c>/Font</c>.</param>
    /// <param name="name">The name.</param>
    /// <returns>The resource, or null when the name is not defined.</returns>
    internal static PdfValue Find(PdfAGraphicsWalk walk, PdfAContentState state, KnownName category, PdfName name)
    {
        var scope = state.Scope;
        var value = default(PdfValue);
        var found = false;
        if (!scope.IsPage)
        {
            found = TryGet(scope.Local, category, name, out value);
            if (!found && scope.HasPage && TryGet(scope.Page, category, name, out value))
            {
                found = true;
                FailInherited(walk, state);
            }
        }
        else if (scope.HasPage && TryGet(scope.Page, category, name, out value))
        {
            found = true;
            if (scope.PageInherited)
            {
                FailInherited(walk, state);
            }
        }

        if (!found)
        {
            walk.Context.Fail(PdfARule.ContentUndefinedResources, state.Where);
            return default;
        }

        return value;
    }

    /// <summary>Looks up a colour space by the name a <c>cs</c>, <c>CS</c> or device colour operator uses.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The content stream.</param>
    /// <param name="name">The name.</param>
    /// <returns>What kind of colour space the name gives.</returns>
    internal static PdfAColorSpaceKind FindColorSpace(PdfAGraphicsWalk walk, PdfAContentState state, PdfName name)
    {
        var scope = state.Scope;
        var own = scope.IsPage ? scope.Page : scope.Local;
        var defaultName = !scope.IsPage || scope.HasPage ? PdfAColorSpaces.DefaultFor(walk, own, name) : default;
        var kind = defaultName.IsNone ? FindNamedColorSpace(walk, state, name) : PdfAColorSpaces.FromResource(walk, own, defaultName, true);
        if (kind == PdfAColorSpaceKind.Invalid)
        {
            walk.Context.Fail(PdfARule.ContentUndefinedResources, state.Where);
        }

        return kind;
    }

    /// <summary>Looks up a colour space name no Default colour space replaces: in the stream's resources, then the page's.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The content stream.</param>
    /// <param name="name">The name.</param>
    /// <returns>What kind of colour space the name gives.</returns>
    private static PdfAColorSpaceKind FindNamedColorSpace(PdfAGraphicsWalk walk, PdfAContentState state, PdfName name)
    {
        var scope = state.Scope;
        if (!scope.IsPage)
        {
            var kind = PdfAColorSpaces.FromResource(walk, scope.Local, name, false);
            if (kind != PdfAColorSpaceKind.Invalid || !scope.HasPage)
            {
                return kind;
            }

            kind = PdfAColorSpaces.FromResource(walk, scope.Page, name, false);
            FailInheritedColorSpace(walk, state, kind);
            return kind;
        }

        if (!scope.HasPage)
        {
            return PdfAColorSpaces.FromResource(walk, null, name, false);
        }

        var found = PdfAColorSpaces.FromResource(walk, scope.Page, name, false);
        if (scope.PageInherited)
        {
            FailInheritedColorSpace(walk, state, found);
        }

        return found;
    }

    /// <summary>Records an inherited colour space; the device spaces are built in, so they never count.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The content stream.</param>
    /// <param name="kind">The colour space found.</param>
    private static void FailInheritedColorSpace(PdfAGraphicsWalk walk, PdfAContentState state, PdfAColorSpaceKind kind)
    {
        if (kind is not (PdfAColorSpaceKind.Invalid or PdfAColorSpaceKind.Device))
        {
            FailInherited(walk, state);
        }
    }

    /// <summary>Records a name found only in resources the stream does not own.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The content stream.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FailInherited(PdfAGraphicsWalk walk, PdfAContentState state) =>
        walk.Context.Fail(PdfARule.ContentInheritedResources, state.Where);

    /// <summary>Reads a resource from one resource dictionary and checks it is usable for its category.</summary>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="category">The category.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The resource.</param>
    /// <returns><see langword="true"/> when the resource is defined and usable.</returns>
    private static bool TryGet(PdfDictionary? resources, KnownName category, PdfName name, out PdfValue value)
    {
        value = default;
        if (resources?.GetDictionary(category) is not { } entries || entries.GetRaw(name).IsNull)
        {
            return false;
        }

        value = entries.Get(name);
        return category switch
        {
            KnownName.Font => IsFont(value),
            KnownName.XObject => IsXObject(value),
            KnownName.Pattern => PdfAColorSpaces.IsPattern(value),
            _ => true,
        };
    }

    /// <summary>Determines whether a font resource is a dictionary of a font type veraPDF reads.</summary>
    /// <param name="value">The resolved resource.</param>
    /// <returns><see langword="true"/> for Type 1, multiple master, TrueType, Type 3 and Type 0 fonts.</returns>
    private static bool IsFont(PdfValue value) =>
        value.Kind == PdfKind.Dictionary
        && value.AsDictionary()!.GetName(KnownName.Subtype).ToKnownName() is KnownName.Type1 or KnownName.MMType1 or KnownName.TrueType or KnownName.Type3 or KnownName.Type0;

    /// <summary>Determines whether an XObject resource has a subtype veraPDF reads.</summary>
    /// <param name="value">The resolved resource.</param>
    /// <returns><see langword="true"/> for image, form and PostScript XObjects.</returns>
    private static bool IsXObject(PdfValue value) =>
        value.AsDictionary()?.GetName(KnownName.Subtype).ToKnownName() is KnownName.Image or KnownName.Form or KnownName.PS;
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>Writes a page's transition (<c>/Trans</c>, Table 161 in ISO 32000-2 12.4.4.2) and its automatic advance time (<c>/Dur</c>, 12.4.4.1).</summary>
internal static class PdfTransitionEditor
{
    /// <summary>The direction value that stands for the name <c>None</c> in the model.</summary>
    private const int NoDirection = -1;

    /// <summary>The direction of a Glitter transition that runs corner to corner (Table 161).</summary>
    private const int DiagonalDirection = 315;

    /// <summary>The first version with the Fly, Push, Cover, Uncover and Fade styles (Table 161).</summary>
    private static readonly PdfVersion NewStylesVersion = new(1, 5);

    /// <summary>The styles with no extra rules, and the styles that need a version.</summary>
    private static readonly string[] Styles = ["Split", "Blinds", "Box", "Wipe", "Dissolve", "Glitter", "R", "Fly", "Push", "Cover", "Uncover", "Fade"];

    /// <summary>The directions in degrees the standard allows (Table 161).</summary>
    private static readonly int[] Directions = [0, 90, 180, 270, DiagonalDirection];

    /// <summary>The styles from PDF 1.5.</summary>
    private static readonly string[] NewStyles = ["Fly", "Push", "Cover", "Uncover", "Fade"];

    /// <summary>The styles that take a direction (Table 161).</summary>
    private static readonly string[] DirectionStyles = ["Wipe", "Glitter", "Fly", "Cover", "Uncover", "Push"];

    /// <summary>Sets or removes a page's transition and advance time.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="pageId">The page's object.</param>
    /// <param name="transition">The transition, or <see langword="null"/> to remove both.</param>
    /// <exception cref="ArgumentException">A value is not one the standard allows for the style.</exception>
    internal static void Set(PdfObjectEdits edits, PdfObjectId pageId, PdfPageTransition? transition)
    {
        var page = edits.Edit(pageId) ?? throw new ArgumentException("The page is not a dictionary.", nameof(pageId));
        _ = page.Remove(KnownName.Trans);
        var duration = edits.Store.Names.Intern("Dur"u8);
        _ = page.Remove(duration);
        if (transition is null)
        {
            return;
        }

        Validate(transition);
        var store = edits.Store;
        var trans = new PdfDictionary(store);
        trans.Set(KnownName.Type, PdfValue.FromName(KnownName.Trans));
        trans.Set(KnownName.S, PdfValue.FromName(store.Names.Intern(transition.Style)));
        trans.Set(KnownName.D, PdfValue.FromReal(transition.Duration));
        WriteStyleKeys(store, trans, transition);
        page.Set(KnownName.Trans, PdfValue.FromDictionary(trans));
        if (transition.AdvanceAfter is { } advance)
        {
            page.Set(duration, PdfValue.FromReal(advance));
        }

        if (Array.IndexOf(NewStyles, transition.Style) >= 0)
        {
            PdfCatalogEditor.RaiseVersion(edits, NewStylesVersion);
        }
    }

    /// <summary>Writes the entries that only some styles use.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="trans">The transition dictionary.</param>
    /// <param name="transition">The transition.</param>
    private static void WriteStyleKeys(PdfObjectStore store, PdfDictionary trans, PdfPageTransition transition)
    {
        var style = transition.Style;
        if (style is "Split" or "Blinds")
        {
            trans.Set(store.Names.Intern("Dm"u8), PdfValue.FromName(store.Names.Intern(transition.Dimension)));
        }

        if (style is "Split" or "Box" or "Fly")
        {
            trans.Set(store.Names.Intern("M"u8), PdfValue.FromName(store.Names.Intern(transition.Motion)));
        }

        if (Array.IndexOf(DirectionStyles, style) >= 0 && transition.Direction != 0)
        {
            trans.Set(
                store.Names.Intern("Di"u8),
                transition.Direction == NoDirection ? PdfValue.FromName(store.Names.Intern("None"u8)) : PdfValue.FromInteger(transition.Direction));
        }

        if (style != "Fly")
        {
            return;
        }

        trans.Set(store.Names.Intern("SS"u8), PdfValue.FromReal(transition.Scale));
        trans.Set(store.Names.Intern("B"u8), PdfValue.FromBoolean(transition.Rectangular));
    }

    /// <summary>Checks a transition against the values the standard allows.</summary>
    /// <param name="transition">The transition.</param>
    /// <exception cref="ArgumentException">A value is not allowed.</exception>
    private static void Validate(PdfPageTransition transition)
    {
        if (Array.IndexOf(Styles, transition.Style) < 0)
        {
            throw new ArgumentException($"'{transition.Style}' is not a page transition style.", nameof(transition));
        }

        if (!(transition.Duration >= 0) || transition.AdvanceAfter is < 0)
        {
            throw new ArgumentException("A transition and the time before the page advances cannot be negative.", nameof(transition));
        }

        if (transition.Dimension is not ("H" or "V") || transition.Motion is not ("I" or "O"))
        {
            throw new ArgumentException("A transition's dimension must be H or V and its motion I or O.", nameof(transition));
        }

        ValidateDirection(transition);
    }

    /// <summary>Checks a transition's direction and scale.</summary>
    /// <param name="transition">The transition.</param>
    /// <exception cref="ArgumentException">A value is not allowed.</exception>
    private static void ValidateDirection(PdfPageTransition transition)
    {
        var direction = transition.Direction;
        if (direction != NoDirection && Array.IndexOf(Directions, direction) < 0)
        {
            throw new ArgumentException("A transition's direction must be 0, 90, 180, 270 or 315 degrees, or none.", nameof(transition));
        }

        if (direction == DiagonalDirection && transition.Style != "Glitter")
        {
            throw new ArgumentException("Only a Glitter transition can run at 315 degrees.", nameof(transition));
        }

        if (!(transition.Scale > 0))
        {
            throw new ArgumentException("A transition's scale must be more than zero.", nameof(transition));
        }
    }
}

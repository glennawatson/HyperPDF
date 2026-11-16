// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The part of Edit mode that sets existing text in another font and size.</summary>
public sealed partial class PageContentEditViewModel
{
    /// <summary>Sets the picked text in the font chosen for text, at the size it has.</summary>
    /// <returns><see langword="true"/> when text is picked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RestyleSelectedText() => RestyleSelectedText(0F);

    /// <summary>Sets the picked text in the font chosen for text, at a size.</summary>
    /// <param name="points">The size in points, or zero or less to keep the size the text has.</param>
    /// <returns><see langword="true"/> when text is picked.</returns>
    public bool RestyleSelectedText(float points)
    {
        if (!CanShape(out var picked) || picked.Object.Kind != PageObjectKind.Text)
        {
            return false;
        }

        var style = new PageTextStyle(NewTextFamily, NewTextBold, NewTextItalic, points);
        Track(Edit(picked, _ => Editor!.RestyleTextAsync(picked.Page, picked.Object.Index, style, _lifetime), picked.Object.Bounds, UiText.Get("Changed the font.")));
        return true;
    }

    /// <summary>Chooses the family of font for new text and for restyled text, from a menu.</summary>
    /// <param name="family">The name of a <see cref="PageTextFamily"/>.</param>
    [ReactiveCommand]
    private void ChooseTextFamily(string family)
    {
        if (Enum.TryParse<PageTextFamily>(family, out var chosen))
        {
            NewTextFamily = chosen;
        }
    }

    /// <summary>Makes new text and restyled text bold, or not, from a menu.</summary>
    [ReactiveCommand]
    private void ToggleTextBold() => NewTextBold = !NewTextBold;

    /// <summary>Makes new text and restyled text italic, or not, from a menu.</summary>
    [ReactiveCommand]
    private void ToggleTextItalic() => NewTextItalic = !NewTextItalic;

    /// <summary>Sets the picked text in the font chosen for text, from a menu.</summary>
    [ReactiveCommand]
    private void ApplyTextFont() => _ = RestyleSelectedText();

    /// <summary>Sets the picked text to a size, from a menu.</summary>
    /// <param name="points">The size in points as digits.</param>
    [ReactiveCommand]
    private void SetPickedTextSize(string points)
    {
        if (float.TryParse(points, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size > 0)
        {
            _ = RestyleSelectedText(size);
        }
    }
}

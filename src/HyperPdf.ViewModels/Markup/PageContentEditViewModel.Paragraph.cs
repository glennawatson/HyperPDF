// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The commands of Edit mode that retype a whole paragraph, so the new words wrap to the width the paragraph had.</summary>
public sealed partial class PageContentEditViewModel
{
    /// <summary>The numbers of the lines of the paragraph being retyped, or <see langword="null"/> when a single line is.</summary>
    private int[]? _paragraphLines;

    /// <summary>The words the paragraph had when the retyping began, joined into one run, to tell whether they changed.</summary>
    private string _paragraphWords = string.Empty;

    /// <summary>Starts retyping every line of the picked text's paragraph as one text. A line with no paragraph around it is retyped alone.</summary>
    /// <returns><see langword="true"/> when a text object is picked.</returns>
    public bool BeginParagraphEdit()
    {
        if (Selected is not { Object.Kind: PageObjectKind.Text } picked)
        {
            return false;
        }

        if (!_pages.TryGetValue(picked.Page, out var objects))
        {
            return BeginTextEdit();
        }

        var lines = ParagraphFinder.Find(objects, picked.Object);
        if (lines.Length < 2)
        {
            return BeginTextEdit();
        }

        _paragraphLines = lines;
        _paragraphWords = JoinLines(objects, lines);
        EditingText = _paragraphWords;
        TextEdit = picked;
        MarksVersion++;
        return true;
    }

    /// <summary>Joins the words of some lines of a page, in the order given, with a space between them.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="lines">The numbers of the lines.</param>
    /// <returns>The words.</returns>
    internal static string JoinLines(PageObjectInfo[] objects, int[] lines)
    {
        var words = new StringBuilder();
        foreach (var index in lines)
        {
            foreach (var item in objects)
            {
                if (item.Index != index)
                {
                    continue;
                }

                _ = words.Length > 0 ? words.Append(' ').Append(item.Text.Trim()) : words.Append(item.Text.Trim());
                break;
            }
        }

        return words.ToString();
    }

    /// <summary>Starts retyping the picked text's paragraph from a menu.</summary>
    [ReactiveCommand]
    private void EditPickedParagraph() => _ = BeginParagraphEdit();

    /// <summary>Writes the retyped paragraph to the page, when its words changed.</summary>
    /// <param name="edit">The picked line the retyping began from.</param>
    /// <param name="lines">The numbers of the paragraph's lines.</param>
    /// <param name="text">The new words.</param>
    private void CommitParagraph(PageObjectPick edit, int[] lines, string text)
    {
        if (text != _paragraphWords)
        {
            Track(Edit(edit, _ => Editor!.ReflowParagraph(edit.Page, lines, text), edit.Object.Bounds, UiText.Get("Changed the paragraph.")));
        }
    }
}

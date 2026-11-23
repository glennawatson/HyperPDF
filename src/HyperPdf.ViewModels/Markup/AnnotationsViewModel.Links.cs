// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Links made from selected text.</summary>
public sealed partial class AnnotationsViewModel
{
    /// <summary>
    /// Asks for a web address or a page and links the selected text to it. Each page the selection covers gets one link over the
    /// lines selected on it.
    /// </summary>
    /// <param name="lines">The selected lines, by page.</param>
    /// <returns>A signal that ends when the link is added or the question is given up.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> LinkSelection(IReadOnlyDictionary<int, List<PageRect>> lines)
    {
        if (!CanAnnotate || lines.Count == 0)
        {
            return Signal.Return(RxVoid.Default);
        }

        var asked = new TextPrompt(UiText.Get("Add a link"), UiText.Get("A web address such as https://example.com, or page: and a page number"), string.Empty, UiText.Get("Add the link"), false);
        return PromptInteraction.Handle(asked).ToSignal()
            .Select(target => target is { Length: > 0 } ? LinkLines(lines, target.Trim()) : RxVoid.Default);
    }

    /// <summary>Adds one link on each page over the lines selected on it.</summary>
    /// <param name="lines">The selected lines, by page.</param>
    /// <param name="target">The address or page typed.</param>
    /// <returns>The completion value.</returns>
    private RxVoid LinkLines(IReadOnlyDictionary<int, List<PageRect>> lines, string target)
    {
        var added = 0;
        foreach (var page in lines)
        {
            if (page.Value.Count == 0)
            {
                continue;
            }

            var area = page.Value[0];
            for (var i = 1; i < page.Value.Count; i++)
            {
                area = area.Union(page.Value[i]);
            }

            if (AddLink(page.Key, area, target))
            {
                added++;
            }
        }

        if (added == 0)
        {
            _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("That is not a web address or a page of this document. Type an address such as https://example.com, or page: and a page number.")));
        }

        return RxVoid.Default;
    }
}

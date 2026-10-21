// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>What clicking a page does for each annotation tool that works by a single click.</summary>
public static class ClickTools
{
    /// <summary>Runs a click tool: adds a note, text, a stamp or a caret where the page was clicked.</summary>
    /// <param name="annotations">The annotation state, whose tool is the one chosen.</param>
    /// <param name="page">The page clicked.</param>
    /// <param name="point">The point clicked, in page space.</param>
    /// <param name="snaps">Whether text clicked into a place to write fits it; Alt turns this off.</param>
    public static void Run(AnnotationsViewModel annotations, int page, PagePoint point, bool snaps)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        switch (annotations.Tool)
        {
            case AnnotationTool.Note:
            {
                if (annotations.HitTest(page, point) is { Kind: AnnotationKind.Note } existing)
                {
                    _ = annotations.OpenBubble(existing);
                }
                else
                {
                    _ = annotations.BeginNoteBubble(page, point);
                }

                break;
            }

            case AnnotationTool.Text:
            {
                if (annotations.HitTest(page, point) is { Kind: AnnotationKind.TextBox } box)
                {
                    _ = annotations.EditText(box);
                    return;
                }

                _ = snaps && annotations.RegionAt(page, point) is { } region
                    ? annotations.BeginInRegion(page, region)
                    : annotations.BeginText(page, point, 0);
                break;
            }

            case AnnotationTool.Stamp:
            {
                _ = annotations.AddStamp(page, point);
                break;
            }

            case AnnotationTool.Caret:
            {
                _ = annotations.AddCaret(page, point);
                break;
            }

            default:
            {
                break;
            }
        }
    }
}

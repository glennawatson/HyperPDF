// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Help;
using HyperPdf.Viewer.Icons;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Provides localised help for buttons in their toolbar context.</summary>
internal static class ButtonHelp
{
    /// <summary>Describes a main toolbar action.</summary>
    /// <param name="icon">The action's icon.</param>
    /// <returns>The help text, or null to use the control's name.</returns>
    internal static string? Toolbar(ChromeIcon icon) => icon switch
    {
        ChromeIcon.Open => Descriptions.Open,
        ChromeIcon.Save => Descriptions.Save,
        ChromeIcon.Print => Descriptions.Print,
        ChromeIcon.Document => Descriptions.DocumentMenu,
        ChromeIcon.Dual => Descriptions.ViewMenu,
        ChromeIcon.Menu => Descriptions.TabsMenu,
        ChromeIcon.Ruler => Descriptions.ToolsMenu,
        ChromeIcon.Highlight => Descriptions.Annotate,
        ChromeIcon.FillSign => Descriptions.FillSign,
        ChromeIcon.Cursor => UiText.Get("Change the page itself. Click text, a picture or a drawing to pick it, then retype, move or remove it. Shortcut: Ctrl+E"),
        ChromeIcon.Search => Descriptions.Find,
        ChromeIcon.Focus => Descriptions.Focus,
        ChromeIcon.Speaker => Descriptions.ReadAloud,
        ChromeIcon.Sidebar => Descriptions.Sidebar,
        ChromeIcon.Back => Descriptions.Back,
        ChromeIcon.Forward => Descriptions.Forward,
        ChromeIcon.PageUp => Descriptions.PreviousPage,
        ChromeIcon.PageDown => Descriptions.NextPage,
        ChromeIcon.None => Descriptions.PageNumber,
        ChromeIcon.ZoomIn => Descriptions.ZoomIn,
        ChromeIcon.ZoomOut => Descriptions.ZoomOut,
        ChromeIcon.FitWidth => Descriptions.FitWidth,
        ChromeIcon.FitPage => Descriptions.FitPage,
        _ => null,
    };

    /// <summary>Describes an annotation control.</summary>
    /// <param name="icon">The tool's icon.</param>
    /// <returns>The help text, or null to use the control's name.</returns>
    internal static string? Marking(ChromeIcon icon) => icon switch
    {
        ChromeIcon.Cursor => Descriptions.SelectTool,
        ChromeIcon.Highlight => Descriptions.HighlightTool,
        ChromeIcon.Underline => Descriptions.UnderlineTool,
        ChromeIcon.Strike => Descriptions.StrikeTool,
        ChromeIcon.Draw => Descriptions.DrawTool,
        ChromeIcon.Note => Descriptions.NoteTool,
        ChromeIcon.Text => Descriptions.TextTool,
        ChromeIcon.Box => Descriptions.Rectangle,
        ChromeIcon.Oval => Descriptions.Ellipse,
        ChromeIcon.Arrow => Descriptions.Arrow,
        ChromeIcon.Line => Descriptions.Line,
        ChromeIcon.LineWidth => Descriptions.LineWidth,
        ChromeIcon.Undo => Descriptions.Undo,
        ChromeIcon.Redo => Descriptions.Redo,
        _ => null,
    };

    /// <summary>Describes a sidebar selector.</summary>
    /// <param name="icon">The selector's icon.</param>
    /// <returns>The help text, or null to use the selector's name.</returns>
    internal static string? Sidebar(ChromeIcon icon) => icon switch
    {
        ChromeIcon.Thumbnails => Descriptions.Thumbnails,
        ChromeIcon.Outline => Descriptions.Outline,
        ChromeIcon.Search => Descriptions.SearchResults,
        ChromeIcon.Annotations => Descriptions.Annotations,
        ChromeIcon.Attachment => Descriptions.Attachments,
        ChromeIcon.Layers => Descriptions.Layers,
        ChromeIcon.Parts => Descriptions.Parts,
        ChromeIcon.Media => Descriptions.Media,
        ChromeIcon.Tags => Descriptions.Tags,
        _ => null,
    };
}

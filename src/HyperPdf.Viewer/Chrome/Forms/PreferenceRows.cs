// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Preferences;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// The settings the preferences window lists. Each says what it does in a sentence a person new to it can follow, which is
/// shown when the pointer rests on it, together with the value a new install starts with.
/// </summary>
internal static class PreferenceRows
{
    /// <summary>Gets the rows of the window, top to bottom. A row's place in this list is the number its changes come back with.</summary>
    internal static PreferenceRow[] All { get; } =
    [
        PreferenceRow.Heading("Look"),
        PreferenceRow.Choice("Interface language", PreferencesViewModel.InterfaceLanguageOptions, static p => p.InterfaceLanguage, static (p, value) => p.InterfaceLanguage = value)
            .About("The language of the menus and windows. A change takes effect the next time HyperPDF starts."),
        PreferenceRow.Choice("Colour scheme", PreferencesViewModel.ColorSchemeOptions, static p => p.ColorScheme, static (p, value) => p.ColorScheme = value)
            .About("The colours of the window. Calm is soft and low in glare. High contrast gives the strongest difference between words and background."),
        PreferenceRow.Choice("Drawing (next start)", PreferencesViewModel.RenderBackendOptions, static p => p.RenderBackend, static (p, value) => p.RenderBackend = value)
            .Offering(PreferencesViewModel.IsRenderBackendOffered)
            .About("What draws the window. Automatic uses the graphics card, or the processor when that cannot be used. Change it only if pages draw wrongly. It takes effect at the next start."),
        PreferenceRow.Choice("PDF page colour", PreferencesViewModel.PageToneOptions, static p => p.PageTone, static (p, value) => p.PageTone = value)
            .About("The paper and ink colours that pages are shown in. It does not change the file or how it prints."),
        PreferenceRow.Field("Paper, as six hex digits such as 000000", static p => p.CustomPaper, static (p, value) => p.CustomPaper = value).When(static p => p.UsesCustomPageTone)
            .WithDefault()
            .About("The paper colour of the custom page colours."),
        PreferenceRow.Field("Ink, as six hex digits such as FFFF00", static p => p.CustomInk, static (p, value) => p.CustomInk = value).When(static p => p.UsesCustomPageTone)
            .WithDefault()
            .About("The ink colour of the custom page colours."),
        PreferenceRow.Status(static p => p.CustomToneContrast).When(static p => p.UsesCustomPageTone),
        PreferenceRow.Choice("Text size", PreferencesViewModel.FontSizeOptions, static p => p.FontSize, static (p, value) => p.FontSize = value)
            .About("The size of the words in menus, bars and windows. It does not change the page."),
        PreferenceRow.Choice("Tool bar", PreferencesViewModel.ToolbarOptions, static p => p.Toolbar, static (p, value) => p.Toolbar = value)
            .About("Whether tool bar buttons show their names beside their icons."),
        PreferenceRow.Choice("Motion", PreferencesViewModel.MotionOptions, static p => p.Motion, static (p, value) => p.Motion = value)
            .About("Whether zooming glides or jumps straight to the new size. Reduced jumps."),
        PreferenceRow.Choice("Text cursor", PreferencesViewModel.CaretOptions, static p => p.Caret, static (p, value) => p.Caret = value)
            .About("Whether the text cursor blinks or stays steady."),
        PreferenceRow.Heading("Opening documents"),
        PreferenceRow.Choice("Opening zoom", PreferencesViewModel.OpeningZoomOptions, static p => p.OpeningZoom, static (p, value) => p.OpeningZoom = value)
            .About("How large a document is when it first opens."),
        PreferenceRow.Choice("When a file changes on disk", PreferencesViewModel.FileChangeOptions, static p => p.FileChange, static (p, value) => p.FileChange = value)
            .About("What happens when another program saves over a file that is open here."),
        PreferenceRow.Toggle("Reopen at the last page read", static p => p.ReopenAtLastPage, static (p, on) => p.ReopenAtLastPage = on)
            .About("Opens a document at the page you were on when you last closed it."),
        PreferenceRow.Toggle("Follow the document's opening action", static p => p.FollowOpenAction, static (p, on) => p.FollowOpenAction = on)
            .About("Lets a document choose the page it opens at."),
        PreferenceRow.Toggle("Follow the document's side panel choice", static p => p.FollowPageMode, static (p, on) => p.FollowPageMode = on)
            .About("Lets a document choose the side panel it opens with, such as its bookmarks."),
        PreferenceRow.Toggle("Follow the document's page layout", static p => p.FollowPageLayout, static (p, on) => p.FollowPageLayout = on)
            .About("Lets a document choose its page layout and reading direction, such as two pages side by side."),
        PreferenceRow.Toggle("Show the document's title instead of its file name", static p => p.FollowDisplayDocTitle, static (p, on) => p.FollowDisplayDocTitle = on)
            .About("Shows the title written inside a document on its tab, when the document asks for that."),
        PreferenceRow.Toggle("Follow the document's print settings", static p => p.FollowPrintDefaults, static (p, on) => p.FollowPrintDefaults = on)
            .About("Starts the print window with the choices a document asks for."),
        PreferenceRow.Heading("Reading and forms"),
        PreferenceRow.Toggle("Keep the keyboard cursor on the page", static p => p.KeepKeyboardCursor, static (p, on) => p.KeepKeyboardCursor = on)
            .About("Opens documents with a text cursor on the page, which the keyboard moves."),
        PreferenceRow.Toggle("Check spelling in forms and notes", static p => p.CheckSpelling, static (p, on) => p.CheckSpelling = on)
            .About("Underlines misspelled words as you type in forms and notes."),
        PreferenceRow.Toggle("Ignore bookmark colours", static p => p.IgnoreOutlineColours, static (p, on) => p.IgnoreOutlineColours = on)
            .About("Shows every bookmark in the window's own text colour, whatever colour the document gives it."),
        PreferenceRow.Toggle("Play page transitions", static p => p.FollowPageTransitions, static (p, on) => p.FollowPageTransitions = on)
            .About("While presenting, plays the page change effects a document asks for."),
        PreferenceRow.Toggle("Turn pages on the document's timings", static p => p.FollowPageTimings, static (p, on) => p.FollowPageTimings = on)
            .About("While presenting, moves to the next page by itself after the time a page asks for."),
        PreferenceRow.Toggle("List fonts that are only for preview", static p => p.ShowPreviewOnlyFonts, static (p, on) => p.ShowPreviewOnlyFonts = on)
            .About("Offers fonts for typing that show on screen but cannot be saved in a PDF."),
        PreferenceRow.Choice("Form field highlight colour", PreferencesViewModel.FormHighlightColorOptions, static p => p.FormHighlightColor, static (p, value) => p.FormHighlightColor = value)
            .About("The colour that marks the fields of a form you can fill in."),
        PreferenceRow.Choice(
            "Form field highlight strength",
            PreferencesViewModel.FormHighlightOpacityOptions,
            static p => p.FormHighlightOpacity,
            static (p, value) => p.FormHighlightOpacity = value)
            .About("How strongly the fields of a form are marked. Off shows no mark."),
        PreferenceRow.Field("Name on comments", static p => p.CommentAuthor, static (p, value) => p.CommentAuthor = value)
            .About("The name written on notes you add. Empty uses this computer's user name."),
        PreferenceRow.Heading("Sharpness of text and pictures"),
        PreferenceRow.Choice("Edges", SharpnessViewModel.PresetOptions, static p => p.Sharpness.Preset, static (p, value) => p.Sharpness.Preset = value)
            .About("A ready-made set of the sharpness choices below. Custom means the choices below match no set."),
        PreferenceRow.Choice("Text edges", SharpnessViewModel.TextEdgeOptions, static p => p.Sharpness.TextEdges, static (p, value) => p.Sharpness.TextEdges = value)
            .About("How the edges of letters are drawn. Smooth blends them into the page. Sharp makes each pixel fully dark or fully clear."),
        PreferenceRow.Choice("Fit small text to pixels", SharpnessViewModel.TextHintingOptions, static p => p.Sharpness.TextHinting, static (p, value) => p.Sharpness.TextHinting = value)
            .About("Moves the strokes of small letters onto whole pixels, so they look firmer."),
        PreferenceRow.Choice("Small text contrast", SharpnessViewModel.TextContrastOptions, static p => p.Sharpness.TextContrast, static (p, value) => p.Sharpness.TextContrast = value)
            .About("How much darker small text is drawn."),
        PreferenceRow.Choice("Fit lines to pixels", SharpnessViewModel.StrokeAdjustmentOptions, static p => p.Sharpness.StrokeAdjustment, static (p, value) => p.Sharpness.StrokeAdjustment = value)
            .About("Moves thin lines onto whole pixels, so they are even and crisp."),
        PreferenceRow.Choice("Thinnest line", SharpnessViewModel.MinimumLineOptions, static p => p.Sharpness.MinimumLineWidth, static (p, value) => p.Sharpness.MinimumLineWidth = value)
            .About("The thinnest a line is drawn, so very thin lines do not vanish."),
        PreferenceRow.Toggle("Smooth the edges of lines and shapes", static p => p.Sharpness.SmoothGraphics, static (p, on) => p.Sharpness.SmoothGraphics = on)
            .About("Blends the edges of lines and shapes into the page."),
        PreferenceRow.Toggle("Smooth pictures", static p => p.Sharpness.SmoothImages, static (p, on) => p.Sharpness.SmoothImages = on)
            .About("Blends the pixels of a picture that is shown larger or smaller than it is, so it does not look blocky."),
        PreferenceRow.Toggle("Average pixels when shrinking big pictures", static p => p.Sharpness.AreaImageReduction, static (p, on) => p.Sharpness.AreaImageReduction = on)
            .About("Shrinks a big picture by averaging the pixels each screen pixel covers."),
        PreferenceRow.Toggle("Remember the shapes of small letters", static p => p.Sharpness.SmallTextMasks, static (p, on) => p.Sharpness.SmallTextMasks = on)
            .About("Draws small letters from shapes kept from the last time each was drawn."),
        PreferenceRow.Heading("Services"),
        PreferenceRow.Note("The voice for reading aloud, its speed and its Azure key are in Read Aloud Settings, on the Read Aloud bar and in the Reading Aids menu."),
        PreferenceRow.Field("Timestamp server for signing", static p => p.TimestampServer, static (p, value) => p.TimestampServer = value)
            .About("The address of a server that adds a trusted time to signatures you make with a certificate. Empty adds none."),
    ];
}

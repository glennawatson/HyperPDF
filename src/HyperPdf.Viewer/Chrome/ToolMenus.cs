// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Text;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The submenus of the main menu that reach the document's tools: copies, pages, bookmarks, reading aids, signing and the tools
/// that work on the page. Every entry runs a view model command, so a tool works the same from the menu, a key or a screen reader.
/// </summary>
internal static class ToolMenus
{
    /// <summary>The name of the icon of an entry that chooses pages: a ticked box.</summary>
    private const string TickedIcon = "CheckOn";

    /// <summary>The name of the icon of an entry that adds pages.</summary>
    private const string AddIcon = "Add";

    /// <summary>The pages Swap works on: two.</summary>
    private const int PairOfPages = 2;

    /// <summary>How close two spacings are for them to be the same choice.</summary>
    private const float SpacingTolerance = 0.001F;

    /// <summary>The percentage shown at actual size.</summary>
    private const int ActualSize = 100;

    /// <summary>The tolerance when identifying a preset zoom factor.</summary>
    private const double ZoomTolerance = 1e-6;

    /// <summary>The zoom levels offered, in percent.</summary>
    private static readonly string[] ZoomLevels = ["50", "75", "100", "125", "150", "200", "400"];

    /// <summary>How solid the picked objects can be made, in percent.</summary>
    private static readonly string[] Opacities = ["100", "75", "50", "25"];

    /// <summary>The sides a picture can be cropped from, with the words of the entry that crops each, in English.</summary>
    private static readonly CropSide[] CropSides =
    [
        new("all", "Crop {0}% off Every Side of the Picked Object"),
        new("left", "Crop {0}% off the Left of the Picked Object"),
        new("top", "Crop {0}% off the Top of the Picked Object"),
        new("right", "Crop {0}% off the Right of the Picked Object"),
        new("bottom", "Crop {0}% off the Bottom of the Picked Object"),
    ];

    /// <summary>The shares of a picture a crop menu entry cuts off, in percent.</summary>
    private static readonly string[] CropPercents = ["5", "10", "25"];

    /// <summary>The dash patterns the lines of picked objects can have, by name.</summary>
    private static readonly string[] DashNames = ["Solid", "Dashed", "Dotted", "DashDot"];

    /// <summary>How the colours of picked objects can mix with the page, by blend mode name.</summary>
    private static readonly string[] BlendNames =
    [
        "Normal", "Multiply", "Screen", "Overlay", "Darken", "Lighten", "ColorDodge", "ColorBurn",
        "HardLight", "SoftLight", "Difference", "Exclusion", "Hue", "Saturation", "Color", "Luminosity",
    ];

    /// <summary>The gradients the picked objects can be filled with, with the words of the entry that fills with each, in English.</summary>
    private static readonly GradientChoice[] Gradients =
    [
        new("Fill the Picked Object with a Blue to White Gradient, Left to Right", "linear:0:1F4E9E:FFFFFF"),
        new("Fill the Picked Object with a Blue to White Gradient, Top to Bottom", "linear:270:1F4E9E:FFFFFF"),
        new("Fill the Picked Object with a Black to White Gradient, Left to Right", "linear:0:000000:FFFFFF"),
        new("Fill the Picked Object with a Gold to Red Gradient, Left to Right", "linear:0:F2B705:B3261E"),
        new("Fill the Picked Object with a White to Blue Gradient from the Middle", "radial:FFFFFF:1F4E9E"),
    ];

    /// <summary>The line ends the lines of picked objects can have, by name.</summary>
    private static readonly string[] CapNames = ["Butt", "Round", "Square"];

    /// <summary>The corners the lines of picked objects can have, by name.</summary>
    private static readonly string[] JoinNames = ["Miter", "Round", "Bevel"];

    /// <summary>The words of the stamps that can be chosen.</summary>
    private static readonly string[] StampLabels = ["APPROVED", "REVIEWED", "DRAFT", "CONFIDENTIAL", "FINAL", "NOT APPROVED"];

    /// <summary>The sizes in points the menus offer for picked text.</summary>
    private static readonly string[] TextSizes = ["8", "10", "12", "14", "18", "24", "36"];

    /// <summary>The colours the menus offer for the fill and the line of a picked object.</summary>
    private static readonly PaletteColour[] PaletteColours =
    [
        new("Black", "000000"),
        new("White", "FFFFFF"),
        new("Grey", "808080"),
        new("Red", "D32F2F"),
        new("Orange", "F57C00"),
        new("Yellow", "FBC02D"),
        new("Green", "388E3C"),
        new("Blue", "1976D2"),
        new("Purple", "7B1FA2"),
    ];

    /// <summary>Lists the tools for measuring, copying a page area and recognising text.</summary>
    /// <param name="tab">The open document.</param>
    /// <returns>The tools in menu order.</returns>
    internal static List<MenuEntry> Tools(DocumentTabViewModel tab) =>
    [
        MenuEntry.Radio(UiText.Get("_Measure"), tab.Measure.ToggleCommand, null, tab.Measure.IsOn),
        MenuEntry.Radio(UiText.Get("_Snapshot"), tab.PageTools.ToggleSnapshotToolCommand, null, tab.PageTools.IsSnapshotTool),
        Item("Recognise _Text", tab.TextRecognition.RecognizeCommand),
        Item("Recognise Text in Another _Language…", tab.TextRecognition.ChooseLanguageCommand),
    ];

    /// <summary>Lists the kinds of copy that can be saved.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Copies(DocumentTabViewModel tab)
    {
        var copies = tab.Copies;
        return
        [
            Copy(copies.SaveCopy, "Copy"),
            Copy(copies.Optimize, "Save"),
            Copy(copies.Archive, "Save"),
            Copy(copies.Pdf20, "Save"),
            Copy(copies.Flatten, "Layers"),
            Copy(copies.ExportPages, "Save"),
            MenuEntry.Separator(),
            Copy(copies.Protect, "Lock"),
            Copy(copies.Unprotect, "Unlock"),
            Copy(copies.Redact, "Close"),
        ];
    }

    /// <summary>Lists what can be done to the pages of the document.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Pages(DocumentTabViewModel tab)
    {
        var pages = tab.Pages;
        return
        [
            Item("Turn the Page _Right", pages.RotateClockwiseCommand),
            Item("Turn the Page _Left", pages.RotateCounterclockwiseCommand),
            Item("Move the Page _Earlier", pages.MoveEarlierCommand),
            Item("Move the Page La_ter", pages.MoveLaterCommand),
            Item("_Duplicate the Page", pages.DuplicateCommand),
            Item("Re_verse the Order of the Pages", pages.ReverseCommand, "Reverse"),
            MenuEntry.Item(UiText.Get("S_wap the Two Selected Pages"), null, pages.ReverseCommand, null, pages.SelectedPages.Length == PairOfPages, "Reverse"),
            Item("Delete the Page", pages.DeleteCommand),
            MenuEntry.Separator(),
            MenuEntry.Radio(UiText.Get("_Organise Pages"), tab.ToggleOrganisePagesCommand, null, pages.IsOrganising),
            Item("Select _Every Page", pages.SelectAllCommand),
            Item("Select the _Odd Pages", pages.SelectOddCommand, TickedIcon),
            Item("Select the Eve_n Pages", pages.SelectEvenCommand, TickedIcon),
            Item("Add the Page I Am On to the Selection", pages.AddCurrentToSelectionCommand),
            MenuEntry.Separator(),
            Item("E_xtract Pages…", pages.ExtractCommand),
            Item("Save Each Page as Its Own _File…", pages.SplitPagesCommand, "Split"),
            Item("Add a _Blank Page", pages.InsertBlankPageCommand, AddIcon),
            Item("Re_place Pages with Pages from Files…", pages.ReplaceCommand, "Replace"),
            Item("_Insert Pages from Files…", pages.InsertFilesCommand),
            Item("Insert _Pictures as Pages…", pages.InsertImagesCommand),
            Item("_Merge Files…", pages.MergeFilesCommand),
            Item("S_huffle In the Pages of a File…", pages.InterleaveCommand, AddIcon),
            Item("Save the Parts Between _Bookmarks as Files…", pages.SplitAtBookmarksCommand, "Split"),
            MenuEntry.Separator(),
            MenuEntry.Item(pages.UndoText, null, pages.UndoCommand, null, ((ICommand)pages.UndoCommand).CanExecute(null), "Undo"),
            MenuEntry.Item(pages.RedoText, null, pages.RedoCommand, null, ((ICommand)pages.RedoCommand).CanExecute(null), "Redo"),
        ];
    }

    /// <summary>Lists what can be added to the pages while organising them: a blank page, the pages of files, pictures, and files joined on at the end.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> AddPages(DocumentTabViewModel tab)
    {
        var pages = tab.Pages;
        return
        [
            Item("A _Blank Page", pages.InsertBlankPageCommand, AddIcon),
            Item("_Pages from Files…", pages.InsertFilesCommand, AddIcon),
            Item("Pictures as P_ages…", pages.InsertImagesCommand, AddIcon),
            Item("_Files Joined on at the End…", pages.MergeFilesCommand, AddIcon),
            Item("Pages of a File _Shuffled In…", pages.InterleaveCommand, AddIcon),
        ];
    }

    /// <summary>Lists the ways to choose the pages the page tools change.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> ChoosePages(DocumentTabViewModel tab)
    {
        var pages = tab.Pages;
        return
        [
            Item("_Every Page", pages.SelectAllCommand, TickedIcon),
            Item("The _Odd Pages", pages.SelectOddCommand, TickedIcon),
            Item("The Eve_n Pages", pages.SelectEvenCommand, TickedIcon),
            Item("Add the _Page I Am On", pages.AddCurrentToSelectionCommand, TickedIcon),
        ];
    }

    /// <summary>Lists what can be added around the content of the pages: watermarks, headers, footers, page and Bates numbers, a background colour and a crop.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Decorations(DocumentTabViewModel tab)
    {
        var decorations = tab.Decorations;
        return
        [
            Item("Add a _Watermark…", decorations.ShowWatermarkCommand),
            Item("Add a _Header…", decorations.ShowHeaderCommand),
            Item("Add a _Footer…", decorations.ShowFooterCommand),
            Item("Add _Page Numbers…", decorations.ShowPageNumbersCommand),
            Item("Add _Bates Numbers…", decorations.ShowBatesCommand),
            MenuEntry.Separator(),
            Item("Fill the Page _Background…", decorations.ShowBackgroundCommand),
            Item("_Crop the Pages…", decorations.ShowCropCommand),
            MenuEntry.Separator(),
            Item("Number the Pages i, ii, iii or A-1…", tab.Pages.Labels.ShowCommand),
        ];
    }

    /// <summary>Lists what can be done to the bookmarks of the document.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Bookmarks(DocumentTabViewModel tab)
    {
        var bookmarks = tab.OutlineEditing;
        return
        [
            Item("_Add a Bookmark Here…", bookmarks.AddCommand),
            Item("_Rename the Bookmark…", bookmarks.RenameCommand),
            Item("Re_move the Bookmark", bookmarks.RemoveCommand),
            MenuEntry.Separator(),
            Item("Move _Up", bookmarks.MoveUpCommand),
            Item("Move _Down", bookmarks.MoveDownCommand),
            Item("Move _In", bookmarks.IndentCommand),
            Item("Move _Out", bookmarks.OutdentCommand),
        ];
    }

    /// <summary>Lists the reading aids: read aloud, focus mode, reading mode, slow scrolling and text recognition.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Reading(DocumentTabViewModel tab)
    {
        var aloud = tab.ReadAloud;
        var focus = tab.FocusMode;
        var scroll = tab.PageTools;
        var text = tab.TextRecognition;
        return
        [
            MenuEntry.Radio(UiText.Get("Read _Aloud"), aloud.ToggleCommand, null, aloud.IsOpen),
            Item("_Play or Pause", aloud.PlayPauseCommand),
            Item("Read This _Page", aloud.ReadThisPageCommand),
            Item("Read to the _End", aloud.ReadToEndCommand),
            Item("Sentence _Before", aloud.PreviousCommand),
            Item("_Next Sentence", aloud.NextCommand),
            Item("Read Aloud _Settings…", aloud.ShowSettingsCommand),
            MenuEntry.Separator(),
            MenuEntry.Radio(UiText.Get("_Focus Mode"), focus.ToggleCommand, null, focus.IsOn),
            Item("Larger Focus Text", focus.LargerCommand),
            Item("Smaller Focus Text", focus.SmallerCommand),
            MenuEntry.Radio(UiText.Get("_Reading Mode"), tab.ReadModeCommand, null, tab.IsReading),
            MenuEntry.Radio(UiText.Get("Presen_tation"), tab.PresentCommand, null, tab.IsPresenting),
            MenuEntry.Separator(),
            MenuEntry.Radio(UiText.Get("_Scroll Slowly by Itself"), scroll.ToggleAutoScrollCommand, null, scroll.IsAutoScrolling),
            Item("Scroll Faster", scroll.FasterAutoScrollCommand),
            Item("Scroll Slower", scroll.SlowerAutoScrollCommand),
            MenuEntry.Separator(),
            Item("Recognise _Text in Scanned Pages", text.RecognizeCommand),
            Item("Choose the Language to Recognise…", text.ChooseLanguageCommand),
            Item("Manage the Recognition Languages…", text.ManageLanguagesCommand),
            Item("Stop Recognising", text.StopCommand),
            MenuEntry.Separator(),
            Item("Next Place in the Article", tab.Parts.NextPlaceCommand),
            Item("Previous Place in the Article", tab.Parts.PreviousPlaceCommand),
        ];
    }

    /// <summary>Lists the page layouts, the scroll directions and the zoom levels.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries, with the ones in use ticked.</returns>
    internal static List<MenuEntry> Layout(DocumentTabViewModel tab)
    {
        var entries = new List<MenuEntry>
        {
            MenuEntry.Radio(UiText.Get("_One Page Wide"), tab.SetLayoutCommand, nameof(PageLayoutMode.Single), tab.LayoutMode == PageLayoutMode.Single),
            MenuEntry.Radio(UiText.Get("_Two Pages Wide"), tab.SetLayoutCommand, nameof(PageLayoutMode.Dual), tab.LayoutMode == PageLayoutMode.Dual),
            MenuEntry.Radio(UiText.Get("Two Pages Wide with a _Cover"), tab.SetLayoutCommand, nameof(PageLayoutMode.DualCover), tab.LayoutMode == PageLayoutMode.DualCover),
            MenuEntry.Separator(),
            MenuEntry.Radio(UiText.Get("Scroll _Down the Pages"), tab.SetScrollModeCommand, nameof(ScrollMode.Vertical), tab.ScrollMode == ScrollMode.Vertical),
            MenuEntry.Radio(UiText.Get("Scroll _Across the Pages"), tab.SetScrollModeCommand, nameof(ScrollMode.Horizontal), tab.ScrollMode == ScrollMode.Horizontal),
            MenuEntry.Radio(UiText.Get("_Wrap the Pages"), tab.SetScrollModeCommand, nameof(ScrollMode.Wrapped), tab.ScrollMode == ScrollMode.Wrapped),
            MenuEntry.Separator(),
        };
        foreach (var level in ZoomLevels)
        {
            entries.Add(MenuEntry.Item(UiText.Format("Zoom to {0}%", level), null, tab.SetZoomCommand, level, true));
        }

        return entries;
    }

    /// <summary>Lists every voice that can read aloud, whichever engine speaks it, marking the one chosen. A rule sets each engine's voices apart.</summary>
    /// <param name="aloud">Reading aloud.</param>
    /// <returns>The voices.</returns>
    internal static List<MenuEntry> Voices(HyperPdf.ViewModels.Reading.ReadAloudViewModel aloud)
    {
        var entries = new List<MenuEntry>(aloud.VoiceNames.Length + 1);
        var engine = string.Empty;
        for (var i = 0; i < aloud.VoiceNames.Length; i++)
        {
            // A voice is named with its engine first, so the words before the colon tell where one engine's voices end.
            var name = aloud.VoiceNames[i];
            var colon = name.IndexOf(':', StringComparison.Ordinal);
            var of = colon > 0 ? name[..colon] : string.Empty;
            if (i > 0 && !string.Equals(of, engine, StringComparison.Ordinal))
            {
                entries.Add(MenuEntry.Separator());
            }

            engine = of;
            entries.Add(MenuEntry.Radio(name, aloud.ChooseVoiceCommand, name, i == aloud.VoiceIndex));
        }

        return entries;
    }

    /// <summary>Lists the reading speeds, marking the one chosen.</summary>
    /// <param name="aloud">Reading aloud.</param>
    /// <returns>The speeds.</returns>
    internal static List<MenuEntry> Speeds(HyperPdf.ViewModels.Reading.ReadAloudViewModel aloud)
    {
        var entries = new List<MenuEntry>(aloud.SpeedNames.Length);
        for (var i = 0; i < aloud.SpeedNames.Length; i++)
        {
            entries.Add(MenuEntry.Radio(aloud.SpeedNames[i], aloud.ChooseSpeedCommand, aloud.SpeedNames[i], i == aloud.SpeedIndex));
        }

        return entries;
    }

    /// <summary>Lists the fit modes and zoom presets, marking the current choice.</summary>
    /// <param name="tab">The open document.</param>
    /// <returns>The zoom choices for the percentage button.</returns>
    internal static List<MenuEntry> Zoom(DocumentTabViewModel tab)
    {
        var entries = new List<MenuEntry>
        {
            new(MenuEntryKind.RadioItem, UiText.Get("Fit _Width"), "Ctrl+2", tab.FitWidthCommand, null, true, null, tab.ZoomMode == ZoomMode.FitWidth, []),
            new(MenuEntryKind.RadioItem, UiText.Get("Fit _Page"), "Ctrl+1", tab.FitPageCommand, null, true, null, tab.ZoomMode == ZoomMode.FitPage, []),
            MenuEntry.Separator(),
        };
        foreach (var level in ZoomLevels)
        {
            var percent = int.Parse(level, CultureInfo.InvariantCulture);
            var selected = tab.ZoomMode == ZoomMode.Free && Math.Abs(tab.Zoom - ((double)percent / ActualSize)) < ZoomTolerance;
            entries.Add(new(MenuEntryKind.RadioItem, $"{level}%", percent == ActualSize ? "Ctrl+0" : null, tab.SetZoomCommand, level, true, null, selected, []));
        }

        return entries;
    }

    /// <summary>Lists the measuring tool and what can be done with a measurement.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Measuring(DocumentTabViewModel tab)
    {
        var measure = tab.Measure;
        return
        [
            MenuEntry.Radio(UiText.Get("_Measure"), measure.ToggleCommand, null, measure.IsOn),
            Item("Keep the Measurement as a Mark", measure.KeepCommand),
            Item("Clear the Measurement", measure.ClearCommand),
        ];
    }

    /// <summary>Lists filling and signing: placing a signature or initials and adjusting the mark being placed.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Signing(DocumentTabViewModel tab)
    {
        var sign = tab.FillAndSign;
        return
        [
            MenuEntry.Radio(UiText.Get("_Fill and Sign"), sign.ToggleCommand, null, sign.IsActive),
            Item("Place My _Signature…", sign.SignatureCommand),
            Item("Place My _Initials…", sign.InitialsCommand),
            Item("Put the Mark Down", sign.PlaceCommand),
            Item("Adjust the Mark", sign.AdjustCommand),
            Item("Make the Mark Bigger", sign.BiggerCommand),
            Item("Make the Mark Smaller", sign.SmallerCommand),
            Item("Remove the Mark", sign.RemoveMarkCommand),
            Item("Stop Placing", sign.CancelPlacementCommand),
        ];
    }

    /// <summary>Lists the form fields that can be added and what can be changed about the picked field.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> FormFields(DocumentTabViewModel tab)
    {
        var design = tab.FormDesign;
        return
        [
            Item("Add a Form _Text Field", design.AddTextFieldCommand),
            Item("Add a Form Check _Box", design.AddCheckBoxCommand),
            Item("Add a Form Drop-_Down List", design.AddDropDownCommand),
            Item("Add a Form _List Box", design.AddListBoxCommand),
            Item("Add a Pair of _Radio Buttons", design.AddRadioButtonsCommand),
            Item("Add a Box to _Sign", design.AddSignatureFieldCommand),
            Item("Add a Form _Button", design.AddButtonCommand),
            MenuEntry.Separator(),
            Item("Make the Picked Field Required or Not", design.ToggleRequiredCommand),
            Item("Make the Picked Field Read-Only or Not", design.ToggleReadOnlyCommand),
            Item("Remove the Picked Form Field", design.RemoveFocusedCommand),
        ];
    }

    /// <summary>Lists what changes the page's own content: turning Edit mode on, adding shapes, text and pictures, and changing the picked objects.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> ContentEditing(DocumentTabViewModel tab)
    {
        var edit = tab.ContentEdit;
        return
        [
            MenuEntry.Radio(UiText.Get("_Edit the Page's Own Content"), edit.ToggleEditingCommand, null, edit.IsEditing),
            Item("Change the Picked Text…", edit.EditPickedTextCommand),
            Item("Remove the Picked Object", edit.RemovePickedCommand),
            Item("Add a _Rectangle", edit.AddRectangleCommand),
            Item("Add an _Ellipse", edit.AddEllipseCommand),
            Item("Add a _Line", edit.AddLineCommand),
            Item("Add Some _Text", edit.AddTextCommand),
            Item("Add a _Picture…", edit.AddPictureCommand),
            .. TextFont(edit),
            .. GroupWork(edit),
            Item("Make the Picked Object Larger", edit.GrowPickedCommand),
            Item("Make the Picked Object Smaller", edit.ShrinkPickedCommand),
            Item("Turn the Picked Object to the Right", edit.TurnRightCommand),
            Item("Turn the Picked Object to the Left", edit.TurnLeftCommand),
            Item("Flip the Picked Object Across", edit.FlipAcrossCommand),
            Item("Flip the Picked Object Upside Down", edit.FlipDownCommand),
            .. Palette("Fill the Picked Object with {0}", edit.SetPickedFillCommand),
            .. Palette("Colour the Picked Object's Line {0}", edit.SetPickedLineCommand),
            .. Looks(edit),
        ];
    }

    /// <summary>
    /// Lists the marking tools and settings the bar has no room for: the stamps, callouts, polygons, clouds, poly-lines, redaction and
    /// caret tools, the stamp words, and every setting of typed text. The entry of the tool in use, and of each setting in use, is ticked.
    /// </summary>
    /// <param name="marks">The marking tools of the tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> MoreMarks(AnnotationsViewModel marks)
    {
        List<MenuEntry> entries =
        [
            MarkTool(marks, UiText.Get("_Stamp"), AnnotationTool.Stamp),
            MarkTool(marks, UiText.Get("_Callout"), AnnotationTool.Callout),
            MarkTool(marks, UiText.Get("_Polygon"), AnnotationTool.Polygon),
            MarkTool(marks, UiText.Get("C_loud"), AnnotationTool.Cloud),
            MarkTool(marks, UiText.Get("Poly-_line"), AnnotationTool.PolyLine),
            MarkTool(marks, UiText.Get("Freehand H_ighlighter"), AnnotationTool.FreehandHighlight),
            MarkTool(marks, UiText.Get("Insert Te_xt Here"), AnnotationTool.Caret),
            MarkTool(marks, UiText.Get("Redact an _Area"), AnnotationTool.Redact),
            MarkTool(marks, UiText.Get("Redact Selected Te_xt"), AnnotationTool.RedactText),
            MenuEntry.Separator(),
        ];
        foreach (var label in StampLabels)
        {
            entries.Add(MenuEntry.Radio(UiText.Format("Stamp {0}", label), marks.SetStampCommand, label, marks.Tool == AnnotationTool.Stamp && marks.StampLabel == label));
        }

        entries.Add(Item("Stamp with My Own _Words…", marks.CustomStampCommand));
        entries.Add(Item("Stamp from a _Picture…", marks.PictureStampCommand));
        entries.Add(MenuEntry.Separator());
        entries.Add(Item("Text _Properties…", marks.EditTextPropertiesCommand));
        entries.Add(Item("_Finish Typing the Text", marks.FinishTextCommand));
        entries.Add(Item("_Edit the Picked Note…", marks.EditNoteCommand));
        entries.Add(Item("Filter and _Sort the Notes…", marks.ShowFilterCommand));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Radio(UiText.Get("Bol_d"), marks.ToggleBoldCommand, null, marks.IsBold));
        entries.Add(MenuEntry.Radio(UiText.Get("_Italic"), marks.ToggleItalicCommand, null, marks.IsItalic));
        entries.Add(MenuEntry.Radio(UiText.Get("_Underlined"), marks.ToggleUnderlineCommand, null, marks.IsUnderline));
        entries.Add(MenuEntry.Radio(UiText.Get("Align _Left"), marks.SetAlignmentCommand, MenuParameters.Of(TextBoxAlignment.Left), marks.TextAlignment == TextBoxAlignment.Left));
        entries.Add(MenuEntry.Radio(UiText.Get("Align _Centre"), marks.SetAlignmentCommand, MenuParameters.Of(TextBoxAlignment.Center), marks.TextAlignment == TextBoxAlignment.Center));
        entries.Add(MenuEntry.Radio(UiText.Get("Align _Right"), marks.SetAlignmentCommand, MenuParameters.Of(TextBoxAlignment.Right), marks.TextAlignment == TextBoxAlignment.Right));
        entries.Add(Item("Make the Typed Text Larger", marks.GrowTextCommand));
        entries.Add(Item("Make the Typed Text Smaller", marks.ShrinkTextCommand));
        entries.Add(MenuEntry.Separator());
        foreach (var option in AnnotationsViewModel.TextColors)
        {
            entries.Add(MenuEntry.Radio(UiText.Format("Text Colour: {0}", UiText.Get(option.NameKey)), marks.SetTextColorCommand, option.NameKey, marks.TextColor == option.Color));
        }

        foreach (var option in AnnotationsViewModel.LineSpacings)
        {
            entries.Add(Spacing("Line Spacing: {0}", option, marks.LineSpacing, marks.SetLineSpacingCommand));
        }

        foreach (var option in AnnotationsViewModel.CharacterSpacings)
        {
            entries.Add(Spacing("Letter Spacing: {0}", option, marks.CharacterSpacing, marks.SetCharacterSpacingCommand));
        }

        return entries;
    }

    /// <summary>Lists what can be done with signatures and with the document's accessibility tags.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Trust(DocumentTabViewModel tab)
    {
        var signatures = tab.Signatures;
        return
        [
            Item("_Check the Signatures", signatures.CheckCommand),
            Item("_Sign with a Certificate…", signatures.SignWithCertificateCommand),
            Copy(tab.Copies.ExternalSign, "Certificate"),
            Copy(tab.Copies.Validation, "Certificate"),
            MenuEntry.Separator(),
            Item("Check the _Accessibility Tags", tab.Tags.CheckCommand),
            Item("_Edit the Selected Tag…", tab.Tags.EditCommand),
            Item("Write the Tag Changes to the Document", tab.Tags.ApplyCommand),
        ];
    }

    /// <summary>Lists what the window can do that is not about one document.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Window(MainViewModel main) =>
    [
        MenuEntry.Item(UiText.Get("_New Window"), "Ctrl+N", main.NewWindowCommand, null, true),
        Item("Convert _Pictures to a PDF…", main.ConvertPicturesCommand),
        Item("Search a _Folder of Documents…", main.SearchFolderCommand),
        Item("Show the Document in Its F_older", main.ShowInFolderCommand),
        Item("Forget the Recent Documents", main.ClearRecentCommand),
    ];

    /// <summary>Makes the entry of one kind of copy, which opens its window.</summary>
    /// <typeparam name="T">The view model of the copy.</typeparam>
    /// <param name="action">The copy.</param>
    /// <param name="icon">The icon name.</param>
    /// <returns>The entry, greyed while the copy cannot be made.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Copy<T>(TabCopyAction<T> action, string icon)
        where T : class, ICopyWindowRequest =>
        MenuEntry.Item(action.MenuText, null, action.RunCommand, null, action.CanRun, icon);

    /// <summary>Makes the entry of a marking tool, ticked while it is the tool in use.</summary>
    /// <param name="marks">The marking tools of the tab shown.</param>
    /// <param name="header">The entry's text.</param>
    /// <param name="tool">The tool.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry MarkTool(AnnotationsViewModel marks, string header, AnnotationTool tool) =>
        MenuEntry.Radio(header, marks.SetToolCommand, MenuParameters.Of(tool), marks.Tool == tool);

    /// <summary>Makes the entry of one spacing choice, ticked while it is the spacing in use.</summary>
    /// <param name="format">The words of the entry, with the spacing's name in place of the placeholder.</param>
    /// <param name="option">The choice.</param>
    /// <param name="current">The spacing in use.</param>
    /// <param name="command">The command that takes the choice's name.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Spacing(string format, LineSpacingOption option, float current, ICommand command) =>
        MenuEntry.Radio(UiText.Format(format, UiText.Get(option.NameKey)), command, option.NameKey, Math.Abs(current - option.Value) < SpacingTolerance);

    /// <summary>Makes an entry that runs a command, greyed while the command cannot run.</summary>
    /// <param name="header">The entry's text in English, which is looked up in the language shown.</param>
    /// <param name="command">The command.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Item(string header, ICommand command) => MenuEntry.Item(UiText.Get(header), null, command, null, command.CanExecute(null));

    /// <summary>Makes an entry that runs a command and shows a named icon, greyed while the command cannot run.</summary>
    /// <param name="header">The entry's text in English, which is looked up in the language shown.</param>
    /// <param name="command">The command.</param>
    /// <param name="icon">The icon name.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Item(string header, ICommand command, string icon) => MenuEntry.Item(UiText.Get(header), null, command, null, command.CanExecute(null), icon);

    /// <summary>Lists what can be done with several picked objects: picking them all, lining them up and spreading them.</summary>
    /// <param name="edit">The view model of Edit mode.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> GroupWork(PageContentEditViewModel edit) =>
    [
        Item("Copy the Picked Object", edit.CopyPickedObjectCommand),
        Item("Cut the Picked Object", edit.CutPickedObjectCommand),
        Item("Paste the Copied Object", edit.PasteObjectCommand),
        Item("Duplicate the Picked Object", edit.DuplicateObjectCommand),
        Item("Bring the Picked Object to the Front", edit.BringToFrontCommand),
        Item("Bring the Picked Object Forward One Step", edit.BringForwardCommand),
        Item("Send the Picked Object Back One Step", edit.SendBackwardCommand),
        Item("Send the Picked Object to the Back", edit.SendToBackCommand),
        Item("Pick Every Object on the Page", edit.PickEveryObjectCommand),
        Item("Line Up the Picked Objects on the Left", edit.AlignLeftCommand),
        Item("Line Up the Picked Objects on Their Middles, Across", edit.AlignCentreCommand),
        Item("Line Up the Picked Objects on the Right", edit.AlignRightCommand),
        Item("Line Up the Picked Objects on the Top", edit.AlignTopCommand),
        Item("Line Up the Picked Objects on Their Middles, Down", edit.AlignMiddleCommand),
        Item("Line Up the Picked Objects on the Bottom", edit.AlignBottomCommand),
        Item("Spread the Picked Objects Evenly Across", edit.SpreadAcrossCommand),
        Item("Spread the Picked Objects Evenly Down", edit.SpreadDownCommand),
    ];

    /// <summary>Lists the choices of font for new and picked text and the sizes picked text can be set to.</summary>
    /// <param name="edit">The view model of Edit mode.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> TextFont(PageContentEditViewModel edit)
    {
        List<MenuEntry> entries =
        [
            MenuEntry.Radio(UiText.Get("Text in a Sans Serif Font"), edit.ChooseTextFamilyCommand, nameof(PageTextFamily.Sans), edit.NewTextFamily == PageTextFamily.Sans),
            MenuEntry.Radio(UiText.Get("Text in a Serif Font"), edit.ChooseTextFamilyCommand, nameof(PageTextFamily.Serif), edit.NewTextFamily == PageTextFamily.Serif),
            MenuEntry.Radio(UiText.Get("Text in a Fixed Width Font"), edit.ChooseTextFamilyCommand, nameof(PageTextFamily.Fixed), edit.NewTextFamily == PageTextFamily.Fixed),
            MenuEntry.Radio(UiText.Get("Bold Text"), edit.ToggleTextBoldCommand, null, edit.NewTextBold),
            MenuEntry.Radio(UiText.Get("Italic Text"), edit.ToggleTextItalicCommand, null, edit.NewTextItalic),
            Item("Change the Picked Paragraph…", edit.EditPickedParagraphCommand),
            Item("Set the Picked Text in This Font", edit.ApplyTextFontCommand),
        ];
        entries.AddRange(TextSizeEntries(edit.SetPickedTextSizeCommand));
        return entries;
    }

    /// <summary>Lists how solid the picked objects can be made, and the dashes, ends and corners their lines can have.</summary>
    /// <param name="edit">The view model of Edit mode.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> Looks(PageContentEditViewModel edit)
    {
        var entries = new List<MenuEntry>(Opacities.Length + BlendNames.Length + DashNames.Length + CapNames.Length + JoinNames.Length);
        AddNamed(entries, "Make the Picked Object {0}% Solid", Opacities, edit.SetPickedOpacityCommand);
        AddNamed(entries, "Mix the Picked Object's Colours with the Page as {0}", BlendNames, edit.SetPickedBlendCommand);
        foreach (var gradient in Gradients)
        {
            entries.Add(MenuEntry.Item(UiText.Get(gradient.Words), null, edit.SetPickedGradientCommand, gradient.How, ((ICommand)edit.SetPickedGradientCommand).CanExecute(gradient.How)));
        }

        AddNamed(entries, "Draw the Picked Object's Line {0}", DashNames, edit.SetPickedDashCommand);
        AddNamed(entries, "End the Picked Object's Line with a {0} End", CapNames, edit.SetPickedCapCommand);
        AddNamed(entries, "Give the Picked Object's Line {0} Corners", JoinNames, edit.SetPickedJoinCommand);
        entries.AddRange(Pictures(edit));
        return entries;
    }

    /// <summary>Lists what can be done with a picked picture: drawing another in its place, cropping it from each side and from every side, and saving it.</summary>
    /// <param name="edit">The view model of Edit mode.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> Pictures(PageContentEditViewModel edit)
    {
        var entries = new List<MenuEntry>
        {
            Item("Draw Another Picture in Place of the Picked Picture…", edit.ReplacePickedPictureCommand),
            Item("Save the Picked Picture as a File…", edit.SavePickedPictureCommand),
            Item("Change the Picked Picture in Another Program…", edit.EditPickedPictureOutsideCommand),
            Item("Make the Picked Object a Link…", edit.AddLinkToPickedCommand),
        };
        foreach (var side in CropSides)
        {
            foreach (var percent in CropPercents)
            {
                var how = $"{side.Key}:{percent}";
                entries.Add(MenuEntry.Item(UiText.Format(side.Words, percent), null, edit.CropPickedPictureCommand, how, ((ICommand)edit.CropPickedPictureCommand).CanExecute(how)));
            }
        }

        return entries;
    }

    /// <summary>Adds one entry for each choice of a setting, each running the command with the choice's name.</summary>
    /// <param name="entries">Receives the entries.</param>
    /// <param name="format">The words of an entry, with the choice's words in place of the placeholder.</param>
    /// <param name="choices">The names of the choices, each of which is also the words shown for it.</param>
    /// <param name="command">The command that takes the name.</param>
    private static void AddNamed(List<MenuEntry> entries, string format, string[] choices, ICommand command)
    {
        foreach (var choice in choices)
        {
            entries.Add(MenuEntry.Item(UiText.Format(format, UiText.Get(choice)), null, command, choice, command.CanExecute(choice)));
        }
    }

    /// <summary>Lists one entry for each text size offered, each running the command with the size in points.</summary>
    /// <param name="command">The command that takes the size.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> TextSizeEntries(ICommand command)
    {
        var entries = new List<MenuEntry>(TextSizes.Length);
        foreach (var size in TextSizes)
        {
            entries.Add(MenuEntry.Item(UiText.Format("Set the Picked Text to {0} Points", size), null, command, size, command.CanExecute(size)));
        }

        return entries;
    }

    /// <summary>Lists one entry for each colour of the palette, each running the command with the colour's six hexadecimal digits.</summary>
    /// <param name="format">The words of an entry, with the colour's name in place of the placeholder.</param>
    /// <param name="command">The command that takes the colour.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> Palette(string format, ICommand command)
    {
        var entries = new List<MenuEntry>(PaletteColours.Length);
        foreach (var colour in PaletteColours)
        {
            entries.Add(MenuEntry.Item(UiText.Format(format, UiText.Get(colour.Name)), null, command, colour.Rgb, command.CanExecute(colour.Rgb)));
        }

        return entries;
    }

    /// <summary>A side a picture can be cropped from.</summary>
    /// <param name="Key">The word the crop command is given for the side.</param>
    /// <param name="Words">The words of the menu entry, with the percent in place of the placeholder.</param>
    private readonly record struct CropSide(string Key, string Words);

    /// <summary>One gradient menu entry.</summary>
    /// <param name="Words">The words of the entry, in English.</param>
    /// <param name="How">The shape and colours, as the command reads them.</param>
    private readonly record struct GradientChoice(string Words, string How);

    /// <summary>A colour of the palette.</summary>
    /// <param name="Name">The colour's name in the words of the menus.</param>
    /// <param name="Rgb">The colour as six hexadecimal digits.</param>
    private readonly record struct PaletteColour(string Name, string Rgb);
}

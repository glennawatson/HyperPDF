// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Help;

/// <summary>
/// What each control does, in short plain English. Each text is both the tooltip and the screen reader help text
/// (see <c>ControlHelp</c> in the render head). A shortcut, when there is one, comes after the explanation and matches
/// the app's shortcut table.
/// </summary>
public static class Descriptions
{
    /// <summary>A shared copy action resource key.</summary>
    private const string CopyProgressKey = "Moves while the copy is being made.";

    /// <summary>A shared copy action resource key.</summary>
    private const string CopyCancelKey = "Stop making the copy. No file is saved.";

    /// <summary>A shared copy action resource key.</summary>
    private const string CopySaveKey = "Choose where to save the copy, then make it. The open file is never replaced.";

    /// <summary>Gets the Back button.</summary>
    public static string Back => UiText.Get(
        "Go back to where you were before a jump, like Back in a web browser. Jumps come from links, the outline, search results and page "
            + "numbers. Shortcut: Alt+Left");

    /// <summary>Gets the Forward button.</summary>
    public static string Forward => UiText.Get("Go forward again after going Back, like Forward in a web browser. Shortcut: Alt+Right");

    /// <summary>Gets the Sidebar toggle.</summary>
    public static string Sidebar => UiText.Get("Show or hide the sidebar with page pictures, the outline and search results. Shortcut: F9");

    /// <summary>Gets the previous page button.</summary>
    public static string PreviousPage => UiText.Get("Go to the previous page. Shortcut: Page Up");

    /// <summary>Gets the next page button.</summary>
    public static string NextPage => UiText.Get("Go to the next page. Shortcut: Page Down");

    /// <summary>Gets the page number box.</summary>
    public static string PageNumber => UiText.Get("Type a page number, then press Enter to go to that page. Shortcut: Ctrl+L");

    /// <summary>Gets the zoom out button.</summary>
    public static string ZoomOut => UiText.Get("Make the pages smaller. Shortcut: Ctrl+Minus");

    /// <summary>Gets the zoom in button.</summary>
    public static string ZoomIn => UiText.Get("Make the pages bigger. Shortcut: Ctrl+Plus");

    /// <summary>Gets the zoom menu button, which shows the current zoom.</summary>
    public static string ZoomMenu => UiText.Get("Shows the current zoom. Choose to fit the width, fit the whole page or pick a set size.");

    /// <summary>Gets the fit width zoom.</summary>
    public static string FitWidth => UiText.Get("Make the page fill the width of the window. Shortcut: Ctrl+2");

    /// <summary>Gets the fit page zoom.</summary>
    public static string FitPage => UiText.Get("Make the whole page fit in the window. Shortcut: Ctrl+1");

    /// <summary>Gets the 50% zoom.</summary>
    public static string Zoom50 => UiText.Get("Show the pages at half their real size.");

    /// <summary>Gets the 75% zoom.</summary>
    public static string Zoom75 => UiText.Get("Show the pages at three quarters of their real size.");

    /// <summary>Gets the 100% zoom.</summary>
    public static string Zoom100 => UiText.Get("Show the pages at their real size. Shortcut: Ctrl+0");

    /// <summary>Gets the 125% zoom.</summary>
    public static string Zoom125 => UiText.Get("Show the pages a quarter bigger than their real size.");

    /// <summary>Gets the 150% zoom.</summary>
    public static string Zoom150 => UiText.Get("Show the pages one and a half times their real size.");

    /// <summary>Gets the 200% zoom.</summary>
    public static string Zoom200 => UiText.Get("Show the pages twice their real size.");

    /// <summary>Gets the 400% zoom.</summary>
    public static string Zoom400 => UiText.Get("Show the pages four times their real size.");

    /// <summary>Gets the Save button and menu item.</summary>
    public static string Save => UiText.Get("Save your annotations and form entries to the file. Shortcut: Ctrl+S");

    /// <summary>Gets the Print button.</summary>
    public static string Print => UiText.Get("Choose the pages and settings, then print. Shortcut: Ctrl+P");

    /// <summary>Gets the Annotate toggle.</summary>
    public static string Annotate => UiText.Get("Show the tools to highlight, underline, draw, and add notes and text.");

    /// <summary>Gets the Fill and Sign toggle.</summary>
    public static string FillSign => UiText.Get("Show the tools to fill in forms and sign the document.");

    /// <summary>Gets the Find toggle.</summary>
    public static string Find => UiText.Get("Search for words in this document. Shortcut: Ctrl+F");

    /// <summary>Gets the Focus toggle.</summary>
    public static string Focus => UiText.Get("Show only the text, in one calm column, without the page layout. Shortcut: Ctrl+4");

    /// <summary>Gets the Read Aloud toggle.</summary>
    public static string ReadAloud => UiText.Get("Read the document aloud, starting from this page. Shortcut: Ctrl+Shift+Y");

    /// <summary>Gets the View menu button.</summary>
    public static string ViewMenu => UiText.Get("Open the View menu to turn pages, change the page layout, choose how dragging works, read or present.");

    /// <summary>Gets the rotate left menu item.</summary>
    public static string RotateLeft => UiText.Get("Turn the pages a quarter turn to the left. Shortcut: Ctrl+Left");

    /// <summary>Gets the rotate right menu item.</summary>
    public static string RotateRight => UiText.Get("Turn the pages a quarter turn to the right. Shortcut: Ctrl+Right");

    /// <summary>Gets the single page layout menu item.</summary>
    public static string SinglePage => UiText.Get("Show one page in each row. Scroll down for the next page.");

    /// <summary>Gets the dual page layout menu item.</summary>
    public static string DualPage => UiText.Get("Show two pages side by side: pages 1 and 2, then 3 and 4. Next page moves to the next pair.");

    /// <summary>Gets the dual page with cover layout menu item.</summary>
    public static string DualPageCover => UiText.Get("Show the first page alone like a book cover, then pages side by side: 2 and 3, then 4 and 5.");

    /// <summary>Gets the vertical scroll menu item.</summary>
    public static string VerticalScroll => UiText.Get("Pages follow one another down the window and you scroll down. This is the usual way to read.");

    /// <summary>Gets the horizontal scroll menu item.</summary>
    public static string HorizontalScroll => UiText.Get("All pages sit in one row and you scroll from side to side. The mouse wheel moves sideways.");

    /// <summary>Gets the wrapped scroll menu item.</summary>
    public static string WrappedScroll => UiText.Get("Pages fill each row across the window, then continue on the next row, so a wide window shows several pages.");

    /// <summary>Gets the Read Mode menu item.</summary>
    public static string ReadMode => UiText.Get("Put the tool bars and sidebar away so the pages fill the window. Shortcut: Ctrl+H");

    /// <summary>Gets the Split View menu item.</summary>
    public static string SplitView => UiText.Get(
        "Show a second view of this document beside the first, to read or compare two places at once. Choose it again to go back to one "
            + "view.");

    /// <summary>Gets the divider between the two views of a split.</summary>
    public static string SplitSplitter => UiText.Get("Drag to share the width between the two views.");

    /// <summary>Gets the Present menu item.</summary>
    public static string Present => UiText.Get("Show one page at a time on the full screen. Press Esc to stop. Shortcut: Shift+F5");

    /// <summary>Gets the caret navigation menu item.</summary>
    public static string CaretNavigation => UiText.Get("Move through the text with the arrow keys. Hold Shift to select text. Shortcut: F7");

    /// <summary>Gets the page by page menu item.</summary>
    public static string PageByPage => UiText.Get("Show one page, or one pair of pages, at a time instead of scrolling through all the pages.");

    /// <summary>Gets the Select Text tool menu item.</summary>
    public static string SelectTextTool => UiText.Get("Drag over the pages to select text. Clicks follow links.");

    /// <summary>Gets the Hand tool menu item.</summary>
    public static string HandTool => UiText.Get("Drag the pages to move them, like moving paper with your hand. Hold Space or the middle mouse button to do this with any tool.");

    /// <summary>Gets the Zoom to Area tool menu item.</summary>
    public static string ZoomAreaTool => UiText.Get("Drag a box around part of a page to zoom in until it fills the view. Press Esc to stop.");

    /// <summary>Gets the Auto-scroll menu item.</summary>
    public static string AutoScroll => UiText.Get("Scroll down by itself at a calm speed. Up and Down change the speed. Press Esc or click to stop. Shortcut: Ctrl+Shift+H");

    /// <summary>Gets the First Page menu item.</summary>
    public static string FirstPage => UiText.Get("Go to the first page. Home also works when the pages have the keyboard. Shortcut: Ctrl+Home");

    /// <summary>Gets the Last Page menu item.</summary>
    public static string LastPage => UiText.Get("Go to the last page. End also works when the pages have the keyboard. Shortcut: Ctrl+End");

    /// <summary>Gets the auto-scroll bar's Slower button.</summary>
    public static string AutoScrollSlower => UiText.Get("Make auto-scroll slower. The Down arrow does the same.");

    /// <summary>Gets the auto-scroll bar's Faster button.</summary>
    public static string AutoScrollFaster => UiText.Get("Make auto-scroll faster. The Up arrow does the same.");

    /// <summary>Gets the auto-scroll bar's Stop button.</summary>
    public static string AutoScrollStop => UiText.Get("Stop scrolling by itself. Esc or a click on the pages does the same.");

    /// <summary>Gets the Document menu button.</summary>
    public static string DocumentMenu => UiText.Get("Open the Document menu to save a copy, reload, find the file or see its details.");

    /// <summary>Gets the Select All menu item.</summary>
    public static string SelectAll => UiText.Get("Select all the text on the current page, ready to copy. Shortcut: Ctrl+A");

    /// <summary>Gets the Save As menu item.</summary>
    public static string SaveAs => UiText.Get("Save a copy of the document with a new name or in a new place. Shortcut: Ctrl+Shift+S");

    /// <summary>Gets the Reload menu item.</summary>
    public static string Reload => UiText.Get("Load the file again from the disk. Shortcut: F5");

    /// <summary>Gets the Show in Folder menu item.</summary>
    public static string ShowInFolder => UiText.Get("Open the folder that holds this file.");

    /// <summary>Gets the Properties menu item.</summary>
    public static string Properties => UiText.Get("See details about this document, such as its title, author and size. Shortcut: Alt+Enter");

    /// <summary>Gets the Tools menu button.</summary>
    public static string ToolsMenu => UiText.Get("Open the Tools menu to measure the page, copy an image of part of it or recognise scanned text.");

    /// <summary>Gets the Snapshot tool menu item.</summary>
    public static string SnapshotTool => UiText.Get("Drag a box around part of a page to copy a sharp image of it. Press Esc to stop.");

    /// <summary>Gets the Measure menu item.</summary>
    public static string Measure => UiText.Get("Measure distances, lengths and areas on the page. Shortcut: Ctrl+Shift+M");

    /// <summary>Gets the Recognise Text menu item.</summary>
    public static string RecognizeText => UiText.Get("Find the words in scanned pages so you can search, select and hear them.");

    /// <summary>Gets the language bar's Not Now button.</summary>
    public static string CloseLanguageBar => UiText.Get("Hide this bar. Nothing on the pages has been changed, and any download in progress stops.");

    /// <summary>Gets the language bar's recognise button.</summary>
    public static string RecognizeInLanguage => UiText.Get("Find the words in this document in the chosen language. A language not on this computer downloads first and stays for next time.");

    /// <summary>Gets the language bar's language list.</summary>
    public static string DocumentLanguage => UiText.Get("Choose the language the scanned pages are written in.");

    /// <summary>Gets the Preferences Stop Download button.</summary>
    public static string StopLanguageDownload => UiText.Get("Stop the language download. Languages that already finished are kept.");

    /// <summary>Gets a language's tick box in Preferences.</summary>
    public static string UseLanguage => UiText.Get("Tick to find words in this language. Tick more than one for documents that mix languages.");

    /// <summary>Gets a language's Download button in Preferences.</summary>
    public static string DownloadLanguage => UiText.Get("Download this language and keep it on this computer, so words in it can be found.");

    /// <summary>Gets a language's Remove button in Preferences.</summary>
    public static string RemoveLanguage => UiText.Get("Delete this language from this computer to free space. You can download it again later.");

    /// <summary>Gets the Recognise Text in Another Language menu item.</summary>
    public static string RecognizeTextInLanguage => UiText.Get("Choose the language a scanned document is written in, then find its words. Other languages download in one click.");

    /// <summary>Gets the Leave Read Mode button.</summary>
    public static string LeaveReadMode => UiText.Get("Bring back the tool bars and sidebar. Shortcut: Esc or Ctrl+H");

    /// <summary>Gets the button that keeps a measurement.</summary>
    public static string MeasureKeep => UiText.Get("Keep the finished measurement on the page as an annotation.");

    /// <summary>Gets the button that clears a measurement.</summary>
    public static string MeasureClear => UiText.Get("Clear the measurement you are making. Shortcut: Esc");

    /// <summary>Gets the button that closes the measuring tools.</summary>
    public static string MeasureDone => UiText.Get("Put the measuring tools away.");

    /// <summary>Gets the distance tool.</summary>
    public static string Distance => UiText.Get("Click two points on the page to measure the distance between them.");

    /// <summary>Gets the perimeter tool.</summary>
    public static string Perimeter => UiText.Get("Click points along a path to measure its length. Double-click to finish.");

    /// <summary>Gets the area tool.</summary>
    public static string Area => UiText.Get("Click the corners of a shape to measure its area. Double-click to finish.");

    /// <summary>Gets the measuring scale box.</summary>
    public static string MeasureScale => UiText.Get("Type how the page relates to real size, such as 1 cm = 2 m or 1:100.");

    /// <summary>Gets the annotation Undo button.</summary>
    public static string Undo => UiText.Get("Undo your last annotation change: adding, deleting, moving, colour, style, note or reply. Shortcut: Ctrl+Z");

    /// <summary>Gets the annotation Redo button.</summary>
    public static string Redo => UiText.Get("Make the annotation change you undid again. Shortcut: Ctrl+Y or Ctrl+Shift+Z");

    /// <summary>Gets the button that closes the annotation tools.</summary>
    public static string AnnotateDone => UiText.Get("Put the annotation tools away.");

    /// <summary>Gets the select tool.</summary>
    public static string SelectTool => UiText.Get(
        "Select text, or click an annotation to pick it. Drag a picked annotation or its corner squares to move or resize it; arrow keys "
            + "nudge it.");

    /// <summary>Gets the highlight tool.</summary>
    public static string HighlightTool => UiText.Get("Select text to highlight it.");

    /// <summary>Gets the underline tool.</summary>
    public static string UnderlineTool => UiText.Get("Select text to underline it.");

    /// <summary>Gets the strike out tool.</summary>
    public static string StrikeTool => UiText.Get("Select text to draw a line through it.");

    /// <summary>Gets the draw tool.</summary>
    public static string DrawTool => UiText.Get("Drag on the page to draw freehand.");

    /// <summary>Gets the freehand highlighter tool.</summary>
    public static string FreehandHighlightTool => UiText.Get("Drag on the page with a wide, see-through pen to highlight anything, including pictures and scanned text.");

    /// <summary>Gets the caret tool.</summary>
    public static string CaretTool => UiText.Get("Click on the page to mark a place where text should be inserted.");

    /// <summary>Gets the note tool.</summary>
    public static string NoteTool => UiText.Get("Click on the page to add a sticky note.");

    /// <summary>Gets the text tool.</summary>
    public static string TextTool => UiText.Get("Click on the page and type. Click or double-click text to change it.");

    /// <summary>Gets the shape button.</summary>
    public static string Shape => UiText.Get("Choose a shape, then drag on the page to draw it.");

    /// <summary>Gets the rectangle shape.</summary>
    public static string Rectangle => UiText.Get("Draw rectangles. Drag on the page to draw one.");

    /// <summary>Gets the ellipse shape.</summary>
    public static string Ellipse => UiText.Get("Draw ovals and circles. Drag on the page to draw one.");

    /// <summary>Gets the arrow shape.</summary>
    public static string Arrow => UiText.Get("Draw arrows. Drag on the page from the tail to the point.");

    /// <summary>Gets the line shape.</summary>
    public static string Line => UiText.Get("Draw straight lines. Drag on the page to draw one.");

    /// <summary>Gets the callout tool.</summary>
    public static string Callout => UiText.Get("Write text with an arrow pointing at something. Drag from the thing to where the text goes.");

    /// <summary>Gets the polygon tool.</summary>
    public static string Polygon => UiText.Get("Draw a shape with straight sides. Click each corner, then press Enter or double-click.");

    /// <summary>Gets the cloud tool.</summary>
    public static string Cloud => UiText.Get("Draw a cloud around an area. Click each corner, then press Enter or double-click.");

    /// <summary>Gets the connected lines tool.</summary>
    public static string PolyLine => UiText.Get("Draw joined straight lines. Click each point, then press Enter or double-click.");

    /// <summary>Gets the stamp button.</summary>
    public static string Stamp => UiText.Get("Choose a stamp, then click on the page to place it.");

    /// <summary>Gets the Approved stamp.</summary>
    public static string StampApproved => UiText.Get("Use the Approved stamp. Click on the page to place it.");

    /// <summary>Gets the Reviewed stamp.</summary>
    public static string StampReviewed => UiText.Get("Use the Reviewed stamp. Click on the page to place it.");

    /// <summary>Gets the Draft stamp.</summary>
    public static string StampDraft => UiText.Get("Use the Draft stamp. Click on the page to place it.");

    /// <summary>Gets the Confidential stamp.</summary>
    public static string StampConfidential => UiText.Get("Use the Confidential stamp. Click on the page to place it.");

    /// <summary>Gets the Final stamp.</summary>
    public static string StampFinal => UiText.Get("Use the Final stamp. Click on the page to place it.");

    /// <summary>Gets the not approved stamp.</summary>
    public static string StampNotApproved => UiText.Get("Use the Not Approved stamp. Click on the page to place it.");

    /// <summary>Gets the custom stamp item.</summary>
    public static string StampCustom => UiText.Get("Type your own words for a stamp, then click on the page to place it.");

    /// <summary>Gets the picture stamp item.</summary>
    public static string StampPicture => UiText.Get("Choose a picture file to use as a stamp, then click on the page to place it.");

    /// <summary>Gets the line width button.</summary>
    public static string LineWidth => UiText.Get("Choose how thick new drawings, lines and shapes are, and the one you picked.");

    /// <summary>Gets the thin line width.</summary>
    public static string LineThin => UiText.Get("Use thin lines for new drawings and shapes and the one you picked.");

    /// <summary>Gets the medium line width.</summary>
    public static string LineMedium => UiText.Get("Use medium lines for new drawings and shapes and the one you picked.");

    /// <summary>Gets the thick line width.</summary>
    public static string LineThick => UiText.Get("Use thick lines for new drawings and shapes and the one you picked.");

    /// <summary>Gets the text size button.</summary>
    public static string AnnotationTextSize => UiText.Get("Choose the size of new text and callouts, and of the text you picked.");

    /// <summary>Gets the small text size.</summary>
    public static string TextSmall => UiText.Get("Use small text for new text and callouts and the one you picked.");

    /// <summary>Gets the medium text size.</summary>
    public static string TextMedium => UiText.Get("Use medium text for new text and callouts and the one you picked.");

    /// <summary>Gets the large text size.</summary>
    public static string TextLarge => UiText.Get("Use large text for new text and callouts and the one you picked.");

    /// <summary>Gets the extra large text size.</summary>
    public static string TextExtraLarge => UiText.Get("Use extra large text for new text and callouts and the one you picked.");

    /// <summary>Gets the annotation colour button.</summary>
    public static string AnnotationColour => UiText.Get("Choose the colour for new annotations, drawings and text, and for the annotation you picked.");

    /// <summary>Gets the yellow annotation colour.</summary>
    public static string Yellow => UiText.Get("Use yellow for new annotations and the one you picked.");

    /// <summary>Gets the green annotation colour.</summary>
    public static string Green => UiText.Get("Use green for new annotations and the one you picked.");

    /// <summary>Gets the blue annotation colour.</summary>
    public static string Blue => UiText.Get("Use blue for new annotations and the one you picked.");

    /// <summary>Gets the red annotation colour.</summary>
    public static string Red => UiText.Get("Use red for new annotations and the one you picked.");

    /// <summary>Gets the purple annotation colour.</summary>
    public static string Purple => UiText.Get("Use purple for new annotations and the one you picked.");

    /// <summary>Gets the orange annotation colour.</summary>
    public static string Orange => UiText.Get("Use orange for new annotations and the one you picked.");

    /// <summary>Gets the grey annotation colour.</summary>
    public static string Grey => UiText.Get("Use grey for new annotations and the one you picked.");

    /// <summary>Gets the dark blue annotation colour.</summary>
    public static string DarkBlue => UiText.Get("Use dark blue for new annotations and the one you picked.");

    /// <summary>Gets the comment list's text filter.</summary>
    public static string AnnotationFilterText => UiText.Get("Type words to show only the comments that contain them.");

    /// <summary>Gets the button that shows the comment list's filters and sort order.</summary>
    public static string AnnotationFilters => UiText.Get("Show or hide the choices that filter and sort the comment list.");

    /// <summary>Gets the comment list's type filter.</summary>
    public static string AnnotationTypeFilter => UiText.Get("Show only one type of comment, such as notes or drawings.");

    /// <summary>Gets the comment list's colour filter.</summary>
    public static string AnnotationColourFilter => UiText.Get("Show only the comments of one colour.");

    /// <summary>Gets the comment list's author filter.</summary>
    public static string AnnotationAuthorFilter => UiText.Get("Show only the comments one person wrote.");

    /// <summary>Gets the comment list's sort order.</summary>
    public static string AnnotationSort => UiText.Get("Choose the order of the comment list: by page, newest first, by author or by type.");

    /// <summary>Gets the button that clears the comment list's filters.</summary>
    public static string ClearAnnotationFilter => UiText.Get("Show every comment again.");

    /// <summary>Gets the button that closes the Fill and Sign tools.</summary>
    public static string FillSignDone => UiText.Get("Put the Fill & Sign tools away.");

    /// <summary>Gets the Sign with Certificate button.</summary>
    public static string CertificateSign => UiText.Get("Sign a copy of the document with your digital certificate, a .p12 or .pfx file.");

    /// <summary>Gets the Check Signatures button.</summary>
    public static string CheckSignatures => UiText.Get("Check who signed this document and whether it changed after signing. This may go online.");

    /// <summary>Gets the list of colourants on a colour separation page.</summary>
    public static string ShowSeparation => UiText.Get("Show another colour separation of the same printed page. Each separation is a page of its own in this file.");

    /// <summary>Gets the button that stops text recognition.</summary>
    public static string StopRecognition => UiText.Get("Stop finding the words in scanned pages.");

    /// <summary>Gets the button that closes Read Aloud.</summary>
    public static string CloseReadAloud => UiText.Get("Stop reading and close the Read Aloud bar. Shortcut: Ctrl+Shift+E");

    /// <summary>Gets the previous sentence button.</summary>
    public static string PreviousSentence => UiText.Get("Go back one sentence and read it again. Shortcut: Ctrl+Shift+Comma");

    /// <summary>Gets the Play button.</summary>
    public static string Play => UiText.Get("Carry on reading from where it stopped. Shortcut: Ctrl+Shift+C");

    /// <summary>Gets the Pause button.</summary>
    public static string Pause => UiText.Get("Pause reading. Your place is kept. Shortcut: Ctrl+Shift+C");

    /// <summary>Gets the next sentence button.</summary>
    public static string NextSentence => UiText.Get("Skip to the next sentence. Shortcut: Ctrl+Shift+Period");

    /// <summary>Gets the voice list.</summary>
    public static string Voice => UiText.Get("Choose the voice that reads aloud.");

    /// <summary>Gets the speed list.</summary>
    public static string Speed => UiText.Get("Choose how fast the voice reads.");

    /// <summary>Gets the Read Aloud options button.</summary>
    public static string ReadAloudOptions => UiText.Get("Choose how the text being read is marked on the page.");

    /// <summary>Gets the check box that marks each word as it is read.</summary>
    public static string WordMark => UiText.Get("Mark each word as it is read aloud.");

    /// <summary>Gets the check box that dims text away from what is read.</summary>
    public static string FocusBand => UiText.Get("Dim the text away from the part being read aloud.");

    /// <summary>Gets the Download Voice button.</summary>
    public static string DownloadVoice => UiText.Get("Download this voice so it can read aloud on this computer.");

    /// <summary>Gets a button that hides a message.</summary>
    public static string DismissMessage => UiText.Get("Hide this message.");

    /// <summary>Gets the Dismiss button on the message about a document whose integrity protection failed or could not be checked.</summary>
    public static string DismissIntegrityNotice => UiText.Get("Hide this message about the document's integrity protection. It comes back next time the document opens.");

    /// <summary>Gets the Dismiss button on the message about content that cannot be shown.</summary>
    public static string DismissContentWarning => UiText.Get("Hide this message about parts of the document HyperPDF cannot show. It comes back next time the document opens.");

    /// <summary>Gets the Dismiss button on the message about a repaired file.</summary>
    public static string DismissRepairNotice => UiText.Get("Hide this message about the repaired file. It comes back next time the document opens.");

    /// <summary>Gets the Dismiss button on the message about features the file needs.</summary>
    public static string DismissRequirementNotice => UiText.Get("Hide this message about features the file needs. It comes back next time the document opens.");

    /// <summary>Gets the Details button on the message about features the file needs.</summary>
    public static string ToggleRequirementDetails => UiText.Get("Show or hide which features the file needs that HyperPDF does not fully provide.");

    /// <summary>Gets the Details button on the message about a repaired file.</summary>
    public static string ToggleRepairDetails => UiText.Get("Show or hide what was wrong with the file and how it was repaired.");

    /// <summary>Gets the Reload button on the changed file bar.</summary>
    public static string ReloadChanged => UiText.Get("Load the changed file from the disk.");

    /// <summary>Gets the Dismiss button on the changed file bar.</summary>
    public static string DismissReload => UiText.Get("Keep what is on screen now and hide this message.");

    /// <summary>Gets the find box.</summary>
    public static string SearchBox => UiText.Get("Type the words to find. Press Enter for the next result or Shift+Enter for the one before.");

    /// <summary>Gets the match case option.</summary>
    public static string MatchCase => UiText.Get("Only find text with the same capital and small letters.");

    /// <summary>Gets the whole words option.</summary>
    public static string WholeWords => UiText.Get("Only find whole words, not parts of longer words.");

    /// <summary>Gets the ignore accents option.</summary>
    public static string IgnoreAccents => UiText.Get("Find letters with or without accents, so resume also finds résumé.");

    /// <summary>Gets the similar characters option.</summary>
    public static string SimilarCharacters => UiText.Get("Treat look-alike characters as the same: curly and straight quotes, dashes, and full-width letters.");

    /// <summary>Gets the previous result button.</summary>
    public static string PreviousResult => UiText.Get("Go to the previous result. Shortcut: Shift+F3");

    /// <summary>Gets the next result button.</summary>
    public static string NextResult => UiText.Get("Go to the next result. Shortcut: F3");

    /// <summary>Gets the button that closes the find bar.</summary>
    public static string CloseFind => UiText.Get("Close the find bar. Shortcut: Esc");

    /// <summary>Gets the thumbnails sidebar tab.</summary>
    public static string Thumbnails => UiText.Get("Show a small picture of each page. Choose one to go to that page.");

    /// <summary>Gets the outline sidebar tab.</summary>
    public static string Outline => UiText.Get("Show the document's headings. Choose one to go to it.");

    /// <summary>Gets the search results sidebar tab.</summary>
    public static string SearchResults => UiText.Get("Show every place your search found. Choose one to go to it.");

    /// <summary>Gets the annotations sidebar tab.</summary>
    public static string Annotations => UiText.Get("Show the highlights, notes and other annotations in this document.");

    /// <summary>Gets the media sidebar tab.</summary>
    public static string Media => UiText.Get("Show the sound and video in this document. They never play by themselves.");

    /// <summary>Gets the Save Media button.</summary>
    public static string SaveMedia => UiText.Get("Save the chosen sound or video as a file.");

    /// <summary>Gets the Open Media button.</summary>
    public static string OpenMedia => UiText.Get("Open the chosen sound or video with your system's player, after you agree. Nothing plays until you choose it.");

    /// <summary>Gets the Play Media button.</summary>
    public static string PlayMedia => UiText.Get("Play the chosen sound or video here. It starts only when you press this.");

    /// <summary>Gets the Pause Media button.</summary>
    public static string PauseMedia => UiText.Get("Pause the sound or video where it is.");

    /// <summary>Gets the Stop Media button.</summary>
    public static string StopMedia => UiText.Get("Stop the sound or video and go back to the start.");

    /// <summary>Gets the Media Position bar.</summary>
    public static string MediaPosition => UiText.Get(
        "Drag to move to another place in the sound or video. With the keyboard, the arrow keys move five seconds and Page Up or Page Down "
            + "move thirty.");

    /// <summary>Gets the Skip Media Back button.</summary>
    public static string SkipMediaBack => UiText.Get("Go back ten seconds.");

    /// <summary>Gets the Skip Media Forward button.</summary>
    public static string SkipMediaForward => UiText.Get("Go forward ten seconds.");

    /// <summary>Gets the attachments sidebar tab.</summary>
    public static string Attachments => UiText.Get("Show the files attached to this document.");

    /// <summary>Gets the layers sidebar tab.</summary>
    public static string Layers => UiText.Get("Show the document's layers so you can hide or show them.");

    /// <summary>Gets the tags sidebar tab.</summary>
    public static string Tags => UiText.Get("Show the tags that tell a screen reader how this document reads, and fix them.");

    /// <summary>Gets the tag type box.</summary>
    public static string TagType => UiText.Get("What the chosen tag is, such as a heading or a paragraph. Only types the file may use are listed.");

    /// <summary>Gets the tag description box.</summary>
    public static string TagDescription => UiText.Get("What a screen reader says in place of the content, for example what a picture shows.");

    /// <summary>Gets the tag replacement text box.</summary>
    public static string TagReplacement => UiText.Get("Text that stands in for the content when it is read aloud or copied. Leave it empty when the page text reads well.");

    /// <summary>Gets the tag language box.</summary>
    public static string TagLanguage => UiText.Get(
        "The language of this tag's content, such as en-AU, so a screen reader uses the right voice. Leave it empty to use the document's "
            + "language.");

    /// <summary>Gets the tag title box.</summary>
    public static string TagTitle => UiText.Get("A short title for the chosen tag.");

    /// <summary>Gets the move-inside choice and button.</summary>
    public static string MoveTagInto => UiText.Get(
        "Choose another tag, then move the chosen tag inside it, as its last item. A move that would break the document's structure is "
            + "refused.");

    /// <summary>Gets the new custom tag type fields.</summary>
    public static string NewRole => UiText.Get("Name a custom tag type this document uses and choose the standard tag it stands for, so screen readers know what it means.");

    /// <summary>Gets the remove custom tag type button.</summary>
    public static string RemoveRole => UiText.Get("Take away the chosen custom tag type's meaning. It is refused while any tag still uses the type.");

    /// <summary>Gets the cell span boxes.</summary>
    public static string TagSpan => UiText.Get("How many rows or columns this table cell stretches across. Leave it at 1 for a normal cell.");

    /// <summary>Gets the list numbering box.</summary>
    public static string TagNumbering => UiText.Get("How this list labels its items, such as bullets or numbers. Screen readers use it to say what kind of list it is.");

    /// <summary>Gets the tag placement box.</summary>
    public static string TagPlacement => UiText.Get("Whether this tag is its own block (Block), runs inside a line (Inline), or sits before, at the start or at the end of its text.");

    /// <summary>Gets the header cell scope box.</summary>
    public static string TagScope => UiText.Get(
        "Which cells this table header labels: the cells in its row, in its column, or both. Screen readers use it to say which header goes "
            + "with a cell.");

    /// <summary>Gets the Check Accessibility button.</summary>
    public static string CheckTags => UiText.Get("Look for things a screen reader would struggle with, such as a picture with no description.");

    /// <summary>Gets the Apply Tag Changes button.</summary>
    public static string ApplyTag => UiText.Get("Save the type and text above to the chosen tag. Undo takes the change back.");

    /// <summary>Gets the Move Tag Up button.</summary>
    public static string MoveTagUp => UiText.Get("Make a screen reader read the chosen tag earlier. The page does not change.");

    /// <summary>Gets the Move Tag Down button.</summary>
    public static string MoveTagDown => UiText.Get("Make a screen reader read the chosen tag later. The page does not change.");

    /// <summary>Gets the Nest Tag button.</summary>
    public static string NestTag => UiText.Get("Put the chosen tag inside the tag above it, so a screen reader reads it as part of that tag.");

    /// <summary>Gets the Un-nest Tag button.</summary>
    public static string UnnestTag => UiText.Get("Move the chosen tag out of the tag it is inside, to just after it.");

    /// <summary>Gets the Remove Tag button.</summary>
    public static string RemoveTag => UiText.Get("Remove the chosen tag when it holds no page content. The tags inside it stay. Undo puts it back.");

    /// <summary>Gets the Undo button in the tags panel.</summary>
    public static string UndoTag => UiText.Get("Take back the latest change to the document, including a tag change.");

    /// <summary>Gets the Save Attachment button.</summary>
    public static string SaveAttachment => UiText.Get("Save the chosen attachment as a file.");

    /// <summary>Gets the Open Attachment button.</summary>
    public static string OpenAttachment => UiText.Get(
        "Open the chosen attached file. A PDF opens in a new tab; other files open in their own app after you agree. Programs are never "
            + "started.");

    /// <summary>Gets the splitter beside the sidebar.</summary>
    public static string SidebarSplitter => UiText.Get("Drag to make the sidebar wider or narrower.");

    /// <summary>Gets the parts and articles button.</summary>
    public static string Parts => UiText.Get("Show the document's parts and article threads, if it has any.");

    /// <summary>Gets the Previous place button of the articles list.</summary>
    public static string PreviousPlace => UiText.Get("Go back to the place before this one in the chosen article.");

    /// <summary>Gets the Next place button of the articles list.</summary>
    public static string NextPlace => UiText.Get("Go to the next place in the chosen article, in the order the file gives.");

    /// <summary>Gets the Reset Layers button.</summary>
    public static string ResetLayers => UiText.Get("Put every layer back to how the file sets it. The file does not change.");

    /// <summary>Gets the layer configuration list.</summary>
    public static string LayerConfiguration => UiText.Get("Choose one of the layer arrangements the file offers. This sets every layer to that arrangement.");

    /// <summary>Gets a layer's check box.</summary>
    public static string Layer => UiText.Get("Show or hide this layer on screen. The file does not change.");

    /// <summary>Gets the form field editor.</summary>
    public static string FormField => UiText.Get("Type into this form field. Press Tab for the next field or Esc to cancel.");

    /// <summary>Gets the document password box.</summary>
    public static string DocumentPassword => UiText.Get("Type the document's password, then press Enter.");

    /// <summary>Gets the Unlock button.</summary>
    public static string Unlock => UiText.Get("Open the document with this password.");

    /// <summary>Gets the Back to Pages button in Focus Mode.</summary>
    public static string BackToPages => UiText.Get("Leave Focus Mode and show the pages again. Shortcut: Ctrl+4");

    /// <summary>Gets the smaller text button in Focus Mode.</summary>
    public static string SmallerText => UiText.Get("Make the text smaller.");

    /// <summary>Gets the larger text button in Focus Mode.</summary>
    public static string LargerText => UiText.Get("Make the text larger.");

    /// <summary>Gets the Text Settings button in Focus Mode.</summary>
    public static string TextSettings => UiText.Get("Change the text size, spacing, width, colour and typeface.");

    /// <summary>Gets the text size slider.</summary>
    public static string TextSize => UiText.Get("Change the size of the text.");

    /// <summary>Gets the line spacing slider.</summary>
    public static string LineSpacing => UiText.Get("Change the space between lines.");

    /// <summary>Gets the paragraph spacing slider.</summary>
    public static string ParagraphSpacing => UiText.Get("Change the space between paragraphs.");

    /// <summary>Gets the text width slider.</summary>
    public static string TextWidth => UiText.Get("Change how wide the column of text is.");

    /// <summary>Gets the Focus Mode page colour list.</summary>
    public static string FocusPageColour => UiText.Get("Choose the colour behind the text.");

    /// <summary>Gets the typeface list.</summary>
    public static string Typeface => UiText.Get("Choose the typeface for the text.");

    /// <summary>Gets the tab finder button.</summary>
    public static string TabFinder => UiText.Get("Find an open tab by part of its name, title or folder. Shortcut: Ctrl+Shift+A");

    /// <summary>Gets the tab finder box.</summary>
    public static string TabFinderBox => UiText.Get("Type part of a name, title or folder. Press Enter to go to the first tab found.");

    /// <summary>Gets the Open button and menu item.</summary>
    public static string Open => UiText.Get("Choose PDF files to open. Shortcut: Ctrl+O");

    /// <summary>Gets the Open Recent menu.</summary>
    public static string OpenRecent => UiText.Get("Open a document you had open recently. It opens at the page you left it on.");

    /// <summary>Gets the New Window menu item.</summary>
    public static string NewWindow => UiText.Get("Open another window showing this document at the same page, to read two places side by side. Shortcut: Ctrl+N");

    /// <summary>Gets the tabs and settings menu button.</summary>
    public static string TabsMenu => UiText.Get("Open the menu for tabs and settings, such as Preferences, page colour and closed tabs.");

    /// <summary>Gets the Reopen Closed Tabs menu item.</summary>
    public static string ReopenClosedTabs => UiText.Get("Open the tabs you closed again. Shortcut: Ctrl+Shift+T");

    /// <summary>Gets the Search in Folder menu item.</summary>
    public static string SearchFolder => UiText.Get("Search for words in every PDF in a folder. Shortcut: Ctrl+Shift+F");

    /// <summary>Gets the comfort page colour menu item.</summary>
    public static string ComfortPageColour => UiText.Get("Show the pages in a softer colour that is easier on the eyes. Shortcut: Ctrl+I");

    /// <summary>Gets the About and Licences menu item.</summary>
    public static string Licences => UiText.Get("Read the licence for this app and for every component it includes or downloads.");

    /// <summary>Gets the search box of the Licences window.</summary>
    public static string LicenceSearch => UiText.Get("Type part of a name, a licence such as MIT, or where a component comes from, to narrow the list.");

    /// <summary>Gets the licence tree of the Licences window.</summary>
    public static string LicenceTree => UiText.Get("Choose a component to read its licence. Components are grouped by licence. Use the arrow keys to move.");

    /// <summary>Gets the licence text box of the Licences window.</summary>
    public static string LicenceText => UiText.Get("The full licence text of the chosen component. You can select and copy parts of it.");

    /// <summary>Gets the Copy button of the Licences window.</summary>
    public static string CopyLicence => UiText.Get("Copy the full licence text of the chosen component to the clipboard.");

    /// <summary>Gets the Preferences menu item.</summary>
    public static string Preferences => UiText.Get("Change how the app looks and works. Shortcut: Ctrl+Comma");

    /// <summary>Gets the Close All Tabs menu item.</summary>
    public static string CloseAllTabs => UiText.Get("Close every open tab.");

    /// <summary>Gets the close button on a tab.</summary>
    public static string CloseTab => UiText.Get("Close this tab. Shortcut: Ctrl+W");

    /// <summary>Gets the colour scheme list.</summary>
    public static string ColourScheme => UiText.Get("Choose the colours of the app, such as Calm, Dark or High contrast.");

    /// <summary>Gets the interface language list.</summary>
    public static string InterfaceLanguage => UiText.Get("Choose the interface language. Changes apply immediately.");

    /// <summary>Gets the PDF page colour list.</summary>
    public static string PageColour => UiText.Get("Choose the colour of the pages on screen, including high-contrast pairs or your own colours. Printed pages keep their own colours.");

    /// <summary>Gets describes the custom paper colour box.</summary>
    public static string CustomPaperColour => UiText.Get("The colour of the page background, as six hex digits such as 000000 for black.");

    /// <summary>Gets describes the custom ink colour box.</summary>
    public static string CustomInkColour => UiText.Get("The colour of the text and lines, as six hex digits such as FFFF00 for yellow.");

    /// <summary>Gets the tool bar style list.</summary>
    public static string ToolbarStyle => UiText.Get("Choose whether tool bar buttons show their names beside their icons.");

    /// <summary>Gets the file change list.</summary>
    public static string FileChange => UiText.Get("Choose what happens when an open file is changed by another program.");

    /// <summary>Gets the motion list.</summary>
    public static string Motion => UiText.Get("Choose whether the app uses movement, such as smooth scrolling.");

    /// <summary>Gets the text cursor list.</summary>
    public static string TextCursor => UiText.Get("Choose whether the text cursor stays steady or blinks.");

    /// <summary>Gets the interface text size list.</summary>
    public static string InterfaceTextSize => UiText.Get("Choose the size of the text in the app's buttons, menus and lists.");

    /// <summary>Gets the form field colour list.</summary>
    public static string FormHighlightColor => UiText.Get("Choose the colour that marks the form fields you can fill in. It shows on top of each field.");

    /// <summary>Gets the form field colour strength list.</summary>
    public static string FormHighlightOpacity => UiText.Get("Choose how strong the form field colour is. Off removes it, and a higher number makes it stronger.");

    /// <summary>Gets the sharpness set list.</summary>
    public static string SharpnessPreset => UiText.Get("Choose a set of sharpness choices. Smooth uses soft edges. Sharp gives text hard edges. ")
        + UiText.Get(
        "Crisp gives text, lines and pictures hard edges and fits lines to whole pixels. Custom shows that the choices under Advanced match "
            + "none of them.");

    /// <summary>Gets the advanced sharpness group.</summary>
    public static string AdvancedSharpness => UiText.Get("Show or hide the individual sharpness choices. Changing one of them can move the set above to Custom.");

    /// <summary>Gets the text edges list.</summary>
    public static string TextEdges => UiText.Get("Choose how the edges of letters are drawn. Smooth blends them into the page. Sharp gives each pixel only dark or clear. ")
        + UiText.Get("LCD softens the red, green and blue parts of each pixel separately, which can make small text look sharper on a plain page.");

    /// <summary>Gets the small text hinting list.</summary>
    public static string TextHinting => UiText.Get(
        "Choose how small letters sit on the screen's pixels. None keeps spacing even. Slight puts each line on whole pixel rows. Full "
            + "gives the crispest stems with uneven spacing.");

    /// <summary>Gets the small text contrast list.</summary>
    public static string TextContrast => UiText.Get(
        "Make the edges of small letters darker so they are easier to read. Normal draws them as the font gives them. Each step above that "
            + "makes them darker.");

    /// <summary>Gets the small text masks box.</summary>
    public static string SmallTextMasks => UiText.Get(
        "Draw small letters from shapes made once and reused. Pages of text then draw faster. The fit, contrast and LCD choices turn this "
            + "on by themselves.");

    /// <summary>Gets the smooth graphics box.</summary>
    public static string SmoothGraphics => UiText.Get("Smooth the edges of lines and shapes. Turn this off for crisp, hard edges.");

    /// <summary>Gets the smooth images box.</summary>
    public static string SmoothImages => UiText.Get(
        "Blend the pixels of pictures when they are made bigger or smaller. Turn this off to show the nearest source pixel, with hard "
            + "edges.");

    /// <summary>Gets the stroke adjustment list.</summary>
    public static string StrokeAdjustment => UiText.Get("Fit lines to whole screen pixels so thin lines are drawn a whole number of pixels thick. ")
        + UiText.Get("Choose Off to never do it, When the page asks to follow the file, or Always.");

    /// <summary>Gets the minimum line thickness list.</summary>
    public static string MinimumLineThickness => UiText.Get("Make the thinnest lines at least this many screen pixels thick. Off keeps each line as the file draws it.");

    /// <summary>Gets the area image reduction box.</summary>
    public static string AreaImageReduction => UiText.Get(
        "When a big picture is shown much smaller, average the pixels it covers instead of picking some of them. This applies to pictures "
            + "inside the page content as well as stored ones.");

    /// <summary>Gets the opening zoom list.</summary>
    public static string OpeningZoom => UiText.Get("Choose the zoom for new documents. Fit page shows the whole page. Fit width fills the width.");

    /// <summary>Gets the help for the reopen at last page choice.</summary>
    public static string ReopenAtLastPage => UiText.Get("When ticked, a document you open again starts at the page you last read in it.");

    /// <summary>Gets the help for following a document's opening page.</summary>
    public static string FollowOpenAction => UiText.Get("When ticked, a document that names a page to open at starts there. The page you last read always comes first.");

    /// <summary>Gets the help for following a document's page mode.</summary>
    public static string FollowPageMode => UiText.Get("When ticked, a document that asks for its bookmarks, thumbnails, attachments or layers shows that panel when it opens.");

    /// <summary>Gets the help for following a document's page layout and direction.</summary>
    public static string FollowPageLayout => UiText.Get(
        "When ticked, a document that asks for two pages side by side, or for right to left pages, opens that way. The View menu still "
            + "changes it.");

    /// <summary>Gets the help for following a document's title preference.</summary>
    public static string FollowDisplayDocTitle => UiText.Get(
        "When ticked, a document that asks for its title to be shown is named by that title in its tab. Otherwise the tab shows the file "
            + "name.");

    /// <summary>Gets the help for following a document's print choices.</summary>
    public static string FollowPrintDefaults => UiText.Get("When ticked, the print window starts with the copies, two-sided choice and scaling the document asks for. You can change them.");

    /// <summary>Gets the help for ignoring bookmark colours.</summary>
    public static string IgnoreOutlineColours => UiText.Get(
        "When ticked, every bookmark uses the colour of your theme, so none is hard to read. Bold and italic bookmarks stay bold and "
            + "italic.");

    /// <summary>Gets the help for following a document's page transitions.</summary>
    public static string FollowPageTransitions => UiText.Get(
        "When ticked, presenting shows the effect a page asks for as it appears, such as a fade or a wipe. Off by default. Reduced motion "
            + "turns it off.");

    /// <summary>Gets the help for following a document's page timings.</summary>
    public static string FollowPageTimings => UiText.Get(
        "When ticked, presenting moves to the next page after the time the page asks for. Off by default, so pages change only when you "
            + "ask. ")
        + UiText.Get("Reduced motion turns it off. Keys and clicks still move pages.");

    /// <summary>Gets the help for keeping the keyboard cursor on pages.</summary>
    public static string KeepKeyboardCursor => UiText.Get(
        "When ticked, documents open with a text cursor on the page, so the arrow keys move through the words and a screen reader can "
            + "follow. ")
        + UiText.Get("It works like Caret browsing (F7), which you can still switch off for a tab.");

    /// <summary>Gets the help for the spelling check choice.</summary>
    public static string CheckSpelling => UiText.Get("When ticked, misspelled words in form fields are underlined. Right-click one for corrections, from this computer's own dictionary.");

    /// <summary>Gets the comment author box.</summary>
    public static string CommentAuthor => UiText.Get("Type the name shown as the author of your notes, replies and other annotations. Leave it empty to use your user name.");

    /// <summary>Gets the Read Aloud voice service list.</summary>
    public static string SpeechEngine => UiText.Get("Choose which voice service reads aloud.");

    /// <summary>Gets the Azure Speech key box.</summary>
    public static string AzureKey => UiText.Get("Type a key from your Azure Speech resource. Azure voices need it.");

    /// <summary>Gets the Azure region box.</summary>
    public static string AzureRegion => UiText.Get("Type the region of your Azure Speech resource, such as uksouth.");

    /// <summary>Gets the timestamp server box.</summary>
    public static string TimestampServer => UiText.Get("Optional. Type the address of a server that records a trusted time when you sign.");

    /// <summary>Gets a Close button on a window that has nothing to save.</summary>
    public static string CloseWindow => UiText.Get("Close this window. Shortcut: Esc");

    /// <summary>Gets the Close button on Preferences.</summary>
    public static string ClosePreferences => UiText.Get("Close Preferences. Your changes are already kept. Shortcut: Esc");

    /// <summary>Gets a Cancel button.</summary>
    public static string Cancel => UiText.Get("Close this window without making any changes. Shortcut: Esc");

    /// <summary>Gets the print destination list.</summary>
    public static string PrintDestination => UiText.Get("Choose a printer, save as a PDF file or use the system print dialog.");

    /// <summary>Gets the pages to print list.</summary>
    public static string PrintPages => UiText.Get("Choose which pages to print: all, the current page or your own list.");

    /// <summary>Gets the page range box.</summary>
    public static string PageRange => UiText.Get("Type the pages to print, such as 1-3, 7.");

    /// <summary>Gets the copies box.</summary>
    public static string Copies => UiText.Get("Choose how many copies to print.");

    /// <summary>Gets the print colour list.</summary>
    public static string PrintColour => UiText.Get("Choose to print in colour or in black and white.");

    /// <summary>Gets the two-sided printing check box.</summary>
    public static string TwoSided => UiText.Get("Print on both sides of the paper.");

    /// <summary>Gets the page turning edge list.</summary>
    public static string TurnPages => UiText.Get("Choose which edge the paper turns on when printing on both sides.");

    /// <summary>Gets the print layout list.</summary>
    public static string PrintLayout => UiText.Get("Choose normal pages, a folded booklet or a poster made from several sheets.");

    /// <summary>Gets the pages per sheet list.</summary>
    public static string PagesPerSheet => UiText.Get("Choose how many pages to fit on each sheet of paper.");

    /// <summary>Gets the paper size list.</summary>
    public static string PaperSize => UiText.Get("Choose the size of the paper.");

    /// <summary>Gets the print page size (scaling) choice.</summary>
    public static string PrintScaling => UiText.Get("Choose how big each page prints: filling the paper, at its true size, shrunk only when too big, or at a chosen scale.");

    /// <summary>Gets the custom print scale.</summary>
    public static string PrintScalePercent => UiText.Get("Set the scale as a percentage of the page's true size, from 10 to 400.");

    /// <summary>Gets the print annotations check box.</summary>
    public static string PrintAnnotations => UiText.Get("Print the highlights, notes and other annotations too.");

    /// <summary>Gets the print button in the print window.</summary>
    public static string PrintNext => UiText.Get("Go on to print with these settings.");

    /// <summary>Gets the system print dialog button.</summary>
    public static string SystemPrintDialog => UiText.Get("Print with your computer's own print window instead. Shortcut: Ctrl+Shift+P");

    /// <summary>Gets the certificate file box.</summary>
    public static string CertificateFile => UiText.Get("Type where your certificate file is, a .p12 or .pfx file.");

    /// <summary>Gets the button that chooses a certificate file.</summary>
    public static string ChooseCertificate => UiText.Get("Choose your certificate file from a folder.");

    /// <summary>Gets the certificate password box.</summary>
    public static string CertificatePassword => UiText.Get("Type the password for your certificate. It is never saved.");

    /// <summary>Gets the list of remembered certificates.</summary>
    public static string RememberedCertificates => UiText.Get("Pick a certificate you asked this computer to remember.");

    /// <summary>Gets the button that forgets a remembered certificate.</summary>
    public static string ForgetCertificate => UiText.Get("Forget the picked certificate. The certificate file itself is not changed.");

    /// <summary>Gets the check box that remembers a certificate.</summary>
    public static string RememberCertificate => UiText.Get("Offer this certificate next time. Only where the file is and the name in it are kept, never the password.");

    /// <summary>Gets the Signature button in Fill and Sign.</summary>
    public static string SignatureMark => UiText.Get("Type, draw or choose a picture of your signature, then place it on the page.");

    /// <summary>Gets the Initials button in Fill and Sign.</summary>
    public static string InitialsMark => UiText.Get("Type, draw or choose a picture of your initials, then place them on the page.");

    /// <summary>Gets the button that places the mark being previewed.</summary>
    public static string PlaceMark => UiText.Get("Put the signature on the page where the preview is. Shortcut: Enter");

    /// <summary>Gets the button that stops placing a mark.</summary>
    public static string CancelPlacement => UiText.Get("Stop placing the signature. The document is not changed. Shortcut: Escape");

    /// <summary>Gets the button that makes the mark being placed bigger.</summary>
    public static string BiggerMark => UiText.Get("Make the signature being placed bigger. Shortcut: +");

    /// <summary>Gets the button that makes the mark being placed smaller.</summary>
    public static string SmallerMark => UiText.Get("Make the signature being placed smaller. Shortcut: -");

    /// <summary>Gets the button that picks up the last placed mark again.</summary>
    public static string AdjustMark => UiText.Get("Pick up the signature you just placed so you can move or resize it again.");

    /// <summary>Gets the button that removes the last placed mark.</summary>
    public static string RemoveMark => UiText.Get("Take the signature you just placed off the page.");

    /// <summary>Gets the Type option when making a signature.</summary>
    public static string MarkType => UiText.Get("Make it by typing. This works with only a keyboard.");

    /// <summary>Gets the Draw option when making a signature.</summary>
    public static string MarkDraw => UiText.Get("Make it by drawing with a mouse, pen or finger.");

    /// <summary>Gets the Image option when making a signature.</summary>
    public static string MarkImage => UiText.Get("Make it from a picture, such as a photo or scan of your signature.");

    /// <summary>Gets the text box when typing a signature.</summary>
    public static string MarkText => UiText.Get("Type it as you want it to appear.");

    /// <summary>Gets the drawing area when drawing a signature.</summary>
    public static string MarkPad => UiText.Get("Draw here. Each line you draw is added until you clear it.");

    /// <summary>Gets the button that clears a drawn signature.</summary>
    public static string ClearDrawing => UiText.Get("Rub out the drawing and start again.");

    /// <summary>Gets the button that chooses a picture of a signature.</summary>
    public static string ChooseSignatureImage => UiText.Get("Choose a picture of your signature from a folder.");

    /// <summary>Gets the check box that removes white paper from a picture.</summary>
    public static string RemovePaper => UiText.Get("Make white paper see-through so only the ink shows. Untick to keep the picture as it is.");

    /// <summary>Gets the check box that remembers a signature.</summary>
    public static string RememberMark => UiText.Get("Keep it on this computer to use again. Untick to forget it after this time.");

    /// <summary>Gets the button that forgets a remembered signature.</summary>
    public static string ForgetMark => UiText.Get("Forget the remembered one now. Signatures already placed are not changed.");

    /// <summary>Gets the button that uses the signature that was made.</summary>
    public static string UseMark => UiText.Get("Use this, then choose where it goes on the page.");

    /// <summary>Gets the signing reason box.</summary>
    public static string SigningReason => UiText.Get("Optional. Type why you are signing, such as Approved.");

    /// <summary>Gets the signing location box.</summary>
    public static string SigningLocation => UiText.Get("Optional. Type where you are signing.");

    /// <summary>Gets the signature kind box.</summary>
    public static string SignatureKind => UiText.Get("Choose a plain approval, or certify the document and say which later changes are allowed.");

    /// <summary>Gets the visible signature box.</summary>
    public static string VisibleSignature => UiText.Get("Draws the signer, reason, location and date on the page, so people can see the signature.");

    /// <summary>Gets the Sign button.</summary>
    public static string Sign => UiText.Get("Choose where to save the signed copy, then sign it.");

    /// <summary>Gets the confirm button in a question window.</summary>
    public static string Confirm => UiText.Get("Go ahead with what this window asks about.");

    /// <summary>Gets the text box in a question window.</summary>
    public static string PromptInput => UiText.Get("Type your answer here.");

    /// <summary>Gets the confirm button in a question window with a text box.</summary>
    public static string PromptConfirm => UiText.Get("Use what you typed.");

    /// <summary>Gets the choose folder button.</summary>
    public static string ChooseFolder => UiText.Get("Choose the folder to search.");

    /// <summary>Gets the folder box.</summary>
    public static string FolderBox => UiText.Get("Type the folder to search.");

    /// <summary>Gets the words to find in a folder.</summary>
    public static string FolderQuery => UiText.Get("Type the words to find, then press Enter.");

    /// <summary>Gets the Search button in folder search.</summary>
    public static string SearchFolderStart => UiText.Get("Search every PDF in the folder for the words.");

    /// <summary>Gets the Stop button in folder search.</summary>
    public static string StopSearch => UiText.Get("Stop searching the folder.");

    /// <summary>Gets the include subfolders check box.</summary>
    public static string Subfolders => UiText.Get("Also search the folders inside this folder.");

    /// <summary>Gets the pages, read out by screen readers only so hovering the pages shows no tooltip.</summary>
    public static string Pages => UiText.Get(
        "The document's pages. Page Up and Page Down move a page at a time. F7 turns on a text cursor for the arrow keys. The value is the "
            + "current page's text.");

    /// <summary>Gets the text typed on the page.</summary>
    public static string PageText => UiText.Get("Type here. Enter starts a new line, a click elsewhere keeps the text and Escape cancels. Shortcut: Ctrl+Enter");

    /// <summary>Gets the font family box.</summary>
    public static string FontFamily => UiText.Get("Choose the font of the text you type, or of the text you picked. Each font is shown in its own letters.");

    /// <summary>Gets the font size box.</summary>
    public static string FontSize => UiText.Get("Choose or type the text size in points.");

    /// <summary>Gets the smaller text button.</summary>
    public static string ShrinkPageText => UiText.Get("Make the text one point smaller.");

    /// <summary>Gets the larger text button.</summary>
    public static string GrowPageText => UiText.Get("Make the text one point larger.");

    /// <summary>Gets the bold button.</summary>
    public static string BoldText => UiText.Get("Make the text bold, or not. Shortcut: Ctrl+B while typing.");

    /// <summary>Gets the italic button.</summary>
    public static string ItalicText => UiText.Get("Make the text italic, or not. Shortcut: Ctrl+I while typing.");

    /// <summary>Gets the underline button.</summary>
    public static string UnderlineText => UiText.Get("Underline the text, or not. Shortcut: Ctrl+U while typing.");

    /// <summary>Gets the align left button.</summary>
    public static string AlignLeft => UiText.Get("Start each line at the left of the text box.");

    /// <summary>Gets the centre button.</summary>
    public static string AlignCenter => UiText.Get("Centre each line in the text box.");

    /// <summary>Gets the align right button.</summary>
    public static string AlignRight => UiText.Get("End each line at the right of the text box.");

    /// <summary>Gets the text colour button.</summary>
    public static string TextColour => UiText.Get("Choose the colour of the text you type, or of the text you picked.");

    /// <summary>Gets the spacing button.</summary>
    public static string TextSpacing => UiText.Get("Choose the space between lines and between letters.");

    /// <summary>Gets the text properties button.</summary>
    public static string TextProperties => UiText.Get("Set every text setting with exact numbers, including comb boxes and the wrap width.");

    /// <summary>Gets the finish text button.</summary>
    public static string FinishText => UiText.Get("Keep the text you are typing. Shortcut: Ctrl+Enter.");

    /// <summary>Gets the add text tool in Fill and Sign.</summary>
    public static string AddText => UiText.Get("Click anywhere on the page and type, as on paper. Click a form field to fill it in.");

    /// <summary>Gets the text box in the text properties window.</summary>
    public static string TextPropertiesText => UiText.Get("The text. Enter starts a new line.");

    /// <summary>Gets the alignment box.</summary>
    public static string TextAlignment => UiText.Get("Choose where lines sit across the text box.");

    /// <summary>Gets the wrap width box.</summary>
    public static string WrapWidth => UiText.Get("The width lines wrap at, in points. 0 lets the box grow as you type.");

    /// <summary>Gets the comb boxes box.</summary>
    public static string CombBoxes => UiText.Get("Put one letter in each of this many evenly spaced boxes, as on forms with a box per letter. 0 writes running text.");

    /// <summary>Gets the apply button of the text properties window.</summary>
    public static string ApplyTextProperties => UiText.Get("Use these settings for the text.");

    /// <summary>Gets the preview only fonts preference.</summary>
    public static string PreviewOnlyFonts => UiText.Get(
        "Some fonts' licences forbid copying them into documents. When this is on, you can type in them on screen; the saved PDF uses the "
            + "closest built in font, so the font is never copied.");

    /// <summary>Gets the Save Optimised Copy menu item.</summary>
    public static string OptimizeCopy => UiText.Get("Save a smaller or more accessible copy of this document. Your open file is never changed.");

    /// <summary>Gets the Save PDF/A Copy menu item.</summary>
    public static string ArchiveCopy => UiText.Get("Save a PDF/A copy of this document, made for keeping for many years. Your open file is never changed.");

    /// <summary>Gets the Save as PDF 2.0 Copy menu item.</summary>
    public static string Pdf20Copy => UiText.Get("Save a copy of this document as a PDF 2.0 file. Details in features PDF 2.0 retired are moved when they can be. ")
        + UiText.Get("The copy is refused with a reason when something cannot be kept. Your open file is never changed.");

    /// <summary>Gets the busy bar of the PDF 2.0 copy window.</summary>
    public static string Pdf20Progress => UiText.Get(CopyProgressKey);

    /// <summary>Gets the save button of the PDF 2.0 copy window.</summary>
    public static string Pdf20Save => UiText.Get(CopySaveKey);

    /// <summary>Gets the cancel button of the PDF 2.0 copy window.</summary>
    public static string Pdf20Cancel => UiText.Get(CopyCancelKey);

    /// <summary>Gets the summary of the PDF 2.0 copy window.</summary>
    public static string Pdf20Result => UiText.Get("What the copy changed or moved to become a PDF 2.0 file, or why it could not be made.");

    /// <summary>Gets the Protect with Password menu item.</summary>
    public static string ProtectCopy => UiText.Get("Save a copy of this document that needs a password to open, or that limits printing, copying and editing. ")
        + UiText.Get("Your open file is never changed. A signed or PDF/A document is refused with the reason.");

    /// <summary>Gets the Remove Protection menu item.</summary>
    public static string RemoveProtectionCopy => UiText.Get(
        "Save a copy of this document without passwords or limits. A document opened with only its reading password needs the owner "
            + "password. Your open file is never changed.");

    /// <summary>Gets the encryption choices of the protect window.</summary>
    public static string ProtectFormats => UiText.Get("Choose how strongly the copy is locked. Use the arrow keys to move between the choices.");

    /// <summary>Gets the current owner password of the protect and remove protection windows.</summary>
    public static string CurrentOwnerPassword => UiText.Get(
        "The owner password of the protection this document has now. It is needed because you opened the document with its reading "
            + "password. It is never saved.");

    /// <summary>Gets the reading password of the protect window.</summary>
    public static string ReadingPassword => UiText.Get("People type this password to open the copy. Leave it empty to let anyone open it. It is never saved or shown.");

    /// <summary>Gets the repeat reading password of the protect window.</summary>
    public static string ReadingPasswordRepeat => UiText.Get("Type the reading password again, so a typing mistake does not lock you out.");

    /// <summary>Gets the owner password of the protect window.</summary>
    public static string OwnerPasswordField => UiText.Get("This password gives every permission and can change or remove the protection. ")
        + UiText.Get("It must differ from the reading password when you limit what people can do. It is never saved or shown.");

    /// <summary>Gets the repeat owner password of the protect window.</summary>
    public static string OwnerPasswordRepeat => UiText.Get("Type the owner password again, so a typing mistake does not lock you out.");

    /// <summary>Gets the print permission of the protect window.</summary>
    public static string AllowPrint => UiText.Get("Let people with the reading password print the document.");

    /// <summary>Gets the copy permission of the protect window.</summary>
    public static string AllowCopy => UiText.Get("Let people with the reading password copy text and pictures out of the document.");

    /// <summary>Gets the edit permission of the protect window.</summary>
    public static string AllowEdit => UiText.Get("Let people with the reading password change, add or remove pages.");

    /// <summary>Gets the forms permission of the protect window.</summary>
    public static string AllowForms => UiText.Get("Let people with the reading password fill in forms and add comments.");

    /// <summary>Gets the accessibility permission of the protect window.</summary>
    public static string AllowAccessibility => UiText.Get("Let screen readers and other assistive tools read the text. Leave this on unless you are sure no one needs it.");

    /// <summary>Gets the metadata choice of the protect window.</summary>
    public static string HideProperties => UiText.Get("Also hide the document properties, such as title and author, until the password is typed.");

    /// <summary>Gets the warning of the protect window.</summary>
    public static string ProtectWarning => UiText.Get("Explains what happens to screen readers with the choices made.");

    /// <summary>Gets the progress bar of the protect and remove protection windows.</summary>
    public static string ProtectProgress => UiText.Get(CopyProgressKey);

    /// <summary>Gets the save button of the protect window.</summary>
    public static string ProtectSave => UiText.Get("Check the choices, choose where to save, then make the protected copy. The open file is never replaced.");

    /// <summary>Gets the save button of the remove protection window.</summary>
    public static string RemoveProtectionSave => UiText.Get("Choose where to save, then make the copy without protection. The open file is never replaced.");

    /// <summary>Gets the cancel button of the protect and remove protection windows.</summary>
    public static string ProtectCancel => UiText.Get(CopyCancelKey);

    /// <summary>Gets the summary of the protect and remove protection windows.</summary>
    public static string ProtectResult => UiText.Get("What the copy holds, or why it could not be made.");

    /// <summary>Gets the Add Validation Data or Timestamp menu item.</summary>
    public static string ValidationData => UiText.Get(
        "Save a copy of this signed document with the data its signatures need to be checked years later, or with a trusted timestamp. Your "
            + "open file is never changed.");

    /// <summary>Gets the choices of the validation data window.</summary>
    public static string ValidationActions => UiText.Get("Choose to add validation data or a document timestamp. Use the arrow keys to move between the choices.");

    /// <summary>Gets the record box of the validation data window.</summary>
    public static string ValidationRecords => UiText.Get("Also add a separate record for each signature, so a checker can find the data that belongs to it.");

    /// <summary>Gets the busy bar of the validation data window.</summary>
    public static string ValidationProgress => UiText.Get(CopyProgressKey);

    /// <summary>Gets the save button of the validation data window.</summary>
    public static string ValidationSave => UiText.Get(CopySaveKey);

    /// <summary>Gets the cancel button of the validation data window.</summary>
    public static string ValidationCancel => UiText.Get(CopyCancelKey);

    /// <summary>Gets the summary of the validation data window.</summary>
    public static string ValidationResult => UiText.Get("Where the copy is saved and everything you should know about how it was made.");

    /// <summary>Gets the Sign with External Signer menu item.</summary>
    public static string ExternalSign => UiText.Get("Sign a copy of this document with a key that stays on a hardware token or with a signing service. Your open file is never changed.");

    /// <summary>Gets the certificate box of the external signing window.</summary>
    public static string ExternalCertificates => UiText.Get("The signer's certificates that were read. The signing certificate comes first.");

    /// <summary>Gets the choose certificates button of the external signing window.</summary>
    public static string ExternalChooseCertificates => UiText.Get("Choose the files that hold the signer's certificate and the certificates that lead to a trusted root.");

    /// <summary>Gets the key kind box of the external signing window.</summary>
    public static string ExternalKey => UiText.Get("Choose the kind of signature the signer's key makes. It is chosen for you from the certificate.");

    /// <summary>Gets the prepare button of the external signing window.</summary>
    public static string ExternalPrepare => UiText.Get("Build the file with room for the signature and work out the bytes your signer must sign.");

    /// <summary>Gets the save bytes button of the external signing window.</summary>
    public static string ExternalSaveToSign => UiText.Get("Save the bytes your signer must sign to a file you can give to the signer.");

    /// <summary>Gets the answer kind box of the external signing window.</summary>
    public static string ExternalResultKind => UiText.Get("Choose what your signer gave back: a signature value, or a whole signature container.");

    /// <summary>Gets the finish button of the external signing window.</summary>
    public static string ExternalFinish => UiText.Get("Choose the file your signer gave back, then choose where to save the signed copy.");

    /// <summary>Gets the Export Pages menu item.</summary>
    public static string ExportPages => UiText.Get("Save pages as picture files, or as SVG, PostScript or EPS drawings. Your open file is never changed.");

    /// <summary>Gets the format list of the export pages window.</summary>
    public static string ExportFormats => UiText.Get("Choose the kind of file to make. Use the arrow keys to move between the choices.");

    /// <summary>Gets the page choices of the export pages window.</summary>
    public static string ExportScopes => UiText.Get("Choose which pages to export. Use the arrow keys to move between the choices.");

    /// <summary>Gets the page range box of the export pages window.</summary>
    public static string ExportPageRange => UiText.Get("Type the pages to export, such as 1-3, 7. Used when you choose the pages you type.");

    /// <summary>Gets the resolution box of the export pages window.</summary>
    public static string ExportResolution => UiText.Get("How sharp a picture is, in dots to the inch. Higher is sharper and the file is larger. Drawings ignore it.");

    /// <summary>Gets the marks box of the export pages window.</summary>
    public static string ExportMarkup => UiText.Get("Draw comments, form fields and other marks on the pages.");

    /// <summary>Gets the busy bar of the export pages window.</summary>
    public static string ExportProgress => UiText.Get("Moves while the pages are being exported.");

    /// <summary>Gets the export button of the export pages window.</summary>
    public static string ExportSave => UiText.Get("Choose where to save, then export. A format that holds one page makes a numbered file for each page.");

    /// <summary>Gets the cancel button of the export pages window.</summary>
    public static string ExportCancel => UiText.Get("Stop exporting. Files already finished stay saved.");

    /// <summary>Gets the summary of the export pages window.</summary>
    public static string ExportResult => UiText.Get("What was saved, what the format left out and what was drawn a little differently.");

    /// <summary>Gets the progress bar of the external signing window.</summary>
    public static string ExternalProgress => UiText.Get("Moves while a step is running.");

    /// <summary>Gets the cancel button of the external signing window.</summary>
    public static string ExternalCancel => UiText.Get("Stop the step that is running. No file is saved.");

    /// <summary>Gets the summary of the external signing window.</summary>
    public static string ExternalResult => UiText.Get("Where the signed copy is saved and everything you should know about how it was made.");

    /// <summary>Gets the Save Flattened Copy menu item.</summary>
    public static string FlattenCopy => UiText.Get("Save a copy in which form fields, comments and other marks become part of the page and can no longer be changed. ")
        + UiText.Get("A signed document is refused with a reason. Your open file is never changed.");

    /// <summary>Gets the scope choices of the flattened copy window.</summary>
    public static string FlattenScopes => UiText.Get("Choose what becomes part of the page. Use the arrow keys to move between the choices.");

    /// <summary>Gets the look choices of the flattened copy window.</summary>
    public static string FlattenLooks => UiText.Get("Choose whether the copy keeps the screen look or the print look. Use the arrow keys to move between the choices.");

    /// <summary>Gets the upright box of the flattened copy window.</summary>
    public static string FlattenUpright => UiText.Get(
        "Save pages that are shown turned the right way up, with the turn built into the page. Links and bookmarks on turned pages may land "
            + "in a different place.");

    /// <summary>Gets the busy bar of the flattened copy window.</summary>
    public static string FlattenProgress => UiText.Get(CopyProgressKey);

    /// <summary>Gets the save button of the flattened copy window.</summary>
    public static string FlattenSave => UiText.Get(CopySaveKey);

    /// <summary>Gets the cancel button of the flattened copy window.</summary>
    public static string FlattenCancel => UiText.Get(CopyCancelKey);

    /// <summary>Gets the summary of the flattened copy window.</summary>
    public static string FlattenResult => UiText.Get("What became part of the page, what was left alone and why, or why the copy could not be made.");

    /// <summary>Gets the level choices of the PDF/A copy window.</summary>
    public static string ArchiveLevels => UiText.Get("Choose the PDF/A level the copy must meet. Use the arrow keys to move between the choices.");

    /// <summary>Gets the busy bar of the PDF/A copy window.</summary>
    public static string ArchiveProgress => UiText.Get(CopyProgressKey);

    /// <summary>Gets the save button of the PDF/A copy window.</summary>
    public static string ArchiveSave => UiText.Get(CopySaveKey);

    /// <summary>Gets the cancel button of the PDF/A copy window.</summary>
    public static string ArchiveCancel => UiText.Get(CopyCancelKey);

    /// <summary>Gets the summary of the PDF/A copy window.</summary>
    public static string ArchiveResult => UiText.Get("What the copy changed to meet the level, or why it could not be made.");

    /// <summary>Gets the describe pictures box of the optimised copy window.</summary>
    public static string OptimizeDescribePictures => UiText.Get("Write a short description of each picture as its alternate text, so a screen reader can say what it shows. "
        + "A small model on this computer makes the description, and nothing is sent anywhere. "
        + "The first time, it downloads the model from this project's release. The descriptions can be wrong, so check them. "
        + "Needs the reading structure box.");

    /// <summary>Gets the size choices of the optimised copy window.</summary>
    public static string OptimizePresets => UiText.Get("Choose how much picture detail may be traded for a smaller file. Use the arrow keys to move between the choices.");

    /// <summary>Gets the fill in accessibility box of the optimised copy window.</summary>
    public static string OptimizeAccessibility => UiText.Get("Add the document language, a window title and tag flags when they are missing, so screen readers read the file correctly.");

    /// <summary>Gets the language box of the optimised copy window.</summary>
    public static string OptimizeLanguage => UiText.Get("The language of the text, such as en-AU. It is used only when the document does not say.");

    /// <summary>Gets the add tags box of the optimised copy window.</summary>
    public static string OptimizeTags => UiText.Get(
        "Guess headings, paragraphs and pictures from the page layout when the document has no reading structure. The guess can be wrong, "
            + "so check it.");

    /// <summary>Gets the clean up box of the optimised copy window.</summary>
    public static string OptimizeCleanUp => UiText.Get("Remove page thumbnails, private application data, unused resources and empty comment lists. Pages look the same.");

    /// <summary>Gets the progress bar of the optimised copy window.</summary>
    public static string OptimizeProgress => UiText.Get("Shows how much of the copy is written.");

    /// <summary>Gets the save button of the optimised copy window.</summary>
    public static string OptimizeSave => UiText.Get("Choose where to save the copy, then write it. The open file is never replaced.");

    /// <summary>Gets the cancel button of the optimised copy window.</summary>
    public static string OptimizeCancel => UiText.Get("Stop writing the copy. No file is saved.");

    /// <summary>Gets the summary of the optimised copy window.</summary>
    public static string OptimizeResult => UiText.Get("What the saved copy changed, what was left alone and what needs your attention.");

    /// <summary>Gets the mark area to redact menu item.</summary>
    public static string RedactArea => UiText.Get(
        "Drag a box on the page to mark an area to redact. Nothing is removed until you apply the redactions. Only the HyperPDF engine can "
            + "do this.");

    /// <summary>Gets the mark text to redact menu item.</summary>
    public static string RedactText => UiText.Get("Select text to mark it to redact. Nothing is removed until you apply the redactions. Only the HyperPDF engine can do this.");

    /// <summary>Gets the apply redactions menu item.</summary>
    public static string ApplyRedactions => UiText.Get("Remove everything under the marks for good and save a redacted copy. Your open file is never changed.");

    /// <summary>Gets the pictures choices of the redaction window.</summary>
    public static string RedactImages => UiText.Get("Choose what happens to a picture that a mark touches. Use the arrow keys to move between the choices.");

    /// <summary>Gets the drawings choices of the redaction window.</summary>
    public static string RedactLineArt => UiText.Get("Choose what happens to a line or shape that a mark touches. Use the arrow keys to move between the choices.");

    /// <summary>Gets the hidden text box of the redaction window.</summary>
    public static string RedactHiddenText => UiText.Get("Also remove invisible text under a mark, such as the text layer of a scanned page, so it cannot be searched or copied.");

    /// <summary>Gets the links box of the redaction window.</summary>
    public static string RedactLinks => UiText.Get("Also remove links, comments and form fields under a mark, with their actions and appearances.");

    /// <summary>Gets the metadata box of the redaction window.</summary>
    public static string RedactMetadata => UiText.Get("Also remove the title, author and other document information, and the metadata stream.");

    /// <summary>Gets the confirmation box of the redaction window.</summary>
    public static string RedactConfirm => UiText.Get("Tick this to show you understand that removed content cannot be brought back after the redacted copy is saved.");

    /// <summary>Gets the progress bar of the redaction window.</summary>
    public static string RedactProgress => UiText.Get("Moves while the redactions are applied and the copy is written.");

    /// <summary>Gets the apply button of the redaction window.</summary>
    public static string RedactApply => UiText.Get("Choose where to save the redacted copy, then apply the redactions and write it. The open file is never replaced.");

    /// <summary>Gets the cancel button of the redaction window.</summary>
    public static string RedactCancel => UiText.Get("Stop applying the redactions. No file is saved.");

    /// <summary>Gets the summary of the redaction window.</summary>
    public static string RedactResult => UiText.Get("What was removed, where the redacted copy is saved, and what to check next.");

    /// <summary>Gets the Save a Copy menu item.</summary>
    public static string SaveCopy => UiText.Get(
        "Save a copy of this document as a standard PDF, an optimised smaller file, a PDF/A archive, a PDF 2.0 file or a flattened copy. "
            + "Your open file is never changed.");

    /// <summary>Gets the format choices of the Save a Copy window.</summary>
    public static string SaveCopyFormats => UiText.Get("Choose the kind of copy to save. Use the arrow keys to move between the choices.");

    /// <summary>Gets the progress bar of the Save a Copy window.</summary>
    public static string SaveCopyProgress => UiText.Get(CopyProgressKey);

    /// <summary>Gets the save button of the Save a Copy window.</summary>
    public static string SaveCopySave => UiText.Get(CopySaveKey);

    /// <summary>Gets the cancel button of the Save a Copy window.</summary>
    public static string SaveCopyCancel => UiText.Get(CopyCancelKey);

    /// <summary>Gets the summary of the Save a Copy window.</summary>
    public static string SaveCopyResult => UiText.Get("What the saved copy changed, what was left alone, or why it could not be made.");

    /// <summary>Gets the Protect and Sign submenu in the Document menu.</summary>
    public static string ProtectSignMenu => UiText.Get("Open the choices to protect with a password, remove protection, apply redactions, or sign and timestamp a copy.");

    /// <summary>Gets the note text box of an on-page comment bubble.</summary>
    public static string BubbleNote => UiText.Get("The note text for this comment. Type to change it. Shortcut: Ctrl+Enter");

    /// <summary>Gets the Save Note button of an on-page comment bubble.</summary>
    public static string BubbleSave => UiText.Get("Save your note text. Shortcut: Ctrl+Enter");

    /// <summary>Gets the Reply button of an on-page comment bubble.</summary>
    public static string BubbleReply => UiText.Get("Write a reply to this comment.");

    /// <summary>Gets the reply text box of an on-page comment bubble.</summary>
    public static string BubbleReplyInput => UiText.Get("Type your reply here. Shortcut: Ctrl+Enter");

    /// <summary>Gets the Add Reply button of an on-page comment bubble.</summary>
    public static string BubbleSendReply => UiText.Get("Add this reply to the comment. Shortcut: Ctrl+Enter");

    /// <summary>Gets the Delete button of an on-page comment bubble.</summary>
    public static string BubbleDelete => UiText.Get("Delete this comment from the page. Undo can bring it back.");

    /// <summary>Gets the Close button of an on-page comment bubble.</summary>
    public static string BubbleClose => UiText.Get("Close this comment bubble. Shortcut: Esc");

    /// <summary>Gets the button help text for opening a certificate-encrypted PDF.</summary>
    public static string OpenCertificateDocument => UiText.Get("Open this document with the certificate and private key you chose.");
}

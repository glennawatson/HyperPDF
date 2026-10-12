// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Views;

/// <summary>
/// What each control does, in short plain English. Each text is both the tooltip and the screen reader help text
/// (see <see cref="ControlHelp"/>). A shortcut, when there is one, comes after the explanation and matches
/// <see cref="Shortcuts"/>.
/// </summary>
public static class Descriptions
{
    /// <summary>The Back button.</summary>
    public static readonly string Back = $"Go back to where you were before a jump, like Back in a web browser. Jumps come from links, the outline, search results and page numbers.{Shortcut}Alt+Left";

    /// <summary>The Forward button.</summary>
    public static readonly string Forward = $"Go forward again after going Back, like Forward in a web browser.{Shortcut}Alt+Right";

    /// <summary>The Sidebar toggle.</summary>
    public static readonly string Sidebar = $"Show or hide the sidebar with page pictures, the outline and search results.{Shortcut}F9";

    /// <summary>The previous page button.</summary>
    public static readonly string PreviousPage = $"Go to the previous page.{Shortcut}Page Up";

    /// <summary>The next page button.</summary>
    public static readonly string NextPage = $"Go to the next page.{Shortcut}Page Down";

    /// <summary>The page number box.</summary>
    public static readonly string PageNumber = $"Type a page number, then press Enter to go to that page.{Shortcut}Ctrl+L";

    /// <summary>The zoom out button.</summary>
    public static readonly string ZoomOut = $"Make the pages smaller.{Shortcut}Ctrl+Minus";

    /// <summary>The zoom in button.</summary>
    public static readonly string ZoomIn = $"Make the pages bigger.{Shortcut}Ctrl+Plus";

    /// <summary>The zoom menu button, which shows the current zoom.</summary>
    public static readonly string ZoomMenu = "Shows the current zoom. Choose to fit the width, fit the whole page or pick a set size.";

    /// <summary>The fit width zoom.</summary>
    public static readonly string FitWidth = $"Make the page fill the width of the window.{Shortcut}Ctrl+2";

    /// <summary>The fit page zoom.</summary>
    public static readonly string FitPage = $"Make the whole page fit in the window.{Shortcut}Ctrl+1";

    /// <summary>The 50% zoom.</summary>
    public static readonly string Zoom50 = "Show the pages at half their real size.";

    /// <summary>The 75% zoom.</summary>
    public static readonly string Zoom75 = "Show the pages at three quarters of their real size.";

    /// <summary>The 100% zoom.</summary>
    public static readonly string Zoom100 = $"Show the pages at their real size.{Shortcut}Ctrl+0";

    /// <summary>The 125% zoom.</summary>
    public static readonly string Zoom125 = "Show the pages a quarter bigger than their real size.";

    /// <summary>The 150% zoom.</summary>
    public static readonly string Zoom150 = "Show the pages one and a half times their real size.";

    /// <summary>The 200% zoom.</summary>
    public static readonly string Zoom200 = "Show the pages twice their real size.";

    /// <summary>The 400% zoom.</summary>
    public static readonly string Zoom400 = "Show the pages four times their real size.";

    /// <summary>The Save button and menu item.</summary>
    public static readonly string Save = $"Save your annotations and form entries to the file.{Shortcut}Ctrl+S";

    /// <summary>The Print button.</summary>
    public static readonly string Print = $"Choose the pages and settings, then print.{Shortcut}Ctrl+P";

    /// <summary>The Annotate toggle.</summary>
    public static readonly string Annotate = "Show the tools to highlight, underline, draw, and add notes and text.";

    /// <summary>The Fill and Sign toggle.</summary>
    public static readonly string FillSign = "Show the tools to fill in forms and sign the document.";

    /// <summary>The Find toggle.</summary>
    public static readonly string Find = $"Search for words in this document.{Shortcut}Ctrl+F";

    /// <summary>The Focus toggle.</summary>
    public static readonly string Focus = $"Show only the text, in one calm column, without the page layout.{Shortcut}Ctrl+4";

    /// <summary>The Read Aloud toggle.</summary>
    public static readonly string ReadAloud = $"Read the document aloud, starting from this page.{Shortcut}Ctrl+Shift+Y";

    /// <summary>The View menu button.</summary>
    public static readonly string ViewMenu = "Open the View menu to turn pages, change the page layout, choose how dragging works, read or present.";

    /// <summary>The rotate left menu item.</summary>
    public static readonly string RotateLeft = $"Turn the pages a quarter turn to the left.{Shortcut}Ctrl+Left";

    /// <summary>The rotate right menu item.</summary>
    public static readonly string RotateRight = $"Turn the pages a quarter turn to the right.{Shortcut}Ctrl+Right";

    /// <summary>The single page layout menu item.</summary>
    public static readonly string SinglePage = "Show one page in each row. Scroll down for the next page.";

    /// <summary>The dual page layout menu item.</summary>
    public static readonly string DualPage = "Show two pages side by side: pages 1 and 2, then 3 and 4. Next page moves to the next pair.";

    /// <summary>The dual page with cover layout menu item.</summary>
    public static readonly string DualPageCover = "Show the first page alone like a book cover, then pages side by side: 2 and 3, then 4 and 5.";

    /// <summary>The Read Mode menu item.</summary>
    public static readonly string ReadMode = $"Put the tool bars and sidebar away so the pages fill the window.{Shortcut}Ctrl+H";

    /// <summary>The Split View menu item.</summary>
    public static readonly string SplitView = "Show a second view of this document beside the first, to read or compare two places at once. Choose it again to go back to one view.";

    /// <summary>The divider between the two views of a split.</summary>
    public static readonly string SplitSplitter = "Drag to share the width between the two views.";

    /// <summary>The Present menu item.</summary>
    public static readonly string Present = $"Show one page at a time on the full screen. Press Esc to stop.{Shortcut}Shift+F5";

    /// <summary>The caret navigation menu item.</summary>
    public static readonly string CaretNavigation = $"Move through the text with the arrow keys. Hold Shift to select text.{Shortcut}F7";

    /// <summary>The page by page menu item.</summary>
    public static readonly string PageByPage = "Show one page, or one pair of pages, at a time instead of scrolling through all the pages.";

    /// <summary>The Select Text tool menu item.</summary>
    public static readonly string SelectTextTool = "Drag over the pages to select text. Clicks follow links.";

    /// <summary>The Hand tool menu item.</summary>
    public static readonly string HandTool = "Drag the pages to move them, like moving paper with your hand. Hold Space or the middle mouse button to do this with any tool.";

    /// <summary>The Zoom to Area tool menu item.</summary>
    public static readonly string ZoomAreaTool = "Drag a box around part of a page to zoom in until it fills the view. Press Esc to stop.";

    /// <summary>The Auto-scroll menu item.</summary>
    public static readonly string AutoScroll = $"Scroll down by itself at a calm speed. Up and Down change the speed. Press Esc or click to stop.{Shortcut}Ctrl+Shift+H";

    /// <summary>The First Page menu item.</summary>
    public static readonly string FirstPage = $"Go to the first page. Home also works when the pages have the keyboard.{Shortcut}Ctrl+Home";

    /// <summary>The Last Page menu item.</summary>
    public static readonly string LastPage = $"Go to the last page. End also works when the pages have the keyboard.{Shortcut}Ctrl+End";

    /// <summary>The auto-scroll bar's Slower button.</summary>
    public static readonly string AutoScrollSlower = "Make auto-scroll slower. The Down arrow does the same.";

    /// <summary>The auto-scroll bar's Faster button.</summary>
    public static readonly string AutoScrollFaster = "Make auto-scroll faster. The Up arrow does the same.";

    /// <summary>The auto-scroll bar's Stop button.</summary>
    public static readonly string AutoScrollStop = "Stop scrolling by itself. Esc or a click on the pages does the same.";

    /// <summary>The Document menu button.</summary>
    public static readonly string DocumentMenu = "Open the Document menu to save a copy, reload, find the file or see its details.";

    /// <summary>The Select All menu item.</summary>
    public static readonly string SelectAll = $"Select all the text on the current page, ready to copy.{Shortcut}Ctrl+A";

    /// <summary>The Save As menu item.</summary>
    public static readonly string SaveAs = $"Save a copy of the document with a new name or in a new place.{Shortcut}Ctrl+Shift+S";

    /// <summary>The Reload menu item.</summary>
    public static readonly string Reload = $"Load the file again from the disk.{Shortcut}F5";

    /// <summary>The Show in Folder menu item.</summary>
    public static readonly string ShowInFolder = "Open the folder that holds this file.";

    /// <summary>The Properties menu item.</summary>
    public static readonly string Properties = $"See details about this document, such as its title, author and size.{Shortcut}Alt+Enter";

    /// <summary>The Tools menu button.</summary>
    public static readonly string ToolsMenu = "Open the Tools menu to measure the page, copy an image of part of it or recognise scanned text.";

    /// <summary>The Snapshot tool menu item.</summary>
    public static readonly string SnapshotTool = "Drag a box around part of a page to copy a sharp image of it. Press Esc to stop.";

    /// <summary>The Measure menu item.</summary>
    public static readonly string Measure = $"Measure distances, lengths and areas on the page.{Shortcut}Ctrl+Shift+M";

    /// <summary>The Recognise Text menu item.</summary>
    public static readonly string RecognizeText = "Find the words in scanned pages so you can search, select and hear them.";

    /// <summary>The language bar's Not Now button.</summary>
    public static readonly string CloseLanguageBar = "Hide this bar. Nothing on the pages has been changed, and any download in progress stops.";

    /// <summary>The language bar's recognise button.</summary>
    public static readonly string RecognizeInLanguage = "Find the words in this document in the chosen language. A language not on this computer downloads first and stays for next time.";

    /// <summary>The language bar's language list.</summary>
    public static readonly string DocumentLanguage = "Choose the language the scanned pages are written in.";

    /// <summary>The Preferences Stop Download button.</summary>
    public static readonly string StopLanguageDownload = "Stop the language download. Languages that already finished are kept.";

    /// <summary>A language's tick box in Preferences.</summary>
    public static readonly string UseLanguage = "Tick to find words in this language. Tick more than one for documents that mix languages.";

    /// <summary>A language's Download button in Preferences.</summary>
    public static readonly string DownloadLanguage = "Download this language and keep it on this computer, so words in it can be found.";

    /// <summary>A language's Remove button in Preferences.</summary>
    public static readonly string RemoveLanguage = "Delete this language from this computer to free space. You can download it again later.";

    /// <summary>The Recognise Text in Another Language menu item.</summary>
    public static readonly string RecognizeTextInLanguage = "Choose the language a scanned document is written in, then find its words. Other languages download in one click.";

    /// <summary>The Leave Read Mode button.</summary>
    public static readonly string LeaveReadMode = $"Bring back the tool bars and sidebar.{Shortcut}Esc or Ctrl+H";

    /// <summary>The button that keeps a measurement.</summary>
    public static readonly string MeasureKeep = "Keep the finished measurement on the page as an annotation.";

    /// <summary>The button that clears a measurement.</summary>
    public static readonly string MeasureClear = $"Clear the measurement you are making.{Shortcut}Esc";

    /// <summary>The button that closes the measuring tools.</summary>
    public static readonly string MeasureDone = "Put the measuring tools away.";

    /// <summary>The distance tool.</summary>
    public static readonly string Distance = "Click two points on the page to measure the distance between them.";

    /// <summary>The perimeter tool.</summary>
    public static readonly string Perimeter = "Click points along a path to measure its length. Double-click to finish.";

    /// <summary>The area tool.</summary>
    public static readonly string Area = "Click the corners of a shape to measure its area. Double-click to finish.";

    /// <summary>The measuring scale box.</summary>
    public static readonly string MeasureScale = "Type how the page relates to real size, such as 1 cm = 2 m or 1:100.";

    /// <summary>The annotation Undo button.</summary>
    public static readonly string Undo = $"Undo your last annotation change: adding, deleting, moving, colour, style, note or reply.{Shortcut}Ctrl+Z";

    /// <summary>The annotation Redo button.</summary>
    public static readonly string Redo = $"Make the annotation change you undid again.{Shortcut}Ctrl+Y or Ctrl+Shift+Z";

    /// <summary>The button that closes the annotation tools.</summary>
    public static readonly string AnnotateDone = "Put the annotation tools away.";

    /// <summary>The select tool.</summary>
    public static readonly string SelectTool = "Select text, or click an annotation to pick it. Drag a picked annotation or its corner squares to move or resize it; arrow keys nudge it.";

    /// <summary>The highlight tool.</summary>
    public static readonly string HighlightTool = "Select text to highlight it.";

    /// <summary>The underline tool.</summary>
    public static readonly string UnderlineTool = "Select text to underline it.";

    /// <summary>The strike out tool.</summary>
    public static readonly string StrikeTool = "Select text to draw a line through it.";

    /// <summary>The draw tool.</summary>
    public static readonly string DrawTool = "Drag on the page to draw freehand.";

    /// <summary>The note tool.</summary>
    public static readonly string NoteTool = "Click on the page to add a sticky note.";

    /// <summary>The text tool.</summary>
    public static readonly string TextTool = "Click on the page and type. Click or double-click text to change it.";

    /// <summary>The shape button.</summary>
    public static readonly string Shape = "Choose a shape, then drag on the page to draw it.";

    /// <summary>The rectangle shape.</summary>
    public static readonly string Rectangle = "Draw rectangles. Drag on the page to draw one.";

    /// <summary>The ellipse shape.</summary>
    public static readonly string Ellipse = "Draw ovals and circles. Drag on the page to draw one.";

    /// <summary>The arrow shape.</summary>
    public static readonly string Arrow = "Draw arrows. Drag on the page from the tail to the point.";

    /// <summary>The line shape.</summary>
    public static readonly string Line = "Draw straight lines. Drag on the page to draw one.";

    /// <summary>The callout tool.</summary>
    public static readonly string Callout = "Write text with an arrow pointing at something. Drag from the thing to where the text goes.";

    /// <summary>The polygon tool.</summary>
    public static readonly string Polygon = "Draw a shape with straight sides. Click each corner, then press Enter or double-click.";

    /// <summary>The cloud tool.</summary>
    public static readonly string Cloud = "Draw a cloud around an area. Click each corner, then press Enter or double-click.";

    /// <summary>The connected lines tool.</summary>
    public static readonly string PolyLine = "Draw joined straight lines. Click each point, then press Enter or double-click.";

    /// <summary>The stamp button.</summary>
    public static readonly string Stamp = "Choose a stamp, then click on the page to place it.";

    /// <summary>The Approved stamp.</summary>
    public static readonly string StampApproved = "Use the Approved stamp. Click on the page to place it.";

    /// <summary>The Reviewed stamp.</summary>
    public static readonly string StampReviewed = "Use the Reviewed stamp. Click on the page to place it.";

    /// <summary>The Draft stamp.</summary>
    public static readonly string StampDraft = "Use the Draft stamp. Click on the page to place it.";

    /// <summary>The Confidential stamp.</summary>
    public static readonly string StampConfidential = "Use the Confidential stamp. Click on the page to place it.";

    /// <summary>The Final stamp.</summary>
    public static readonly string StampFinal = "Use the Final stamp. Click on the page to place it.";

    /// <summary>The not approved stamp.</summary>
    public static readonly string StampNotApproved = "Use the Not Approved stamp. Click on the page to place it.";

    /// <summary>The custom stamp item.</summary>
    public static readonly string StampCustom = "Type your own words for a stamp, then click on the page to place it.";

    /// <summary>The picture stamp item.</summary>
    public static readonly string StampPicture = "Choose a picture file to use as a stamp, then click on the page to place it.";

    /// <summary>The line width button.</summary>
    public static readonly string LineWidth = "Choose how thick new drawings, lines and shapes are, and the one you picked.";

    /// <summary>The thin line width.</summary>
    public static readonly string LineThin = "Use thin lines for new drawings and shapes and the one you picked.";

    /// <summary>The medium line width.</summary>
    public static readonly string LineMedium = "Use medium lines for new drawings and shapes and the one you picked.";

    /// <summary>The thick line width.</summary>
    public static readonly string LineThick = "Use thick lines for new drawings and shapes and the one you picked.";

    /// <summary>The text size button.</summary>
    public static readonly string AnnotationTextSize = "Choose the size of new text and callouts, and of the text you picked.";

    /// <summary>The small text size.</summary>
    public static readonly string TextSmall = "Use small text for new text and callouts and the one you picked.";

    /// <summary>The medium text size.</summary>
    public static readonly string TextMedium = "Use medium text for new text and callouts and the one you picked.";

    /// <summary>The large text size.</summary>
    public static readonly string TextLarge = "Use large text for new text and callouts and the one you picked.";

    /// <summary>The extra large text size.</summary>
    public static readonly string TextExtraLarge = "Use extra large text for new text and callouts and the one you picked.";

    /// <summary>The annotation colour button.</summary>
    public static readonly string AnnotationColour = "Choose the colour for new annotations, drawings and text, and for the annotation you picked.";

    /// <summary>The yellow annotation colour.</summary>
    public static readonly string Yellow = "Use yellow for new annotations and the one you picked.";

    /// <summary>The green annotation colour.</summary>
    public static readonly string Green = "Use green for new annotations and the one you picked.";

    /// <summary>The blue annotation colour.</summary>
    public static readonly string Blue = "Use blue for new annotations and the one you picked.";

    /// <summary>The red annotation colour.</summary>
    public static readonly string Red = "Use red for new annotations and the one you picked.";

    /// <summary>The purple annotation colour.</summary>
    public static readonly string Purple = "Use purple for new annotations and the one you picked.";

    /// <summary>The orange annotation colour.</summary>
    public static readonly string Orange = "Use orange for new annotations and the one you picked.";

    /// <summary>The grey annotation colour.</summary>
    public static readonly string Grey = "Use grey for new annotations and the one you picked.";

    /// <summary>The dark blue annotation colour.</summary>
    public static readonly string DarkBlue = "Use dark blue for new annotations and the one you picked.";

    /// <summary>The comment list's text filter.</summary>
    public static readonly string AnnotationFilterText = "Type words to show only the comments that contain them.";

    /// <summary>The button that shows the comment list's filters and sort order.</summary>
    public static readonly string AnnotationFilters = "Show or hide the choices that filter and sort the comment list.";

    /// <summary>The comment list's type filter.</summary>
    public static readonly string AnnotationTypeFilter = "Show only one type of comment, such as notes or drawings.";

    /// <summary>The comment list's colour filter.</summary>
    public static readonly string AnnotationColourFilter = "Show only the comments of one colour.";

    /// <summary>The comment list's author filter.</summary>
    public static readonly string AnnotationAuthorFilter = "Show only the comments one person wrote.";

    /// <summary>The comment list's sort order.</summary>
    public static readonly string AnnotationSort = "Choose the order of the comment list: by page, newest first, by author or by type.";

    /// <summary>The button that clears the comment list's filters.</summary>
    public static readonly string ClearAnnotationFilter = "Show every comment again.";

    /// <summary>The button that closes the Fill and Sign tools.</summary>
    public static readonly string FillSignDone = "Put the Fill & Sign tools away.";

    /// <summary>The Sign with Certificate button.</summary>
    public static readonly string CertificateSign = "Sign a copy of the document with your digital certificate, a .p12 or .pfx file.";

    /// <summary>The Check Signatures button.</summary>
    public static readonly string CheckSignatures = "Check who signed this document and whether it changed after signing. This may go online.";

    /// <summary>The button that stops text recognition.</summary>
    public static readonly string StopRecognition = "Stop finding the words in scanned pages.";

    /// <summary>The button that closes Read Aloud.</summary>
    public static readonly string CloseReadAloud = $"Stop reading and close the Read Aloud bar.{Shortcut}Ctrl+Shift+Y";

    /// <summary>The previous sentence button.</summary>
    public static readonly string PreviousSentence = "Go back one sentence and read it again.";

    /// <summary>The Play button.</summary>
    public static readonly string Play = "Carry on reading from where it stopped.";

    /// <summary>The Pause button.</summary>
    public static readonly string Pause = "Pause reading. Your place is kept.";

    /// <summary>The next sentence button.</summary>
    public static readonly string NextSentence = "Skip to the next sentence.";

    /// <summary>The voice list.</summary>
    public static readonly string Voice = "Choose the voice that reads aloud.";

    /// <summary>The speed list.</summary>
    public static readonly string Speed = "Choose how fast the voice reads.";

    /// <summary>The Read Aloud options button.</summary>
    public static readonly string ReadAloudOptions = "Choose how the text being read is marked on the page.";

    /// <summary>The check box that marks each word as it is read.</summary>
    public static readonly string WordMark = "Mark each word as it is read aloud.";

    /// <summary>The check box that dims text away from what is read.</summary>
    public static readonly string FocusBand = "Dim the text away from the part being read aloud.";

    /// <summary>The Download Voice button.</summary>
    public static readonly string DownloadVoice = "Download this voice so it can read aloud on this computer.";

    /// <summary>A button that hides a message.</summary>
    public static readonly string DismissMessage = "Hide this message.";

    /// <summary>The Dismiss button on the message about content that cannot be shown.</summary>
    public static readonly string DismissContentWarning = "Hide this message about parts of the document HyperPDF cannot show. It comes back next time the document opens.";

    /// <summary>The Dismiss button on the message about a repaired file.</summary>
    public static readonly string DismissRepairNotice = "Hide this message about the repaired file. It comes back next time the document opens.";

    /// <summary>The Details button on the message about a repaired file.</summary>
    public static readonly string ToggleRepairDetails = "Show or hide what was wrong with the file and how it was repaired.";

    /// <summary>The Reload button on the changed file bar.</summary>
    public static readonly string ReloadChanged = "Load the changed file from the disk.";

    /// <summary>The Dismiss button on the changed file bar.</summary>
    public static readonly string DismissReload = "Keep what is on screen now and hide this message.";

    /// <summary>The find box.</summary>
    public static readonly string SearchBox = "Type the words to find. Press Enter for the next result or Shift+Enter for the one before.";

    /// <summary>The match case option.</summary>
    public static readonly string MatchCase = "Only find text with the same capital and small letters.";

    /// <summary>The whole words option.</summary>
    public static readonly string WholeWords = "Only find whole words, not parts of longer words.";

    /// <summary>The previous result button.</summary>
    public static readonly string PreviousResult = $"Go to the previous result.{Shortcut}Shift+F3";

    /// <summary>The next result button.</summary>
    public static readonly string NextResult = $"Go to the next result.{Shortcut}F3";

    /// <summary>The button that closes the find bar.</summary>
    public static readonly string CloseFind = $"Close the find bar.{Shortcut}Esc";

    /// <summary>The thumbnails sidebar tab.</summary>
    public static readonly string Thumbnails = "Show a small picture of each page. Choose one to go to that page.";

    /// <summary>The outline sidebar tab.</summary>
    public static readonly string Outline = "Show the document's headings. Choose one to go to it.";

    /// <summary>The search results sidebar tab.</summary>
    public static readonly string SearchResults = "Show every place your search found. Choose one to go to it.";

    /// <summary>The annotations sidebar tab.</summary>
    public static readonly string Annotations = "Show the highlights, notes and other annotations in this document.";

    /// <summary>The attachments sidebar tab.</summary>
    public static readonly string Attachments = "Show the files attached to this document.";

    /// <summary>The layers sidebar tab.</summary>
    public static readonly string Layers = "Show the document's layers so you can hide or show them.";

    /// <summary>The Save Attachment button.</summary>
    public static readonly string SaveAttachment = "Save the chosen attachment as a file.";

    /// <summary>The Open Attachment button.</summary>
    public static readonly string OpenAttachment = "Open the chosen attached file. A PDF opens in a new tab; other files open in their own app after you agree. Programs are never started.";

    /// <summary>The splitter beside the sidebar.</summary>
    public static readonly string SidebarSplitter = "Drag to make the sidebar wider or narrower.";

    /// <summary>A layer's check box.</summary>
    public static readonly string Layer = "Show or hide this layer on screen. The file does not change.";

    /// <summary>The form field editor.</summary>
    public static readonly string FormField = "Type into this form field. Press Tab for the next field or Esc to cancel.";

    /// <summary>The document password box.</summary>
    public static readonly string DocumentPassword = "Type the document's password, then press Enter.";

    /// <summary>The Unlock button.</summary>
    public static readonly string Unlock = "Open the document with this password.";

    /// <summary>The Back to Pages button in Focus Mode.</summary>
    public static readonly string BackToPages = $"Leave Focus Mode and show the pages again.{Shortcut}Ctrl+4";

    /// <summary>The smaller text button in Focus Mode.</summary>
    public static readonly string SmallerText = "Make the text smaller.";

    /// <summary>The larger text button in Focus Mode.</summary>
    public static readonly string LargerText = "Make the text larger.";

    /// <summary>The Text Settings button in Focus Mode.</summary>
    public static readonly string TextSettings = "Change the text size, spacing, width, colour and typeface.";

    /// <summary>The text size slider.</summary>
    public static readonly string TextSize = "Change the size of the text.";

    /// <summary>The line spacing slider.</summary>
    public static readonly string LineSpacing = "Change the space between lines.";

    /// <summary>The paragraph spacing slider.</summary>
    public static readonly string ParagraphSpacing = "Change the space between paragraphs.";

    /// <summary>The text width slider.</summary>
    public static readonly string TextWidth = "Change how wide the column of text is.";

    /// <summary>The Focus Mode page colour list.</summary>
    public static readonly string FocusPageColour = "Choose the colour behind the text.";

    /// <summary>The typeface list.</summary>
    public static readonly string Typeface = "Choose the typeface for the text.";

    /// <summary>The tab finder button.</summary>
    public static readonly string TabFinder = $"Find an open tab by part of its name, title or folder.{Shortcut}Ctrl+Shift+A";

    /// <summary>The tab finder box.</summary>
    public static readonly string TabFinderBox = "Type part of a name, title or folder. Press Enter to go to the first tab found.";

    /// <summary>The Open button and menu item.</summary>
    public static readonly string Open = $"Choose PDF files to open.{Shortcut}Ctrl+O";

    /// <summary>The Open Recent menu.</summary>
    public static readonly string OpenRecent = "Open a document you had open recently. It opens at the page you left it on.";

    /// <summary>The New Window menu item.</summary>
    public static readonly string NewWindow = $"Open another window showing this document at the same page, to read two places side by side.{Shortcut}Ctrl+N";

    /// <summary>The tabs and settings menu button.</summary>
    public static readonly string TabsMenu = "Open the menu for tabs and settings, such as Preferences, page colour and closed tabs.";

    /// <summary>The Reopen Closed Tabs menu item.</summary>
    public static readonly string ReopenClosedTabs = $"Open the tabs you closed again.{Shortcut}Ctrl+Shift+T";

    /// <summary>The Search in Folder menu item.</summary>
    public static readonly string SearchFolder = $"Search for words in every PDF in a folder.{Shortcut}Ctrl+Shift+F";

    /// <summary>The comfort page colour menu item.</summary>
    public static readonly string ComfortPageColour = $"Show the pages in a softer colour that is easier on the eyes.{Shortcut}Ctrl+I";

    /// <summary>The About and Licences menu item.</summary>
    public static readonly string Licences = "Read the licence for this app and for every component it includes or downloads.";

    /// <summary>The search box of the Licences window.</summary>
    public static readonly string LicenceSearch = "Type part of a name, a licence such as MIT, or where a component comes from, to narrow the list.";

    /// <summary>The licence tree of the Licences window.</summary>
    public static readonly string LicenceTree = "Choose a component to read its licence. Components are grouped by licence. Use the arrow keys to move.";

    /// <summary>The licence text box of the Licences window.</summary>
    public static readonly string LicenceText = "The full licence text of the chosen component. You can select and copy parts of it.";

    /// <summary>The Copy button of the Licences window.</summary>
    public static readonly string CopyLicence = "Copy the full licence text of the chosen component to the clipboard.";

    /// <summary>The Preferences menu item.</summary>
    public static readonly string Preferences = $"Change how the app looks and works.{Shortcut}Ctrl+Comma";

    /// <summary>The Close All Tabs menu item.</summary>
    public static readonly string CloseAllTabs = "Close every open tab.";

    /// <summary>The close button on a tab.</summary>
    public static readonly string CloseTab = $"Close this tab.{Shortcut}Ctrl+W";

    /// <summary>The colour scheme list.</summary>
    public static readonly string ColourScheme = "Choose the colours of the app, such as Calm, Dark or High contrast.";

    /// <summary>The PDF page colour list.</summary>
    public static readonly string PageColour = "Choose the colour of the pages on screen. Printed pages keep their own colours.";

    /// <summary>The tool bar style list.</summary>
    public static readonly string ToolbarStyle = "Choose whether tool bar buttons show their names beside their icons.";

    /// <summary>The file change list.</summary>
    public static readonly string FileChange = "Choose what happens when an open file is changed by another program.";

    /// <summary>The motion list.</summary>
    public static readonly string Motion = "Choose whether the app uses movement, such as smooth scrolling.";

    /// <summary>The text cursor list.</summary>
    public static readonly string TextCursor = "Choose whether the text cursor stays steady or blinks.";

    /// <summary>The interface text size list.</summary>
    public static readonly string InterfaceTextSize = "Choose the size of the text in the app's buttons, menus and lists.";

    /// <summary>The opening zoom list.</summary>
    public static readonly string OpeningZoom = "Choose the zoom for new documents. Fit page shows the whole page. Fit width fills the width.";

    /// <summary>The help for the reopen at last page choice.</summary>
    public static readonly string ReopenAtLastPage = "When ticked, a document you open again starts at the page you last read in it.";

    /// <summary>The help for the spelling check choice.</summary>
    public static readonly string CheckSpelling = "When ticked, misspelled words in form fields are underlined. Right-click one for corrections, from this computer's own dictionary.";

    /// <summary>The comment author box.</summary>
    public static readonly string CommentAuthor = "Type the name shown as the author of your notes, replies and other annotations. Leave it empty to use your user name.";

    /// <summary>The Read Aloud voice service list.</summary>
    public static readonly string SpeechEngine = "Choose which voice service reads aloud.";

    /// <summary>The Azure Speech key box.</summary>
    public static readonly string AzureKey = "Type a key from your Azure Speech resource. Azure voices need it.";

    /// <summary>The Azure region box.</summary>
    public static readonly string AzureRegion = "Type the region of your Azure Speech resource, such as uksouth.";

    /// <summary>The timestamp server box.</summary>
    public static readonly string TimestampServer = "Optional. Type the address of a server that records a trusted time when you sign.";

    /// <summary>A Close button on a window that has nothing to save.</summary>
    public static readonly string CloseWindow = $"Close this window.{Shortcut}Esc";

    /// <summary>The Close button on Preferences.</summary>
    public static readonly string ClosePreferences = $"Close Preferences. Your changes are already kept.{Shortcut}Esc";

    /// <summary>A Cancel button.</summary>
    public static readonly string Cancel = $"Close this window without making any changes.{Shortcut}Esc";

    /// <summary>The print destination list.</summary>
    public static readonly string PrintDestination = "Choose a printer, save as a PDF file or use the system print dialog.";

    /// <summary>The pages to print list.</summary>
    public static readonly string PrintPages = "Choose which pages to print: all, the current page or your own list.";

    /// <summary>The page range box.</summary>
    public static readonly string PageRange = "Type the pages to print, such as 1-3, 7.";

    /// <summary>The copies box.</summary>
    public static readonly string Copies = "Choose how many copies to print.";

    /// <summary>The print colour list.</summary>
    public static readonly string PrintColour = "Choose to print in colour or in black and white.";

    /// <summary>The two-sided printing check box.</summary>
    public static readonly string TwoSided = "Print on both sides of the paper.";

    /// <summary>The page turning edge list.</summary>
    public static readonly string TurnPages = "Choose which edge the paper turns on when printing on both sides.";

    /// <summary>The print layout list.</summary>
    public static readonly string PrintLayout = "Choose normal pages, a folded booklet or a poster made from several sheets.";

    /// <summary>The pages per sheet list.</summary>
    public static readonly string PagesPerSheet = "Choose how many pages to fit on each sheet of paper.";

    /// <summary>The paper size list.</summary>
    public static readonly string PaperSize = "Choose the size of the paper.";

    /// <summary>The print page size (scaling) choice.</summary>
    public static readonly string PrintScaling = "Choose how big each page prints: filling the paper, at its true size, shrunk only when too big, or at a chosen scale.";

    /// <summary>The custom print scale.</summary>
    public static readonly string PrintScalePercent = "Set the scale as a percentage of the page's true size, from 10 to 400.";

    /// <summary>The print annotations check box.</summary>
    public static readonly string PrintAnnotations = "Print the highlights, notes and other annotations too.";

    /// <summary>The print button in the print window.</summary>
    public static readonly string PrintNext = "Go on to print with these settings.";

    /// <summary>The system print dialog button.</summary>
    public static readonly string SystemPrintDialog = $"Print with your computer's own print window instead.{Shortcut}Ctrl+Shift+P";

    /// <summary>The certificate file box.</summary>
    public static readonly string CertificateFile = "Type where your certificate file is, a .p12 or .pfx file.";

    /// <summary>The button that chooses a certificate file.</summary>
    public static readonly string ChooseCertificate = "Choose your certificate file from a folder.";

    /// <summary>The certificate password box.</summary>
    public static readonly string CertificatePassword = "Type the password for your certificate. It is never saved.";

    /// <summary>The list of remembered certificates.</summary>
    public static readonly string RememberedCertificates = "Pick a certificate you asked this computer to remember.";

    /// <summary>The button that forgets a remembered certificate.</summary>
    public static readonly string ForgetCertificate = "Forget the picked certificate. The certificate file itself is not changed.";

    /// <summary>The check box that remembers a certificate.</summary>
    public static readonly string RememberCertificate = "Offer this certificate next time. Only where the file is and the name in it are kept, never the password.";

    /// <summary>The Signature button in Fill and Sign.</summary>
    public static readonly string SignatureMark = "Type, draw or choose a picture of your signature, then place it on the page.";

    /// <summary>The Initials button in Fill and Sign.</summary>
    public static readonly string InitialsMark = "Type, draw or choose a picture of your initials, then place them on the page.";

    /// <summary>The button that places the mark being previewed.</summary>
    public static readonly string PlaceMark = $"Put the signature on the page where the preview is.{Shortcut}Enter";

    /// <summary>The button that stops placing a mark.</summary>
    public static readonly string CancelPlacement = $"Stop placing the signature. The document is not changed.{Shortcut}Escape";

    /// <summary>The button that makes the mark being placed bigger.</summary>
    public static readonly string BiggerMark = $"Make the signature being placed bigger.{Shortcut}+";

    /// <summary>The button that makes the mark being placed smaller.</summary>
    public static readonly string SmallerMark = $"Make the signature being placed smaller.{Shortcut}-";

    /// <summary>The button that picks up the last placed mark again.</summary>
    public static readonly string AdjustMark = "Pick up the signature you just placed so you can move or resize it again.";

    /// <summary>The button that removes the last placed mark.</summary>
    public static readonly string RemoveMark = "Take the signature you just placed off the page.";

    /// <summary>The Type option when making a signature.</summary>
    public static readonly string MarkType = "Make it by typing. This works with only a keyboard.";

    /// <summary>The Draw option when making a signature.</summary>
    public static readonly string MarkDraw = "Make it by drawing with a mouse, pen or finger.";

    /// <summary>The Image option when making a signature.</summary>
    public static readonly string MarkImage = "Make it from a picture, such as a photo or scan of your signature.";

    /// <summary>The text box when typing a signature.</summary>
    public static readonly string MarkText = "Type it as you want it to appear.";

    /// <summary>The drawing area when drawing a signature.</summary>
    public static readonly string MarkPad = "Draw here. Each line you draw is added until you clear it.";

    /// <summary>The button that clears a drawn signature.</summary>
    public static readonly string ClearDrawing = "Rub out the drawing and start again.";

    /// <summary>The button that chooses a picture of a signature.</summary>
    public static readonly string ChooseSignatureImage = "Choose a picture of your signature from a folder.";

    /// <summary>The check box that removes white paper from a picture.</summary>
    public static readonly string RemovePaper = "Make white paper see-through so only the ink shows. Untick to keep the picture as it is.";

    /// <summary>The check box that remembers a signature.</summary>
    public static readonly string RememberMark = "Keep it on this computer to use again. Untick to forget it after this time.";

    /// <summary>The button that forgets a remembered signature.</summary>
    public static readonly string ForgetMark = "Forget the remembered one now. Signatures already placed are not changed.";

    /// <summary>The button that uses the signature that was made.</summary>
    public static readonly string UseMark = "Use this, then choose where it goes on the page.";

    /// <summary>The signing reason box.</summary>
    public static readonly string SigningReason = "Optional. Type why you are signing, such as Approved.";

    /// <summary>The signing location box.</summary>
    public static readonly string SigningLocation = "Optional. Type where you are signing.";

    /// <summary>The Sign button.</summary>
    public static readonly string Sign = "Choose where to save the signed copy, then sign it.";

    /// <summary>The confirm button in a question window.</summary>
    public static readonly string Confirm = "Go ahead with what this window asks about.";

    /// <summary>The text box in a question window.</summary>
    public static readonly string PromptInput = "Type your answer here.";

    /// <summary>The confirm button in a question window with a text box.</summary>
    public static readonly string PromptConfirm = "Use what you typed.";

    /// <summary>The choose folder button.</summary>
    public static readonly string ChooseFolder = "Choose the folder to search.";

    /// <summary>The folder box.</summary>
    public static readonly string FolderBox = "Type the folder to search.";

    /// <summary>The words to find in a folder.</summary>
    public static readonly string FolderQuery = "Type the words to find, then press Enter.";

    /// <summary>The Search button in folder search.</summary>
    public static readonly string SearchFolderStart = "Search every PDF in the folder for the words.";

    /// <summary>The Stop button in folder search.</summary>
    public static readonly string StopSearch = "Stop searching the folder.";

    /// <summary>The include subfolders check box.</summary>
    public static readonly string Subfolders = "Also search the folders inside this folder.";

    /// <summary>The pages, read out by screen readers only so hovering the pages shows no tooltip.</summary>
    public static readonly string Pages = "The document's pages. Page Up and Page Down move a page at a time. F7 turns on a text cursor for the arrow keys. The value is the current page's text.";

    /// <summary>The text typed on the page.</summary>
    public static readonly string PageText = $"Type here. Enter starts a new line, a click elsewhere keeps the text and Escape cancels.{Shortcut}Ctrl+Enter";

    /// <summary>The font family box.</summary>
    public static readonly string FontFamily = "Choose the font of the text you type, or of the text you picked. Each font is shown in its own letters.";

    /// <summary>The font size box.</summary>
    public static readonly string FontSize = "Choose or type the text size in points.";

    /// <summary>The smaller text button.</summary>
    public static readonly string ShrinkPageText = "Make the text one point smaller.";

    /// <summary>The larger text button.</summary>
    public static readonly string GrowPageText = "Make the text one point larger.";

    /// <summary>The bold button.</summary>
    public static readonly string BoldText = "Make the text bold, or not. Shortcut: Ctrl+B while typing.";

    /// <summary>The italic button.</summary>
    public static readonly string ItalicText = "Make the text italic, or not. Shortcut: Ctrl+I while typing.";

    /// <summary>The underline button.</summary>
    public static readonly string UnderlineText = "Underline the text, or not. Shortcut: Ctrl+U while typing.";

    /// <summary>The align left button.</summary>
    public static readonly string AlignLeft = "Start each line at the left of the text box.";

    /// <summary>The centre button.</summary>
    public static readonly string AlignCenter = "Centre each line in the text box.";

    /// <summary>The align right button.</summary>
    public static readonly string AlignRight = "End each line at the right of the text box.";

    /// <summary>The text colour button.</summary>
    public static readonly string TextColour = "Choose the colour of the text you type, or of the text you picked.";

    /// <summary>The spacing button.</summary>
    public static readonly string TextSpacing = "Choose the space between lines and between letters.";

    /// <summary>The text properties button.</summary>
    public static readonly string TextProperties = "Set every text setting with exact numbers, including comb boxes and the wrap width.";

    /// <summary>The finish text button.</summary>
    public static readonly string FinishText = "Keep the text you are typing. Shortcut: Ctrl+Enter.";

    /// <summary>The add text tool in Fill and Sign.</summary>
    public static readonly string AddText = "Click anywhere on the page and type, as on paper. Click a form field to fill it in.";

    /// <summary>The text box in the text properties window.</summary>
    public static readonly string TextPropertiesText = "The text. Enter starts a new line.";

    /// <summary>The alignment box.</summary>
    public static readonly string TextAlignment = "Choose where lines sit across the text box.";

    /// <summary>The wrap width box.</summary>
    public static readonly string WrapWidth = "The width lines wrap at, in points. 0 lets the box grow as you type.";

    /// <summary>The comb boxes box.</summary>
    public static readonly string CombBoxes = "Put one letter in each of this many evenly spaced boxes, as on forms with a box per letter. 0 writes running text.";

    /// <summary>The apply button of the text properties window.</summary>
    public static readonly string ApplyTextProperties = "Use these settings for the text.";

    /// <summary>The preview only fonts preference.</summary>
    public static readonly string PreviewOnlyFonts =
        "Some fonts' licences forbid copying them into documents. When this is on, you can type in them on screen; the saved PDF uses the closest built in font, so the font is never copied.";

    /// <summary>The Save Optimised Copy menu item.</summary>
    public static readonly string OptimizeCopy =
        "Save a smaller or more accessible copy of this document. Your open file is never changed.";

    /// <summary>The size choices of the optimised copy window.</summary>
    public static readonly string OptimizePresets = "Choose how much picture detail may be traded for a smaller file. Use the arrow keys to move between the choices.";

    /// <summary>The fill in accessibility box of the optimised copy window.</summary>
    public static readonly string OptimizeAccessibility = "Add the document language, a window title and tag flags when they are missing, so screen readers read the file correctly.";

    /// <summary>The language box of the optimised copy window.</summary>
    public static readonly string OptimizeLanguage = "The language of the text, such as en-AU. It is used only when the document does not say.";

    /// <summary>The add tags box of the optimised copy window.</summary>
    public static readonly string OptimizeTags = "Guess headings, paragraphs and pictures from the page layout when the document has no reading structure. The guess can be wrong, so check it.";

    /// <summary>The clean up box of the optimised copy window.</summary>
    public static readonly string OptimizeCleanUp = "Remove page thumbnails, private application data, unused resources and empty comment lists. Pages look the same.";

    /// <summary>The progress bar of the optimised copy window.</summary>
    public static readonly string OptimizeProgress = "Shows how much of the copy is written.";

    /// <summary>The save button of the optimised copy window.</summary>
    public static readonly string OptimizeSave = "Choose where to save the copy, then write it. The open file is never replaced.";

    /// <summary>The cancel button of the optimised copy window.</summary>
    public static readonly string OptimizeCancel = "Stop writing the copy. No file is saved.";

    /// <summary>The summary of the optimised copy window.</summary>
    public static readonly string OptimizeResult = "What the saved copy changed, what was left alone and what needs your attention.";

    /// <summary>The mark area to redact menu item.</summary>
    public static readonly string RedactArea =
        "Drag a box on the page to mark an area to redact. Nothing is removed until you apply the redactions. Only the HyperPDF engine can do this.";

    /// <summary>The mark text to redact menu item.</summary>
    public static readonly string RedactText = "Select text to mark it to redact. Nothing is removed until you apply the redactions. Only the HyperPDF engine can do this.";

    /// <summary>The apply redactions menu item.</summary>
    public static readonly string ApplyRedactions =
        "Remove everything under the marks for good and save a redacted copy. Your open file is never changed.";

    /// <summary>The pictures choices of the redaction window.</summary>
    public static readonly string RedactImages = "Choose what happens to a picture that a mark touches. Use the arrow keys to move between the choices.";

    /// <summary>The drawings choices of the redaction window.</summary>
    public static readonly string RedactLineArt = "Choose what happens to a line or shape that a mark touches. Use the arrow keys to move between the choices.";

    /// <summary>The hidden text box of the redaction window.</summary>
    public static readonly string RedactHiddenText = "Also remove invisible text under a mark, such as the text layer of a scanned page, so it cannot be searched or copied.";

    /// <summary>The links box of the redaction window.</summary>
    public static readonly string RedactLinks = "Also remove links, comments and form fields under a mark, with their actions and appearances.";

    /// <summary>The metadata box of the redaction window.</summary>
    public static readonly string RedactMetadata = "Also remove the title, author and other document information, and the metadata stream.";

    /// <summary>The confirmation box of the redaction window.</summary>
    public static readonly string RedactConfirm = "Tick this to show you understand that removed content cannot be brought back after the redacted copy is saved.";

    /// <summary>The progress bar of the redaction window.</summary>
    public static readonly string RedactProgress = "Moves while the redactions are applied and the copy is written.";

    /// <summary>The apply button of the redaction window.</summary>
    public static readonly string RedactApply = "Choose where to save the redacted copy, then apply the redactions and write it. The open file is never replaced.";

    /// <summary>The cancel button of the redaction window.</summary>
    public static readonly string RedactCancel = "Stop applying the redactions. No file is saved.";

    /// <summary>The summary of the redaction window.</summary>
    public static readonly string RedactResult = "What was removed, where the redacted copy is saved, and what to check next.";

    /// <summary>Joins an explanation to its shortcut.</summary>
    private const string Shortcut = " Shortcut: ";

    /// <summary>Gets the button help text for opening a certificate-encrypted PDF.</summary>
    public static string OpenCertificateDocument => "Open this document with the certificate and private key you chose.";
}

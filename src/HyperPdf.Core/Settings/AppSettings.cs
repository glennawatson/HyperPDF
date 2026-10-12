// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json.Serialization;
using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Settings;

/// <summary>User preferences persisted between runs.</summary>
[DebuggerDisplay("AppSettings: {ColorScheme}, {PageTone}, cache {TileCacheMegabytes} MB")]
public sealed class AppSettings
{
    /// <summary>Gets or sets the colour scheme.</summary>
    public ColorSchemeChoice ColorScheme { get; set; }

    /// <summary>Gets or sets how pages are coloured.</summary>
    public PageToneChoice PageTone { get; set; }

    /// <summary>Gets or sets a value indicating whether the page tone is applied (Ctrl+I toggles it off for true colours).</summary>
    public bool PageToneEnabled { get; set; } = true;

    /// <summary>Gets or sets how tool bar buttons are labelled.</summary>
    public ToolbarStyle ToolbarStyle { get; set; }

    /// <summary>Gets or sets what happens when an open file changes on disk.</summary>
    public FileChangeAction FileChangeAction { get; set; }

    /// <summary>Gets or sets whether interface transitions play.</summary>
    public MotionPreference Motion { get; set; }

    /// <summary>Gets or sets whether the text cursor blinks.</summary>
    public CaretPreference Caret { get; set; }

    /// <summary>Gets or sets the interface font size in points, or <see langword="null"/> to follow the desktop.</summary>
    public double? InterfaceFontSizePoints { get; set; }

    /// <summary>Gets or sets the name recorded as the author of new comments, or empty to use the computer's user name.</summary>
    public string CommentAuthor { get; set; } = string.Empty;

    /// <summary>
    /// Gets the signing certificates the user chose to remember, most recently used first. Only the file and who it
    /// names are kept; passwords and private keys are never saved.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<RememberedCertificate> RememberedCertificates { get; } = [];

    /// <summary>Gets or sets the Tesseract languages used to recognise text, for example <c>eng</c> or <c>eng+deu</c>.</summary>
    public string OcrLanguage { get; set; } = "eng";

    /// <summary>Gets or sets the colour of the tint over fillable form fields, as 0xRRGGBB.</summary>
    public uint FormHighlightColor { get; set; } = 0xB4CCDCU;

    /// <summary>Gets or sets the opacity of the tint over fillable form fields, 0 (off) to 255.</summary>
    public int FormHighlightAlpha { get; set; } = 72;

    /// <summary>Gets or sets which voice reads documents aloud.</summary>
    public SpeechEngineChoice SpeechEngine { get; set; }

    /// <summary>Gets or sets the voice used to read aloud, or empty for the engine's first voice.</summary>
    public string SpeechVoice { get; set; } = string.Empty;

    /// <summary>Gets or sets how fast documents are read aloud, 1 for normal.</summary>
    public double SpeechSpeed { get; set; } = 1;

    /// <summary>Gets or sets the person's Azure Speech key. The settings file is readable only by its owner.</summary>
    public string AzureSpeechKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the person's Azure Speech region, for example <c>uksouth</c>.</summary>
    public string AzureSpeechRegion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RFC 3161 timestamp server used when signing with a certificate, for example
    /// <c>http://timestamp.digicert.com</c>; empty signs without a trusted timestamp.
    /// </summary>
    public string TimestampServer { get; set; } = string.Empty;

    /// <summary>Gets or sets how much of the text being read aloud is marked.</summary>
    public ReadAloudHighlight ReadAloudHighlight { get; set; }

    /// <summary>Gets or sets a value indicating whether text away from what is being read is dimmed.</summary>
    public bool FocusBand { get; set; }

    /// <summary>Gets or sets Focus Mode's text size in points.</summary>
    public double FocusFontSize { get; set; } = 17;

    /// <summary>Gets or sets Focus Mode's line spacing, as a multiple of the text size.</summary>
    public double FocusLineSpacing { get; set; } = 1.6;

    /// <summary>Gets or sets Focus Mode's space between paragraphs, as a multiple of the text size.</summary>
    public double FocusParagraphSpacing { get; set; } = 1;

    /// <summary>Gets or sets Focus Mode's text column width, in characters.</summary>
    public int FocusTextWidth { get; set; } = 66;

    /// <summary>Gets or sets Focus Mode's page colour.</summary>
    public FocusPageColour FocusPageColour { get; set; }

    /// <summary>Gets or sets Focus Mode's typeface.</summary>
    public FocusFont FocusFont { get; set; }

    /// <summary>Gets where reading aloud last stopped in each document, by file path.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, ReadingPosition> ReadingPositions { get; } = [];

    /// <summary>Gets or sets a value indicating whether the sidebar is shown.</summary>
    public bool ShowSidebar { get; set; } = true;

    /// <summary>Gets or sets the zoom mode for newly opened documents.</summary>
    public ZoomMode DefaultZoomMode { get; set; } = ZoomMode.FitPage;

    /// <summary>Gets or sets the page arrangement for newly opened documents.</summary>
    public PageLayoutMode DefaultLayoutMode { get; set; } = PageLayoutMode.Single;

    /// <summary>Gets or sets the rendered tile cache budget in megabytes.</summary>
    public int TileCacheMegabytes { get; set; } = 256;

    /// <summary>Gets or sets how many documents stay open in memory; others are reopened when their tab is selected.</summary>
    public int MaxOpenDocuments { get; set; } = 16;

    /// <summary>Gets or sets a value indicating whether tabs are restored on start.</summary>
    public bool RestoreSession { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether words typed into form fields are checked for spelling.</summary>
    public bool CheckSpelling { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether fonts that cannot be saved in a PDF, by their licence or outlines, are
    /// offered for text. Text in them shows on screen in that font and is saved in the closest built in font.
    /// </summary>
    public bool ShowPreviewOnlyFonts { get; set; }

    /// <summary>Gets the words the reader chose to keep, which are never marked as misspelled.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<string> IgnoredWords { get; } = [];

    /// <summary>Gets or sets a value indicating whether a document reopens at the page it was closed at.</summary>
    public bool ReopenAtLastPage { get; set; } = true;

    /// <summary>Gets the page each recent document was closed at, newest last.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<LastViewedPage> LastPages { get; } = [];

    /// <summary>Gets or sets the window width.</summary>
    public double WindowWidth { get; set; } = 1200;

    /// <summary>Gets or sets the window height.</summary>
    public double WindowHeight { get; set; } = 850;

    /// <summary>Gets or sets a value indicating whether the window is maximised.</summary>
    public bool WindowMaximized { get; set; }

    /// <summary>Gets the tabs open at exit.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<SessionTab> Session { get; } = [];
}

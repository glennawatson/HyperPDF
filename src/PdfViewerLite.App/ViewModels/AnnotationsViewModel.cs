// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Annotation state for one tab: the active tool, colour, line width and text size, the sidebar list, undo and redo,
/// and the edits themselves. Edits go straight to the document; the page's tiles are dropped so it redraws.
/// </summary>
[DebuggerDisplay("AnnotationsViewModel: {Tool}, {Items.Count} annotations")]
public sealed partial class AnnotationsViewModel : ReactiveObject, IDisposable
{
    /// <summary>The coordinates stored for each drawn signature point.</summary>
    private const int PointCoordinates = 2;

    /// <summary>The line width of signatures and kept measurements, in points.</summary>
    private const float InkWidth = 1.5F;

    /// <summary>The size of a kept measurement's label.</summary>
    private const float MeasurementTextSize = 9;

    /// <summary>The title, label and button of the reply prompt.</summary>
    private const string ReplyTitle = "Reply";

    /// <summary>How far outside an annotation a click still picks it, in points.</summary>
    private const float HitTolerance = 3;

    /// <summary>The longest side of a picture stamp when placed, in points.</summary>
    private const float PictureStampSide = 144;

    /// <summary>The label of the stamp button while a picture stamp is chosen.</summary>
    private const string PictureStampLabel = "Picture";

    /// <summary>The thin line width in points.</summary>
    private const float ThinLine = 1;

    /// <summary>The medium line width in points, the default.</summary>
    private const float MediumLine = 2;

    /// <summary>The thick line width in points.</summary>
    private const float ThickLine = 4;

    /// <summary>The small text size in points.</summary>
    private const float SmallText = 10;

    /// <summary>The medium text size in points, the default.</summary>
    private const float MediumText = 12;

    /// <summary>The large text size in points.</summary>
    private const float LargeText = 16;

    /// <summary>The extra large text size in points.</summary>
    private const float ExtraLargeText = 24;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Annotations read for hit testing, reused.</summary>
    private readonly List<PageAnnotation> _scratch = [];

    /// <summary>Subscriptions keeping the filtered list up to date.</summary>
    private readonly MultipleDisposable _subscriptions;

    /// <summary>The picture placed by the stamp tool, or <see langword="null"/> to place the stamp's words.</summary>
    private DecodedImage? _picture;

    /// <summary>Initializes a new instance of the <see cref="AnnotationsViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public AnnotationsViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        _subscriptions =
        [
            this.WhenChanged(static x => x.FilterText, static x => x.FilterType, static x => x.FilterColor)
                .Skip(1)
                .SubscribeSafe(_ => ApplyFilter(), OnError),
            this.WhenChanged(static x => x.FilterAuthor, static x => x.SortOrder)
                .Skip(1)
                .SubscribeSafe(_ => ApplyFilter(), OnError),
        ];
        foreach (var subscription in WatchTextFormat())
        {
            _subscriptions.Add(subscription);
        }

        _ = LoadFontFamiliesAsync();
    }

    /// <summary>Gets the stamps offered, in menu order.</summary>
    public static IReadOnlyList<string> Stamps { get; } = ["APPROVED", "REVIEWED", "DRAFT", "CONFIDENTIAL", "FINAL", "NOT APPROVED"];

    /// <summary>Gets the line widths offered, by name, in menu order.</summary>
    public static IReadOnlyList<AnnotationValueOption> LineWidths { get; } = [new("Thin", ThinLine), new("Medium", MediumLine), new("Thick", ThickLine)];

    /// <summary>Gets the text sizes offered, by name, in menu order.</summary>
    public static IReadOnlyList<AnnotationValueOption> FontSizes { get; } = [new("Small", SmallText), new("Medium", MediumText), new("Large", LargeText), new("Extra large", ExtraLargeText)];

    /// <summary>Gets the interaction asking the user for text.</summary>
    public Interaction<TextPrompt, string?> PromptInteraction { get; } = new();

    /// <summary>Gets the interaction asking the user for a picture file to stamp; it returns the path, or <see langword="null"/>.</summary>
    public Interaction<RxVoid, string?> ChoosePictureInteraction { get; } = new();

    /// <summary>Gets or sets a value indicating whether the annotation tool row is shown.</summary>
    [Reactive]
    public partial bool IsAnnotating { get; set; }

    /// <summary>Gets or sets the active tool.</summary>
    [Reactive(
        nameof(IsSelectTool),
        nameof(IsHighlightTool),
        nameof(IsUnderlineTool),
        nameof(IsStrikeOutTool),
        nameof(IsDrawTool),
        nameof(IsNoteTool),
        nameof(IsTextTool),
        nameof(ShapeLabel),
        nameof(StampButtonLabel))]
    public partial AnnotationTool Tool { get; set; }

    /// <summary>Gets or sets a value indicating whether the select tool is active.</summary>
    public bool IsSelectTool
    {
        get => Tool == AnnotationTool.Select;
        set => SelectTool(value, AnnotationTool.Select);
    }

    /// <summary>Gets or sets a value indicating whether the highlight tool is active.</summary>
    public bool IsHighlightTool
    {
        get => Tool == AnnotationTool.Highlight;
        set => SelectTool(value, AnnotationTool.Highlight);
    }

    /// <summary>Gets or sets a value indicating whether the underline tool is active.</summary>
    public bool IsUnderlineTool
    {
        get => Tool == AnnotationTool.Underline;
        set => SelectTool(value, AnnotationTool.Underline);
    }

    /// <summary>Gets or sets a value indicating whether the strikeout tool is active.</summary>
    public bool IsStrikeOutTool
    {
        get => Tool == AnnotationTool.StrikeOut;
        set => SelectTool(value, AnnotationTool.StrikeOut);
    }

    /// <summary>Gets or sets a value indicating whether the draw tool is active.</summary>
    public bool IsDrawTool
    {
        get => Tool == AnnotationTool.Draw;
        set => SelectTool(value, AnnotationTool.Draw);
    }

    /// <summary>Gets or sets a value indicating whether the note tool is active.</summary>
    public bool IsNoteTool
    {
        get => Tool == AnnotationTool.Note;
        set => SelectTool(value, AnnotationTool.Note);
    }

    /// <summary>Gets or sets a value indicating whether the text tool is active.</summary>
    public bool IsTextTool
    {
        get => Tool == AnnotationTool.Text;
        set => SelectTool(value, AnnotationTool.Text);
    }

    /// <summary>Gets the shape button's label: the shape being drawn, or "Shape".</summary>
    public string ShapeLabel => (GetShapeKind(Tool) ?? GetPolygonKind(Tool)) is { } kind ? AnnotationNames.Get(kind) : "Shape";

    /// <summary>Gets or sets the word on the stamp placed by the stamp tool.</summary>
    [Reactive(nameof(StampButtonLabel))]
    public partial string StampLabel { get; set; } = "APPROVED";

    /// <summary>Gets the stamp button's label: the stamp being placed, or "Stamp".</summary>
    public string StampButtonLabel => Tool == AnnotationTool.Stamp ? $"Stamp: {StampLabel}" : "Stamp";

    /// <summary>Gets or sets the colour for new annotations, as 0xRRGGBB; lines, shapes, drawings and text use its deeper tone.</summary>
    [Reactive(nameof(ColorName))]
    public partial uint Color { get; set; } = AnnotationColors.Sand;

    /// <summary>Gets the name of the colour, shown beside its swatch.</summary>
    public string ColorName => AnnotationNames.GetColor(Color);

    /// <summary>Gets or sets the line width of new drawings, lines and shapes, in points.</summary>
    [Reactive(nameof(LineWidthName))]
    public partial float LineWidth { get; set; } = MediumLine;

    /// <summary>Gets the line width's name, shown on its button.</summary>
    public string LineWidthName => $"Line: {NameOf(LineWidths, LineWidth)}";

    /// <summary>Gets or sets the text size of new text boxes and callouts, in points.</summary>
    [Reactive(nameof(FontSizeName), nameof(CurrentTextFormat))]
    public partial float FontSize { get; set; } = MediumText;

    /// <summary>Gets the text size's name, shown on its button.</summary>
    public string FontSizeName => $"Text: {NameOf(FontSizes, FontSize)}";

    /// <summary>Gets the annotation picked with the select tool, outlined on the page.</summary>
    [Reactive]
    public partial PageAnnotation? Selected { get; private set; }

    /// <summary>Gets a value indicating whether the document can be annotated.</summary>
    public bool CanAnnotate => Editor is not null;

    /// <summary>Gets the document's editor, or <see langword="null"/> when the document cannot be edited.</summary>
    private IAnnotationEditor? Editor => DocumentFeature<IAnnotationEditor>.Get(_owner.TryGetDocument());

    /// <summary>Gets the markup kind a tool creates, or <see langword="null"/> when it does not mark text.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The kind.</returns>
    public static AnnotationKind? GetMarkupKind(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Highlight => AnnotationKind.Highlight,
        AnnotationTool.Underline => AnnotationKind.Underline,
        AnnotationTool.StrikeOut => AnnotationKind.StrikeOut,
        AnnotationTool.RedactText => AnnotationKind.Redaction,
        _ => null,
    };

    /// <summary>Gets what a drag draws with a tool, or <see langword="null"/> when the tool does not draw by dragging.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The kind; a callout is dragged from what it points at to where its text goes.</returns>
    public static AnnotationKind? GetShapeKind(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Rectangle => AnnotationKind.Rectangle,
        AnnotationTool.Ellipse => AnnotationKind.Ellipse,
        AnnotationTool.Arrow => AnnotationKind.Arrow,
        AnnotationTool.Line => AnnotationKind.Line,
        AnnotationTool.Callout => AnnotationKind.Callout,
        AnnotationTool.Redact => AnnotationKind.Redaction,
        _ => null,
    };

    /// <summary>Gets the shape a tool draws point by point, or <see langword="null"/> when it does not.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The kind.</returns>
    public static AnnotationKind? GetPolygonKind(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Polygon => AnnotationKind.Polygon,
        AnnotationTool.Cloud => AnnotationKind.Cloud,
        AnnotationTool.PolyLine => AnnotationKind.PolyLine,
        _ => null,
    };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Draws a shape between two points in the deeper tone of the chosen colour, at the chosen line width.</summary>
    /// <param name="page">The page.</param>
    /// <param name="kind">The shape.</param>
    /// <param name="start">Where the drag started.</param>
    /// <param name="end">Where it ended.</param>
    /// <returns><see langword="true"/> when added.</returns>
    public bool AddShape(int page, AnnotationKind kind, PagePoint start, PagePoint end) =>
        EditorForChange() is { } editor && Added(page, editor.AddShape(page, kind, start, end, AnnotationColors.Deep(Color), LineWidth));

    /// <summary>Draws a polygon, cloud or connected lines through points in the deeper tone of the chosen colour.</summary>
    /// <param name="page">The page.</param>
    /// <param name="kind">The shape.</param>
    /// <param name="vertices">The corners.</param>
    /// <returns><see langword="true"/> when added.</returns>
    public bool AddPolygon(int page, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices) =>
        EditorForChange() is { } editor && Added(page, editor.AddPolygon(page, kind, vertices, AnnotationColors.Deep(Color), LineWidth));

    /// <summary>Places the chosen stamp, or the chosen picture, at a point in the deeper tone of the chosen colour.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">The stamp's top-left corner.</param>
    /// <returns><see langword="true"/> when placed.</returns>
    public bool AddStamp(int page, PagePoint location)
    {
        if (EditorForChange() is not { } editor)
        {
            return false;
        }

        if (_picture is not { } picture)
        {
            return !string.IsNullOrWhiteSpace(StampLabel) && Added(page, editor.AddStamp(page, location, StampLabel, AnnotationColors.Deep(Color)));
        }

        var scale = PictureStampSide / Math.Max(picture.Width, picture.Height);
        var bounds = new PageRect(location.X, location.Y, picture.Width * scale, picture.Height * scale);
        return Added(page, editor.AddImageStamp(page, bounds, picture.Pixels.Span, picture.Width, picture.Height));
    }

    /// <summary>Marks the given lines of text on each page; marks over several pages are undone together.</summary>
    /// <param name="kind">The markup kind.</param>
    /// <param name="linesByPage">The selected line rectangles by page.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when anything was marked.</returns>
    public bool MarkText(AnnotationKind kind, IReadOnlyDictionary<int, List<PageRect>> linesByPage, uint color)
    {
        ArgumentNullException.ThrowIfNull(linesByPage);
        if (EditorForChange() is not { } editor)
        {
            return false;
        }

        var marked = false;
        BeginGroup();
        foreach (var (page, lines) in linesByPage)
        {
            if (lines.Count == 0)
            {
                continue;
            }

            marked |= Added(page, editor.AddMarkup(page, kind, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines), color, string.Empty));
        }

        EndGroup();
        return marked;
    }

    /// <summary>Adds a freehand drawing in the deeper tone of the chosen colour, or a drawn signature in ink.</summary>
    /// <param name="page">The page.</param>
    /// <param name="points">The points, stroke after stroke.</param>
    /// <param name="strokeLengths">The number of points in each stroke.</param>
    /// <param name="kind">Ink or signature.</param>
    /// <returns><see langword="true"/> when added.</returns>
    public bool AddInk(int page, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, AnnotationKind kind)
    {
        var signature = kind == AnnotationKind.Signature;
        var color = signature ? AnnotationColors.Ink : AnnotationColors.Deep(Color);
        var width = signature ? InkWidth : LineWidth;
        return EditorForChange() is { } editor && Added(page, editor.AddInk(page, points, strokeLengths, color, width, kind));
    }

    /// <summary>
    /// Keeps a measurement on the page: its lines as a drawing whose comment is the measurement, and the measurement
    /// as text beside its last point, so it shows when the page is printed. Both are undone together.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="points">The measured points, in page space.</param>
    /// <param name="closed">Whether the shape closes back to its first point, as an area does.</param>
    /// <param name="description">The measurement, such as "Distance 12.4 m at 30°".</param>
    /// <returns><see langword="true"/> when it was added.</returns>
    public bool AddMeasurement(int page, ReadOnlySpan<PagePoint> points, bool closed, string description)
    {
        if (EditorForChange() is not { } editor || points.Length < 2)
        {
            return false;
        }

        PagePoint[] path = closed ? [.. points, points[0]] : [.. points];
        ReadOnlySpan<int> lengths = [path.Length];
        BeginGroup();
        var line = editor.AddInk(page, path, lengths, AnnotationColors.Ink, InkWidth, AnnotationKind.Ink);
        var added = Added(page, line);
        if (added)
        {
            _ = editor.SetContents(page, line, description);
            _ = Added(page, editor.AddText(page, points[^1], description, MeasurementTextSize, AnnotationColors.Ink, AnnotationKind.TextBox));
        }

        EndGroup();
        return added;
    }

    /// <summary>Asks for a note and adds it at a point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">Where the note icon goes.</param>
    /// <returns>A task.</returns>
    public async Task AddNoteAsync(int page, PagePoint location)
    {
        var text = await PromptInteraction.Handle(new("Add Note", "Note", string.Empty, "Add Note", true)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text) && EditorForChange() is { } editor)
        {
            _ = Added(page, editor.AddNote(page, location, text, Color));
        }
    }

    /// <summary>Asks for text and writes it at a point, at the chosen size, in the deeper tone of the chosen colour.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">The top-left corner of the text.</param>
    /// <returns>A task.</returns>
    public async Task AddTextAsync(int page, PagePoint location)
    {
        var text = await PromptInteraction.Handle(new("Add Text", "Text to write on the page", string.Empty, "Add Text", true)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text) && EditorForChange() is { } editor)
        {
            _ = Added(page, editor.AddText(page, location, text, FontSize, AnnotationColors.Deep(Color), AnnotationKind.TextBox));
        }
    }

    /// <summary>Asks for text and writes it as a callout pointing at a point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="target">What the callout points at.</param>
    /// <param name="location">The top-left corner of the text.</param>
    /// <returns>A task.</returns>
    public async Task AddCalloutAsync(int page, PagePoint target, PagePoint location)
    {
        var text = await PromptInteraction.Handle(new("Add Callout", "Text of the callout", string.Empty, "Add Callout", true)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text) && EditorForChange() is { } editor)
        {
            _ = Added(page, editor.AddCallout(page, target, location, text, FontSize, AnnotationColors.Deep(Color)));
        }
    }

    /// <summary>Writes a signature or initials mark into its bounds as a removable signature annotation, then picks it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">Where the mark goes, in page space.</param>
    /// <param name="mark">The mark.</param>
    /// <returns>The placed annotation, or <see langword="null"/> when it could not be placed.</returns>
    public PageAnnotation? PlaceMark(int page, PageRect bounds, SignatureMark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        var document = DocumentForChange();
        if (document is null
            || DocumentFeature<IAnnotationEditor>.Get(document) is not { } editor
            || !mark.IsValid)
        {
            return null;
        }

        var imageSignatureEditor = DocumentFeature<IImageSignatureEditor>.Get(document);

        var index = mark.Style switch
        {
            SignatureMarkStyle.Typed => AddTypedMark(editor, page, bounds, mark),
            SignatureMarkStyle.Drawn => AddDrawnMark(editor, page, bounds, mark),
            SignatureMarkStyle.Image when imageSignatureEditor is IImageSignatureEditor images => images.AddImageSignature(page, bounds, mark.Pixels.Span, (int)mark.Width, (int)mark.Height),
            _ => -1,
        };
        if (!Added(page, index))
        {
            return null;
        }

        Selected = Find(page, index);
        return Selected;
    }

    /// <summary>Reads one annotation as it is now.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The annotation, or <see langword="null"/> when there is none at that index.</returns>
    public PageAnnotation? Find(int page, int index)
    {
        if (Editor is not { } editor)
        {
            return null;
        }

        _scratch.Clear();
        editor.GetAnnotations(page, _scratch);
        foreach (var annotation in _scratch)
        {
            if (annotation.Index == index)
            {
                return annotation;
            }
        }

        return null;
    }

    /// <summary>Finds the annotation under a point, topmost first.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The annotation, or <see langword="null"/>.</returns>
    public PageAnnotation? HitTest(int page, PagePoint point)
    {
        if (Editor is not { } editor)
        {
            return null;
        }

        _scratch.Clear();
        editor.GetAnnotations(page, _scratch);
        for (var i = _scratch.Count - 1; i >= 0; i--)
        {
            var bounds = _scratch[i].Bounds;
            var grown = PageRect.FromEdges(bounds.Left - HitTolerance, bounds.Top - HitTolerance, bounds.Right + HitTolerance, bounds.Bottom + HitTolerance);
            if (grown.Contains(point))
            {
                return _scratch[i];
            }
        }

        return null;
    }

    /// <summary>Picks an annotation, or clears the pick.</summary>
    /// <param name="annotation">The annotation.</param>
    public void Select(PageAnnotation? annotation) => Selected = annotation;

    /// <summary>Writes a typed mark's text at the size that fills its bounds, as a signature.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">Where the mark goes.</param>
    /// <param name="mark">The typed mark.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private static int AddTypedMark(IAnnotationEditor editor, int page, PageRect bounds, SignatureMark mark)
    {
        var size = (float)SignatureMarkLayout.TypedFontSize(bounds.Height);
        return editor.AddText(page, new(bounds.Left, bounds.Top), mark.Text, size, AnnotationColors.Ink, AnnotationKind.Signature);
    }

    /// <summary>Draws a drawn mark's strokes, scaled into its bounds, as a signature.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">Where the mark goes.</param>
    /// <param name="mark">The drawn mark.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private static int AddDrawnMark(IAnnotationEditor editor, int page, PageRect bounds, SignatureMark mark)
    {
        var count = mark.Points.Length / PointCoordinates;
        var points = ArrayPool<PagePoint>.Shared.Rent(count);
        try
        {
            SignatureMarkLayout.MapPoints(mark, bounds, points);
            return editor.AddInk(page, points.AsSpan(0, count), mark.StrokeLengths.Span, AnnotationColors.Ink, InkWidth, AnnotationKind.Signature);
        }
        finally
        {
            ArrayPool<PagePoint>.Shared.Return(points);
        }
    }

    /// <summary>Finds the name of a value in a list of named choices.</summary>
    /// <param name="choices">The choices.</param>
    /// <param name="value">The value.</param>
    /// <returns>The name, or the value in points when it is not one of the choices.</returns>
    private static string NameOf(IReadOnlyList<AnnotationValueOption> choices, float value)
    {
        foreach (var (name, choice) in choices)
        {
            if (Math.Abs(choice - value) < float.Epsilon)
            {
                return name;
            }
        }

        return $"{value:0.#} pt";
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Turns a tool on, or back to selecting when it is turned off.</summary>
    /// <param name="on">Whether the tool is being turned on.</param>
    /// <param name="tool">The tool.</param>
    private void SelectTool(bool on, AnnotationTool tool)
    {
        if (on)
        {
            Tool = tool;
        }
        else if (Tool == tool)
        {
            Tool = AnnotationTool.Select;
        }
    }

    /// <summary>Chooses a tool, or goes back to selecting when it is already the active one.</summary>
    /// <param name="tool">The tool.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetTool(AnnotationTool tool) => Tool = Tool == tool ? AnnotationTool.Select : tool;

    /// <summary>Chooses a stamp's words and the stamp tool.</summary>
    /// <param name="label">The stamp's words; blank words keep the current stamp.</param>
    [ReactiveCommand]
    private void SetStamp(string? label)
    {
        if (!string.IsNullOrWhiteSpace(label))
        {
            StampLabel = label.Trim();
            _picture = null;
        }

        Tool = AnnotationTool.Stamp;
    }

    /// <summary>Asks for the person's own words for a stamp, then chooses the stamp tool.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task CustomStampAsync()
    {
        var text = await PromptInteraction.Handle(new("Custom Stamp", "Words on the stamp", string.Empty, "Use Stamp", false)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text))
        {
            SetStamp(text);
        }
    }

    /// <summary>Asks for a picture, reads it, and chooses the stamp tool to place it.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task PictureStampAsync()
    {
        if (await ChoosePictureInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is not { Length: > 0 } path)
        {
            return;
        }

        UsePicture(await Task.Run(() => SignatureImageFile.Load(path)).ConfigureAwait(true));
    }

    /// <summary>Chooses a picture for the stamp tool.</summary>
    /// <param name="picture">The decoded picture, or <see langword="null"/> when it could not be read.</param>
    private void UsePicture(DecodedImage? picture)
    {
        if (picture is null || picture.Width <= 0 || picture.Height <= 0)
        {
            _owner.Notice = "That picture could not be read. Choose a PNG, JPEG, BMP or WebP file.";
            return;
        }

        _picture = picture;
        StampLabel = PictureStampLabel;
        Tool = AnnotationTool.Stamp;
    }

    /// <summary>Shows the tools; Fill &amp; Sign is put away so only one tool row is ever shown.</summary>
    [ReactiveCommand]
    private void Start()
    {
        _owner.FillAndSign.IsActive = false;
        IsAnnotating = true;
    }

    /// <summary>Hides the tools and goes back to selecting text.</summary>
    [ReactiveCommand]
    private void Done()
    {
        _ = CommitText();
        Tool = AnnotationTool.Select;
        IsAnnotating = false;
    }

    /// <summary>Marks the selected text.</summary>
    /// <param name="request">What to mark, and the lines it covers.</param>
    /// <returns><see langword="true"/> when anything was marked.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MarkSelection(MarkSelectionRequest request) => MarkText(request.Kind, request.Lines, request.Color);

    /// <summary>Asks for a note and adds it where the page was clicked.</summary>
    /// <param name="location">The page and point.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task AddNoteHere(PageLocation location) => AddNoteAsync(location.Page, location.Point);

    /// <summary>Asks for text and writes it where the page was clicked.</summary>
    /// <param name="location">The page and point.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task AddTextHere(PageLocation location) => AddTextAsync(location.Page, location.Point);

    /// <summary>Scrolls to an annotation and picks it.</summary>
    /// <param name="item">The sidebar item.</param>
    [ReactiveCommand]
    private void GoTo(AnnotationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        Selected = item.Annotation;
        _owner.NavigateTo(new(item.Annotation.PageIndex, item.Annotation.Bounds, 0));
    }

    /// <summary>Redraws an edited page and refreshes its entries in the list.</summary>
    /// <param name="page">The page.</param>
    private void Edited(int page)
    {
        _owner.OnPageEdited(page);
        RefreshPage(page);
    }
}

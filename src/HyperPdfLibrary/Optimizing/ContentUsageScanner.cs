// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Reads every page's content without decoding images or fonts, to learn how the document uses its resources: the
/// largest size each image is drawn at, which images are used where their size is unknown, which fonts the pages show
/// text in, which fonts other parts of the document need whole, and which resource names each resource dictionary's
/// content uses. Only the transformation matrix is tracked, so a page costs one pass over its operators.
/// </summary>
[DebuggerDisplay("ContentUsageScanner: {Images.Count} images")]
internal sealed partial class ContentUsageScanner
{
    /// <summary>Points per inch.</summary>
    private const float PointsPerInch = 72;

    /// <summary>The smallest drawn size, in points, that is measured; smaller uses are degenerate.</summary>
    private const float MinimumExtent = 0.001F;

    /// <summary>The times one form is walked with its own matrix before later uses are counted as fixed.</summary>
    private const int MaxFormWalks = 64;

    /// <summary>The operands of a matrix.</summary>
    private const int MatrixOperands = 6;

    /// <summary>The index of a matrix's third number.</summary>
    private const int MatrixC = 2;

    /// <summary>The index of a matrix's fourth number.</summary>
    private const int MatrixD = 3;

    /// <summary>The index of a matrix's fifth number.</summary>
    private const int MatrixE = 4;

    /// <summary>The index of a matrix's sixth number.</summary>
    private const int MatrixF = 5;

    /// <summary>The document read.</summary>
    private readonly PdfDocument _document;

    /// <summary>The saved matrices of every stream being walked.</summary>
    private readonly List<Matrix3x2> _stack = [];

    /// <summary>The times each form has been walked with a measured matrix.</summary>
    private readonly Dictionary<int, int> _formWalks = [];

    /// <summary>The forms, patterns and appearance streams already walked as fixed.</summary>
    private readonly HashSet<int> _fixedWalked = [];

    /// <summary>The token of the current document scan.</summary>
    private CancellationToken _cancellationToken;

    /// <summary>Initializes a new instance of the <see cref = "ContentUsageScanner"/> class.</summary>
    /// <param name = "document">The document to read.</param>
    internal ContentUsageScanner(PdfDocument document) => _document = document;

    /// <summary>Gets how each image is used, by object number.</summary>
    internal Dictionary<int, ImageUse> Images { get; } = [];

    /// <summary>Gets the resource names each resource dictionary's content uses.</summary>
    internal Dictionary<PdfDictionary, HashSet<ResourceUse>> UsedNames { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Gets the resource dictionaries whose names something unscanned may use, so none of their entries may be removed.</summary>
    internal HashSet<PdfDictionary> UnsafeResources { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Gets the fonts page content shows text in, by object number.</summary>
    internal HashSet<int> PageFonts { get; } = [];

    /// <summary>Gets the fonts used outside page content, which must keep every glyph.</summary>
    internal HashSet<int> ExcludedFonts { get; } = [];

    /// <summary>Gets the object number of each font dictionary seen.</summary>
    internal Dictionary<PdfDictionary, int> FontNumbers { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Gets a value indicating whether walked content sets a device colour or selects a device colour space.</summary>
    internal bool UsesDeviceColor { get; private set; }

    /// <summary>Reads every page.</summary>
    /// <param name = "pageDone">Called after each page with the number of pages read.</param>
    /// <param name = "cancellationToken">Stops the scan.</param>
    internal void ScanDocument(Action<int>? pageDone, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        for (var i = 0; i < _document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanPage(PdfDocumentPages.GetPage(_document, i));
            pageDone?.Invoke(i + 1);
        }

        ScanAcroForm();
    }

    /// <summary>Reads one page: its content and its annotations' appearances.</summary>
    /// <param name = "page">The page.</param>
    internal void ScanPage(PdfPage page)
    {
        var userUnit = (float)page.Dictionary.GetNumber(KnownName.UserUnit, 1);
        var content = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(page, ref content, _cancellationToken);
            Walk(content.WrittenSpan, page.Resources, Matrix3x2.Identity, new(userUnit, 0, false));
        }
        finally
        {
            content.Dispose();
        }

        ScanAnnotations(page);
    }

    /// <summary>Determines whether an operator sets a device colour or selects a device colour space.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader, for the colour space operand.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsDeviceColor(ContentOperator op, ref ContentReader reader) => op switch
    {
        ContentOperator.SetFillGray or
        ContentOperator.SetStrokeGray or
        ContentOperator.SetFillRgb or
        ContentOperator.SetStrokeRgb or
        ContentOperator.SetFillCmyk or
        ContentOperator.SetStrokeCmyk =>
        true,

        ContentOperator.SetFillColorSpace or ContentOperator.SetStrokeColorSpace =>
            reader.Operand(0).Name.ToKnownName() is KnownName.DeviceGray or KnownName.DeviceRGB or KnownName.DeviceCMYK,
        _ => false,
    };

    /// <summary>Gets the operator's numbers as a matrix.</summary>
    /// <param name = "reader">The reader.</param>
    /// <returns>The matrix.</returns>
    private static Matrix3x2 ReadMatrix(ref ContentReader reader) =>
        reader.OperandCount < MatrixOperands ? Matrix3x2.Identity : new(
            reader.Number(0),
            reader.Number(1),
            reader.Number(MatrixC),
            reader.Number(MatrixD),
            reader.Number(MatrixE),
            reader.Number(MatrixF));

    /// <summary>Reads a form's or stream's /Matrix.</summary>
    /// <param name = "dictionary">The stream dictionary.</param>
    /// <returns>The matrix; identity when absent.</returns>
    private static Matrix3x2 ReadMatrix(PdfDictionary dictionary)
    {
        Span<float> numbers = stackalloc float[MatrixOperands];
        return dictionary.GetArray(KnownName.Matrix) is { Count: >= MatrixOperands } array
        && array.ReadNumbers(numbers) >= MatrixOperands ? new(
            numbers[0],
            numbers[1],
            numbers[MatrixC],
            numbers[MatrixD],
            numbers[MatrixE],
            numbers[MatrixF]) : Matrix3x2.Identity;
    }

    /// <summary>Gets the resolution an image is drawn at by a matrix: the lower of its two axes.</summary>
    /// <param name = "ctm">The matrix mapping the unit square to user space.</param>
    /// <param name = "userUnit">The page's user unit, in points.</param>
    /// <param name = "width">The image width in pixels.</param>
    /// <param name = "height">The image height in pixels.</param>
    /// <returns>The resolution in pixels per inch, or infinity when the image is drawn with no area.</returns>
    private static float Resolution(Matrix3x2 ctm, float userUnit, int width, int height)
    {
        var across = MathF.Sqrt((ctm.M11 * ctm.M11) + (ctm.M12 * ctm.M12)) * userUnit;
        var down = MathF.Sqrt((ctm.M21 * ctm.M21) + (ctm.M22 * ctm.M22)) * userUnit;
        return across > MinimumExtent && down > MinimumExtent ? Math.Min(width * PointsPerInch / across, height * PointsPerInch / down) : float.PositiveInfinity;
    }

    /// <summary>Walks a content stream.</summary>
    /// <param name = "content">The decoded content.</param>
    /// <param name = "resources">The resources in force.</param>
    /// <param name = "ctm">The matrix at the start.</param>
    /// <param name = "walk">The walk's settings.</param>
    private void Walk(ReadOnlySpan<byte> content, PdfDictionary? resources, Matrix3x2 ctm, WalkSettings walk)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, _document.Objects.Names, operands);
        var floor = _stack.Count;
        try
        {
            while (reader.Next(out var op))
            {
                UsesDeviceColor |= IsDeviceColor(op, ref reader);
                if (!HandleState(op, ref reader, ref ctm, floor))
                {
                    HandleResource(op, ref reader, resources, ctm, walk);
                }
            }
        }
        catch (Exception e) when (e is InvalidDataException or PdfException or FormatException or OverflowException)
        {
            // Damaged content stops the walk; its resources are kept whole.
            MarkUnsafe(resources);
        }
        finally
        {
            _stack.RemoveRange(floor, _stack.Count - floor);
        }
    }

    /// <summary>Handles the operators that change the matrix.</summary>
    /// <param name = "op">The operator.</param>
    /// <param name = "reader">The reader.</param>
    /// <param name = "ctm">The current matrix.</param>
    /// <param name = "floor">The stack depth this stream may not restore below.</param>
    /// <returns><see langword="true"/> when the operator was one of them.</returns>
    private bool HandleState(ContentOperator op, ref ContentReader reader, ref Matrix3x2 ctm, int floor)
    {
        switch (op)
        {
            case ContentOperator.Save:
                {
                    _stack.Add(ctm);
                    return true;
                }

            case ContentOperator.Restore:
                {
                    if (_stack.Count > floor)
                    {
                        ctm = _stack[^1];
                        _stack.RemoveAt(_stack.Count - 1);
                    }

                    return true;
                }

            case ContentOperator.ConcatMatrix:
                {
                    ctm = ReadMatrix(ref reader) * ctm;
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Handles the operators that name resources.</summary>
    /// <param name = "op">The operator.</param>
    /// <param name = "reader">The reader.</param>
    /// <param name = "resources">The resources in force.</param>
    /// <param name = "ctm">The current matrix.</param>
    /// <param name = "walk">The walk's settings.</param>
    private void HandleResource(ContentOperator op, ref ContentReader reader, PdfDictionary? resources, Matrix3x2 ctm, WalkSettings walk)
    {
        switch (op)
        {
            case ContentOperator.PaintXObject:
                {
                    PaintXObject(reader.Operand(0).Name, resources, ctm, walk);
                    break;
                }

            case ContentOperator.SetFont:
                {
                    UseFont(reader.Operand(0).Name, resources, walk.IsFixed);
                    break;
                }

            case ContentOperator.SetGraphicsState:
                {
                    UseGraphicsState(reader.Operand(0).Name, resources, walk);
                    break;
                }

            case ContentOperator.BeginInlineImage:
                {
                    // Inline images may name colour spaces and filters by resource name.
                    MarkUnsafe(resources);
                    break;
                }

            default:
                {
                    HandleNamedResource(op, ref reader, resources, walk);
                    break;
                }
        }
    }

    /// <summary>Records the operators that only name a resource: colour spaces, patterns, shadings and properties.</summary>
    /// <param name = "op">The operator.</param>
    /// <param name = "reader">The reader.</param>
    /// <param name = "resources">The resources in force.</param>
    /// <param name = "walk">The walk's settings.</param>
    private void HandleNamedResource(ContentOperator op, ref ContentReader reader, PdfDictionary? resources, WalkSettings walk)
    {
        switch (op)
        {
            case ContentOperator.SetFillColorSpace or ContentOperator.SetStrokeColorSpace:
                {
                    Use(resources, KnownName.ColorSpace, reader.Operand(0).Name);
                    break;
                }

            case ContentOperator.SetFillColorN or ContentOperator.SetStrokeColorN:
                {
                    var last = reader.Operand(reader.OperandCount - 1);
                    if (last.Kind == ContentOperandKind.Name)
                    {
                        UsePattern(last.Name, resources, walk);
                    }

                    break;
                }

            case ContentOperator.PaintShading:
                {
                    Use(resources, KnownName.Shading, reader.Operand(0).Name);
                    break;
                }

            case ContentOperator.BeginMarkedContentProperties or ContentOperator.MarkPointProperties:
                {
                    var properties = reader.Operand(1);
                    if (properties.Kind == ContentOperandKind.Name)
                    {
                        Use(resources, KnownName.Properties, properties.Name);
                    }

                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Records a resource name as used.</summary>
    /// <param name = "resources">The resource dictionary.</param>
    /// <param name = "category">The category.</param>
    /// <param name = "name">The name.</param>
    private void Use(PdfDictionary? resources, KnownName category, PdfName name)
    {
        if (resources is null || name.IsNone)
        {
            return;
        }

        ref var names = ref CollectionsMarshal.GetValueRefOrAddDefault(UsedNames, resources, out _);
        names ??= [];
        _ = names.Add(new(category, name));
    }

    /// <summary>Stops a resource dictionary's entries from being removed.</summary>
    /// <param name = "resources">The resource dictionary.</param>
    private void MarkUnsafe(PdfDictionary? resources)
    {
        if (resources is not null)
        {
            _ = UnsafeResources.Add(resources);
        }
    }

    /// <summary>Handles <c>Do</c>: measures an image or walks a form.</summary>
    /// <param name = "name">The XObject's resource name.</param>
    /// <param name = "resources">The resources in force.</param>
    /// <param name = "ctm">The current matrix.</param>
    /// <param name = "walk">The walk's settings.</param>
    private void PaintXObject(PdfName name, PdfDictionary? resources, Matrix3x2 ctm, WalkSettings walk)
    {
        Use(resources, KnownName.XObject, name);
        var raw = resources?.GetDictionary(KnownName.XObject)?.GetRaw(name) ?? default;
        if (!raw.IsReference || StoreReading.Resolve(_document.Objects, raw).AsStream() is not { } xobject)
        {
            return;
        }

        var number = raw.AsReference().Number;
        var subtype = xobject.Dictionary.GetName(KnownName.Subtype);
        if (subtype.Is(KnownName.Image))
        {
            RecordImage(number, xobject.Dictionary, ctm, walk);
        }
        else if (subtype.Is(KnownName.Form))
        {
            WalkForm(number, xobject, resources, ctm, walk);
        }
    }

    /// <summary>Records one use of an image.</summary>
    /// <param name = "number">The image's object number.</param>
    /// <param name = "dictionary">The image dictionary.</param>
    /// <param name = "ctm">The matrix it is drawn with.</param>
    /// <param name = "walk">The walk's settings.</param>
    private void RecordImage(int number, PdfDictionary dictionary, Matrix3x2 ctm, WalkSettings walk)
    {
        ref var use = ref CollectionsMarshal.GetValueRefOrAddDefault(Images, number, out var exists);
        if (!exists)
        {
            use = ImageUse.Unseen;
        }

        if (walk.IsFixed)
        {
            use = use with
            {
                IsFixed = true
            };
            return;
        }

        var ppi = Resolution(ctm, walk.UserUnit, dictionary.GetInt32(KnownName.Width, 0), dictionary.GetInt32(KnownName.Height, 0));
        use = use with
        {
            MinPpi = Math.Min(use.MinPpi, ppi),
            Uses = use.Uses + 1
        };
    }

    /// <summary>Walks a form XObject with its matrix and resources.</summary>
    /// <param name = "number">The form's object number.</param>
    /// <param name = "form">The form.</param>
    /// <param name = "resources">The resources in force, which a form without its own inherits.</param>
    /// <param name = "ctm">The matrix it is drawn with.</param>
    /// <param name = "walk">The walk's settings.</param>
    private void WalkForm(int number, PdfStream form, PdfDictionary? resources, Matrix3x2 ctm, WalkSettings walk)
    {
        if (walk.Depth >= PdfLimits.MaxDrawDepth)
        {
            return;
        }

        var isFixed = walk.IsFixed;
        ref var walks = ref CollectionsMarshal.GetValueRefOrAddDefault(_formWalks, number, out _);
        walks++;
        if (walks > MaxFormWalks)
        {
            // Measured often enough; later uses only keep what they reach at full resolution.
            isFixed = true;
        }

        if (isFixed && !_fixedWalked.Add(number))
        {
            return;
        }

        WalkStream(form, form.Dictionary.GetDictionary(KnownName.Resources) ?? resources, ReadMatrix(form.Dictionary) * ctm, walk with { Depth = walk.Depth + 1, IsFixed = isFixed });
    }

    /// <summary>Decodes a stream and walks it.</summary>
    /// <param name = "stream">The content stream.</param>
    /// <param name = "resources">Its resources.</param>
    /// <param name = "ctm">The matrix at its start.</param>
    /// <param name = "walk">The walk's settings.</param>
    private void WalkStream(PdfStream stream, PdfDictionary? resources, Matrix3x2 ctm, WalkSettings walk)
    {
        var content = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref content, _cancellationToken);
            Walk(content.WrittenSpan, resources, ctm, walk);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>The settings of one walk.</summary>
    /// <param name = "UserUnit">The page's user unit, in points.</param>
    /// <param name = "Depth">The nesting of forms.</param>
    /// <param name = "IsFixed">Whether images reached are used where their size is unknown.</param>
    private readonly record struct WalkSettings(float UserUnit, int Depth, bool IsFixed);
}

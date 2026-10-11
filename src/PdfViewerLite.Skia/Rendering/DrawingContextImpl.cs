// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using PdfViewerLite.Skia.Bitmaps;
using PdfViewerLite.Skia.Fonts;
using PdfViewerLite.Skia.Geometry;
using PdfViewerLite.Skia.Gpu;
using SkiaSharp;

namespace PdfViewerLite.Skia.Rendering;

/// <summary>High-performance SkiaSharp-based drawing context.</summary>
internal sealed class DrawingContextImpl : IDrawingContextWithAcrylicLikeSupport, IDrawingContextImplWithEffects
{
    /// <summary>The dpi scale factor.</summary>
    private const double DpiScaleFactor = 96.0;

    /// <summary>The half divisor.</summary>
    private const double HalfDivisor = 2.0;

    /// <summary>The max stack alloc count.</summary>
    private const int MaxStackAllocCount = 16;

    /// <summary>The max box shadow dimension.</summary>
    private const double MaxBoxShadowDimension = 8192.0;

    /// <summary>The blur sigma multiplier.</summary>
    private const float BlurSigmaMultiplier = 0.288675F;

    /// <summary>The blur sigma offset.</summary>
    private const float BlurSigmaOffset = 0.5F;

    /// <summary>How far, in device pixels, a bitmap's drawn size may differ from its pixel size and still count as 1:1.</summary>
    private const float OneToOneTolerance = 0.5F;

    /// <summary>The sampling for bitmaps drawn at their own size: bilinear, which is exact on whole pixels and adds no cubic blur.</summary>
    private static readonly SKSamplingOptions OneToOneSampling = new(SKFilterMode.Linear, SKMipmapMode.None);

    /// <summary>The intermediate surface dpi.</summary>
    private readonly Vector _intermediateSurfaceDpi;

    /// <summary>The mask stack.</summary>
    private readonly Stack<(SKMatrix Matrix, PaintWrapper Paint, SKPaint Owned)> _maskStack = new();

    /// <summary>The opacity stack.</summary>
    private readonly Stack<double> _opacityStack = new();

    /// <summary>The render options stack.</summary>
    private readonly Stack<RenderOptions> _renderOptionsStack = new();

    /// <summary>The text options stack.</summary>
    private readonly Stack<TextOptions> _textOptionsStack = new();

    /// <summary>The post transform.</summary>
    private readonly Matrix? _postTransform;

    /// <summary>The disable subpixel text rendering.</summary>
    private readonly bool _disableSubpixelTextRendering;

    /// <summary>The use opacity save layer.</summary>
    private readonly bool _useOpacitySaveLayer;

    /// <summary>The gpu.</summary>
    private readonly ISkiaGpu? _gpu;

    /// <summary>The session.</summary>
    private readonly ISkiaGpuRenderSession? _session;

    /// <summary>The stroke paint.</summary>
    private readonly SKPaint _strokePaint = new();

    /// <summary>The fill paint.</summary>
    private readonly SKPaint _fillPaint = new();

    /// <summary>The box shadow paint.</summary>
    private readonly SKPaint _boxShadowPaint = new();

    /// <summary>The paint for bitmap draws, reset before each use.</summary>
    private readonly SKPaint _bitmapPaint = new();

    /// <summary>The paint for opacity and mask layers, which Skia copies when the layer is saved.</summary>
    private readonly SKPaint _layerPaint = new();

    /// <summary>The round rect.</summary>
    private readonly SKRoundRect _roundRect = new();

    /// <summary>The disposables.</summary>
    private IDisposable?[]? _disposables;

    /// <summary>The current opacity.</summary>
    private double _currentOpacity = 1.0;

    /// <summary>The current transform.</summary>
    private Matrix? _currentTransform;

    /// <summary>The gr context.</summary>
    private GRContext? _graphicsContext;

    /// <summary>The disposed.</summary>
    private int _disposed;

    /// <summary>The leased.</summary>
    private bool _leased;

    /// <summary>Initializes a new instance of the <see cref = "DrawingContextImpl"/> class.</summary>
    /// <param name = "createInfo">The context creation parameters.</param>
    /// <param name = "disposables">Optional elements to dispose when this context is disposed.</param>
    /// <exception cref = "ArgumentException">Thrown when <c>createInfo.Surface?.Canvas</c> is <see langword="null"/>.</exception>
    public DrawingContextImpl(in CreateInfo createInfo, params IDisposable?[]? disposables)
    {
        Canvas = createInfo.Canvas ?? createInfo.Surface?.Canvas ?? throw new ArgumentException("Invalid create info: no Canvas or Surface provided.", nameof(createInfo));
        _intermediateSurfaceDpi = createInfo.Dpi;
        _disposables = disposables;
        _disableSubpixelTextRendering = createInfo.DisableSubpixelTextRendering;
        _graphicsContext = createInfo.GrContext;
        _gpu = createInfo.Gpu;
        _session = createInfo.CurrentSession;
        SurfaceColorType = createInfo.SurfaceColorType ?? SKImageInfo.PlatformColorType;
        if (_graphicsContext is not null)
        {
            Monitor.Enter(_graphicsContext);
        }

        Surface = createInfo.Surface;
        if (createInfo.ScaleDrawingToDpi && !createInfo.Dpi.NearlyEquals(SkiaPlatform.DefaultDpi))
        {
            _postTransform = Matrix.CreateScale(createInfo.Dpi.X / SkiaPlatform.DefaultDpi.X, createInfo.Dpi.Y / SkiaPlatform.DefaultDpi.Y);
        }

        Transform = Matrix.Identity;
        var options = AvaloniaLocator.Current.GetService<SkiaOptions>();
        if (options is not null)
        {
            _useOpacitySaveLayer = options.UseOpacitySaveLayer;
        }
    }

    /// <summary>Gets the Skia canvas.</summary>
    public SKCanvas Canvas { get; }

    /// <summary>Gets the Skia surface if available.</summary>
    public SKSurface? Surface { get; }

    /// <summary>Gets the GPU context if available.</summary>
    public GRContext? GrContext => _graphicsContext;

    /// <summary>Gets the pixel channel order of the compositor surface.</summary>
    public SKColorType SurfaceColorType { get; }

    /// <summary>Gets or sets the render options.</summary>
    public RenderOptions RenderOptions { get; set; }

    /// <summary>Gets or sets the text options.</summary>
    public TextOptions TextOptions { get; set; }

    /// <inheritdoc/>
    public Matrix Transform
    {
        get => _currentTransform ??= Canvas.TotalMatrix44.ToAvaloniaMatrix();
        set
        {
            CheckLease();
            if (_currentTransform == value)
            {
                return;
            }

            _currentTransform = value;
            var transform = value;
            if (_postTransform.HasValue)
            {
                transform *= _postTransform.Value;
            }

            Canvas.SetMatrix(transform.ToSKMatrix44());
        }
    }

    /// <inheritdoc/>
    public void Clear(Color color)
    {
        CheckLease();
        Canvas.Clear(color.ToSKColor());
    }

    /// <inheritdoc/>
    public void DrawBitmap(IBitmapImpl source, double opacity, Rect sourceRect, Rect destRect)
    {
        CheckLease();
        var drawableImage = (IDrawableBitmapImpl)source;
        var s = sourceRect.ToSKRect();
        var d = destRect.ToSKRect();

        // Compare in device pixels: a tile drawn 1:1 on a scaled display is neither up nor down scaled, and choosing
        // its filter from canvas units made it flip between filters as the zoom or position changed.
        var matrix = Canvas.TotalMatrix;
        var deviceWidth = d.Width * MathF.Sqrt((matrix.ScaleX * matrix.ScaleX) + (matrix.SkewY * matrix.SkewY));
        var deviceHeight = d.Height * MathF.Sqrt((matrix.ScaleY * matrix.ScaleY) + (matrix.SkewX * matrix.SkewX));
        var samplingOptions = Math.Abs(deviceWidth - s.Width) < OneToOneTolerance && Math.Abs(deviceHeight - s.Height) < OneToOneTolerance
            ? OneToOneSampling
            : RenderOptions.BitmapInterpolationMode.ToSKSamplingOptions(deviceWidth > s.Width || deviceHeight > s.Height);
        _bitmapPaint.Reset();
        _bitmapPaint.Color = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(byte.MaxValue * opacity * _currentOpacity));
        _bitmapPaint.BlendMode = RenderOptions.BitmapBlendingMode.ToSKBlendMode();
        _bitmapPaint.IsAntialias = RenderOptions.EdgeMode != EdgeMode.Aliased;
        drawableImage.Draw(this, s, d, samplingOptions, _bitmapPaint);
    }

    /// <inheritdoc/>
    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect)
    {
        CheckLease();
        PushOpacityMask(opacityMask, opacityMaskRect);
        DrawBitmap(source, 1.0, new(0, 0, source.PixelSize.Width, source.PixelSize.Height), destRect);
        PopOpacityMask();
    }

    /// <inheritdoc/>
    public void DrawLine(IPen? pen, Point p1, Point p2)
    {
        CheckLease();
        if (pen is not null && TryCreatePaint(_strokePaint, pen, new Rect(p1, p2).Normalize()) is { } stroke)
        {
            using (stroke)
            {
                Canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, stroke.Paint);
            }
        }
    }

    /// <inheritdoc/>
    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry)
    {
        CheckLease();
        if (geometry is RectangleGeometryImpl)
        {
            DrawRectangle(brush, pen, new(geometry.Bounds));
            return;
        }

        if (geometry is EllipseGeometryImpl)
        {
            DrawEllipse(brush, pen, geometry.Bounds);
            return;
        }

        DrawPathGeometry(brush, pen, (GeometryImpl)geometry);
    }

    /// <inheritdoc/>
    public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows boxShadows = default)
    {
        if (rect.Rect.Height <= 0 || rect.Rect.Width <= 0)
        {
            return;
        }

        CheckLease();
        var bounds = rect.Rect.ToSKRect();
        if (TryDrawSolidRectangle(brush, pen, rect, boxShadows))
        {
            return;
        }

        if (rect.Rect.Height > MaxBoxShadowDimension || rect.Rect.Width > MaxBoxShadowDimension)
        {
            boxShadows = default;
        }

        var rounded = PrepareRoundedRectangle(rect, boxShadows.HasInsetShadows);
        DrawRectangleShadows(bounds, rounded, rect.IsRounded, boxShadows, false);
        if (brush is not null)
        {
            using var fill = CreatePaint(_fillPaint, brush, rect.Rect);
            DrawRectangleShape(bounds, rect.IsRounded ? rounded : null, fill.Paint);
        }

        DrawRectangleShadows(bounds, rounded, rect.IsRounded, boxShadows, true);
        DrawRectangleStroke(pen, rect, bounds, rounded);
    }

    /// <inheritdoc/>
    public void DrawRectangle(IExperimentalAcrylicMaterial? material, RoundedRect rect)
    {
        if (material is null || rect.Rect.Height <= 0 || rect.Rect.Width <= 0)
        {
            return;
        }

        CheckLease();
        var rc = rect.Rect.ToSKRect();
        var isRounded = rect.IsRounded;
        SKRoundRect? roundedRectangle = null;
        if (isRounded)
        {
            SetRoundRectangle(rect);
            roundedRectangle = _roundRect;
        }

        _fillPaint.Reset();
        _fillPaint.IsAntialias = true;
        var tint = new SKColor(material.TintColor.R, material.TintColor.G, material.TintColor.B, material.TintColor.A);
        using var backdrop = SKShader.CreateColor(new(material.MaterialColor.R, material.MaterialColor.G, material.MaterialColor.B, material.MaterialColor.A));
        using var tintShader = SKShader.CreateColor(tint);
        using var effectiveTint = SKShader.CreateCompose(backdrop, tintShader);
        _fillPaint.Shader = effectiveTint;
        if (isRounded && roundedRectangle is not null)
        {
            Canvas.DrawRoundRect(roundedRectangle, _fillPaint);
        }
        else
        {
            Canvas.DrawRect(rc, _fillPaint);
        }

        _fillPaint.Reset();
    }

    /// <inheritdoc/>
    public void DrawRegion(IBrush? brush, IPen? pen, IPlatformRenderInterfaceRegion region)
    {
        var r = (SkiaRegionImpl)region;
        if (r.IsEmpty)
        {
            return;
        }

        CheckLease();
        if (brush is null)
        {
            return;
        }

        var bounds = r.Bounds;
        using var fill = CreatePaint(_fillPaint, brush, new(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top));
        Canvas.DrawRegion(r.Region, fill.Paint);
    }

    /// <inheritdoc/>
    public void DrawEllipse(IBrush? brush, IPen? pen, Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        CheckLease();
        var rc = rect.ToSKRect();
        if (brush is not null)
        {
            using var fill = CreatePaint(_fillPaint, brush, rect);
            Canvas.DrawOval(rc, fill.Paint);
        }

        if (pen is not null && TryCreatePaint(_strokePaint, pen, rect.Inflate(new Thickness(pen.Thickness / HalfDivisor))) is { } stroke)
        {
            using (stroke)
            {
                Canvas.DrawOval(rc, stroke.Paint);
            }
        }
    }

    /// <inheritdoc/>
    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun)
    {
        CheckLease();
        if (foreground is null)
        {
            return;
        }

        using var paintWrapper = CreatePaint(_fillPaint, foreground, glyphRun.Bounds);
        var glyphRunImpl = (GlyphRunImpl)glyphRun;
        var effectiveTextOptions = TextOptions;
        if (_disableSubpixelTextRendering)
        {
            var mode = effectiveTextOptions.TextRenderingMode;
            if (mode == TextRenderingMode.SubpixelAntialias
                || (mode == TextRenderingMode.Unspecified && (RenderOptions.EdgeMode == EdgeMode.Antialias || RenderOptions.EdgeMode == EdgeMode.Unspecified)))
            {
                effectiveTextOptions = effectiveTextOptions with
                {
                    TextRenderingMode = TextRenderingMode.Antialias
                };
            }
        }

        var textBlob = glyphRunImpl.GetTextBlob(effectiveTextOptions, RenderOptions);
        Canvas.DrawText(textBlob, (float)glyphRun.BaselineOrigin.X, (float)glyphRun.BaselineOrigin.Y, paintWrapper.Paint);
    }

    /// <inheritdoc/>
    public IDrawingContextLayerImpl CreateLayer(PixelSize size)
    {
        CheckLease();
        return CreateRenderTarget(size, true, false);
    }

    /// <inheritdoc/>
    public void PushClip(Rect clip)
    {
        CheckLease();
        _ = Canvas.Save();
        Canvas.ClipRect(clip.ToSKRect());
    }

    /// <inheritdoc/>
    public void PushClip(RoundedRect clip)
    {
        CheckLease();
        _ = Canvas.Save();
        SetRoundRectangle(clip);
        Canvas.ClipRoundRect(_roundRect, SKClipOperation.Intersect, true);
    }

    /// <inheritdoc/>
    public void PushClip(IPlatformRenderInterfaceRegion region)
    {
        CheckLease();
        _ = Canvas.Save();
        Canvas.ClipRegion(((SkiaRegionImpl)region).Region);
    }

    /// <inheritdoc/>
    public void PopClip()
    {
        CheckLease();
        RestoreCanvas();
    }

    /// <inheritdoc/>
    public void PushLayer(Rect bounds)
    {
        CheckLease();
        _ = Canvas.SaveLayer(bounds.ToSKRect(), null!);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopLayer() =>
PopClip();

    /// <inheritdoc/>
    public void PushOpacity(double opacity, Rect? bounds)
    {
        CheckLease();
        _opacityStack.Push(_currentOpacity);
        var useSaveLayer = _useOpacitySaveLayer || RenderOptions.RequiresFullOpacityHandling == true;
        if (useSaveLayer)
        {
            opacity = _currentOpacity * opacity;
            _currentOpacity = 1.0;
            _layerPaint.Reset();
            _layerPaint.ColorF = new(0, 0, 0, (float)opacity);
            if (bounds.HasValue)
            {
                _ = Canvas.SaveLayer(bounds.Value.ToSKRect(), _layerPaint);
            }
            else
            {
                _ = Canvas.SaveLayer(_layerPaint);
            }
        }
        else
        {
            _currentOpacity *= opacity;
        }
    }

    /// <inheritdoc/>
    public void PopOpacity()
    {
        CheckLease();
        var useSaveLayer = _useOpacitySaveLayer || RenderOptions.RequiresFullOpacityHandling == true;
        if (useSaveLayer)
        {
            RestoreCanvas();
        }

        _currentOpacity = _opacityStack.Pop();
    }

    /// <inheritdoc/>
    public void PushOpacityMask(IBrush mask, Rect bounds)
    {
        CheckLease();
        var rect = bounds.ToSKRect();
        _ = Canvas.SaveLayer(rect, null!);

        // The mask paint is drawn when the mask is popped, so the stack owns it until then.
        var paint = new SKPaint();
        _maskStack.Push((Canvas.TotalMatrix, CreatePaint(paint, mask, bounds), paint));
    }

    /// <inheritdoc/>
    public void PopOpacityMask()
    {
        CheckLease();
        _layerPaint.Reset();
        _layerPaint.BlendMode = SKBlendMode.DstIn;
        _ = Canvas.SaveLayer(_layerPaint);
        var (transform, paintWrapper, owned) = _maskStack.Pop();
        Canvas.SetMatrix(transform);
        using (owned)
        using (paintWrapper)
        {
            Canvas.DrawPaint(paintWrapper.Paint);
        }

        RestoreCanvas();
        RestoreCanvas();
    }

    /// <inheritdoc/>
    public void PushGeometryClip(IGeometryImpl clip)
    {
        CheckLease();
        _ = Canvas.Save();
        Canvas.ClipPath(((GeometryImpl)clip).FillPath, SKClipOperation.Intersect, true);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopGeometryClip() => PopLayer();

    /// <inheritdoc/>
    public void PushRenderOptions(RenderOptions renderOptions)
    {
        CheckLease();
        _renderOptionsStack.Push(RenderOptions);
        RenderOptions = RenderOptions.MergeWith(renderOptions);
    }

    /// <inheritdoc/>
    public void PopRenderOptions()
    {
        CheckLease();
        RenderOptions = _renderOptionsStack.Pop();
    }

    /// <inheritdoc/>
    public void PushTextOptions(TextOptions textOptions)
    {
        CheckLease();
        _textOptionsStack.Push(TextOptions);
        TextOptions = TextOptions.MergeWith(textOptions);
    }

    /// <inheritdoc/>
    public void PopTextOptions()
    {
        CheckLease();
        TextOptions = _textOptionsStack.Pop();
    }

    /// <inheritdoc/>
    public void PushEffect(Rect? clipRect, IEffect effect)
    {
        CheckLease();
        using var filter = CreateEffect(effect);
        using var paint = new SKPaint { ImageFilter = filter };
        if (clipRect.HasValue)
        {
            _ = Canvas.SaveLayer(clipRect.Value.ToSKRect(), paint);
        }
        else
        {
            _ = Canvas.SaveLayer(paint);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopEffect() => PopGeometryClip();

    /// <inheritdoc/>
    public object? GetFeature(Type t) => t == typeof(ISkiaSharpApiLeaseFeature) ? new SkiaLeaseFeature(this) : null;

    /// <inheritdoc/>
    public void Dispose()
    {
        CheckLease();
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _strokePaint.Dispose();
            _fillPaint.Dispose();
            _boxShadowPaint.Dispose();
            _bitmapPaint.Dispose();
            _layerPaint.Dispose();
            _roundRect.Dispose();
            while (_maskStack.TryPop(out var mask))
            {
                mask.Paint.Dispose();
                mask.Owned.Dispose();
            }

            if (_graphicsContext is not null)
            {
                Monitor.Exit(_graphicsContext);
                _graphicsContext = null;
            }

            if (_disposables is not null)
            {
                foreach (var disposable in _disposables)
                {
                    disposable?.Dispose();
                }

                _disposables = null;
            }
        }
        finally
        {
            Volatile.Write(ref _disposed, 1);
        }
    }

    /// <summary>Configures the session paint for a brush.</summary>
    /// <param name = "paint">The paint.</param>
    /// <param name = "brush">The brush.</param>
    /// <param name = "targetRect">The target rect.</param>
    /// <returns>The requested result.</returns>
    internal PaintWrapper CreatePaint(SKPaint paint, IBrush brush, in Rect targetRect)
    {
        paint.Reset();
        paint.IsAntialias = RenderOptions.EdgeMode != EdgeMode.Aliased;
        var opacity = brush.Opacity * (_useOpacitySaveLayer ? 1.0 : _currentOpacity);
        if (brush is ISolidColorBrush solid)
        {
            paint.Color = new(solid.Color.R, solid.Color.G, solid.Color.B, (byte)(solid.Color.A * opacity));
            return new(paint);
        }

        paint.Color = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(byte.MaxValue * opacity));
        if (brush is IGradientBrush gradient)
        {
            ConfigureGradientPaint(paint, gradient, targetRect);
            return new(paint);
        }

        if (brush is ISceneBrush sceneBrush)
        {
            using var content = sceneBrush.CreateContent();
            if (content is not null)
            {
                return ConfigureSceneBrushContent(paint, content, targetRect);
            }

            paint.Color = default;
            return new(paint);
        }

        if (brush is ISceneBrushContent sceneBrushContent)
        {
            return ConfigureSceneBrushContent(paint, sceneBrushContent, targetRect);
        }

        paint.Color = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0);
        return new(paint);
    }

    /// <summary>Creates a shader for the brush bounds.</summary>
    /// <param name="paint">The paint that retains the shader.</param>
    /// <param name = "gradientBrush">The gradient brush.</param>
    /// <param name = "targetRect">The target rect.</param>
    /// <exception cref="NotSupportedException">The gradient type is unsupported.</exception>
    private static void ConfigureGradientPaint(SKPaint paint, IGradientBrush gradientBrush, in Rect targetRect)
    {
        var count = gradientBrush.GradientStops.Count;
        if (count == 0)
        {
            paint.Color = SKColors.Transparent;
            return;
        }

        var mode = gradientBrush.SpreadMethod.ToSKShaderTileMode();
        Span<SKColor> colors = count <= MaxStackAllocCount ? stackalloc SKColor[count] : new SKColor[count];
        Span<float> offsets = count <= MaxStackAllocCount ? stackalloc float[count] : new float[count];
        for (var i = 0; i < count; i++)
        {
            var stop = gradientBrush.GradientStops[i];
            colors[i] = stop.Color.ToSKColor();
            offsets[i] = (float)stop.Offset;
        }

        var matrix = SKMatrix.Identity;
        if (gradientBrush.Transform is { } transform)
        {
            var origin = gradientBrush.TransformOrigin.ToPixels(targetRect);
            var translation = Matrix.CreateTranslation(origin);
            matrix = (-translation * transform.Value * translation).ToSKMatrix();
        }

        if (gradientBrush is IConicGradientBrush conic)
        {
            matrix = SKMatrix.Concat(matrix, SKMatrix.CreateRotationDegrees((float)conic.Angle));
            NativeMethods.AssignShader(paint, NativeMethods.Sweep(conic.Center.ToPixels(targetRect).ToSKPoint(), colors, offsets, mode, ref matrix));
            return;
        }

        var shader = gradientBrush switch
        {
            ILinearGradientBrush linear => NativeMethods.Linear(
                linear.StartPoint.ToPixels(targetRect).ToSKPoint(),
                linear.EndPoint.ToPixels(targetRect).ToSKPoint(),
                colors,
                offsets,
                mode,
                ref matrix),
            IRadialGradientBrush radial => NativeMethods.Radial(
                radial.Center.ToPixels(targetRect).ToSKPoint(),
                (float)radial.RadiusX.ToValue(targetRect.Width),
                colors,
                offsets,
                mode,
                ref matrix),
            _ => throw new NotSupportedException("Unsupported gradient brush type."),
        };
        NativeMethods.AssignShader(paint, shader);
    }

    /// <summary>Converts a blur radius to Skia's standard deviation.</summary>
    /// <param name = "radius">The radius.</param>
    /// <returns>The requested result.</returns>
    private static float SkBlurRadiusToSigma(double radius) => radius <= 0 ? 0.0F : (BlurSigmaMultiplier * (float)radius) + BlurSigmaOffset;

    /// <summary>Draws a rectangle outline using the session stroke paint.</summary>
    /// <param name="pen">The outline pen.</param>
    /// <param name="rect">The rectangle.</param>
    /// <param name="bounds">The pixel bounds.</param>
    /// <param name="rounded">The prepared corner radii.</param>
    private void DrawRectangleStroke(IPen? pen, RoundedRect rect, SKRect bounds, SKRoundRect? rounded)
    {
        if (pen is not null && TryCreatePaint(_strokePaint, pen, rect.Rect.Inflate(new Thickness(pen.Thickness / HalfDivisor))) is { } stroke)
        {
            using (stroke)
            {
                DrawRectangleShape(bounds, rect.IsRounded ? rounded : null, stroke.Paint);
            }
        }
    }

    /// <summary>Draws the fill and stroke paths of a geometry.</summary>
    /// <param name="brush">The fill brush.</param>
    /// <param name="pen">The stroke pen.</param>
    /// <param name="geometry">The path geometry.</param>
    private void DrawPathGeometry(IBrush? brush, IPen? pen, GeometryImpl geometry)
    {
        if (brush is not null && geometry.FillPath is { } fillPath)
        {
            using var fill = CreatePaint(_fillPaint, brush, geometry.Bounds);
            Canvas.DrawPath(fillPath, fill.Paint);
        }

        if (pen is not null && geometry.StrokePath is { } strokePath && TryCreatePaint(_strokePaint, pen, geometry.Bounds.Inflate(new Thickness(pen.Thickness / HalfDivisor))) is { } stroke)
        {
            using (stroke)
            {
                Canvas.DrawPath(strokePath, stroke.Paint);
            }
        }
    }

    /// <summary>Rejects access while the drawing API is leased.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <c>_leased</c>.</exception>
    private void CheckLease()
    {
        if (_leased)
        {
            throw new InvalidOperationException("The underlying graphics API is currently leased.");
        }
    }

    /// <summary>Restores the saved canvas and invalidates the transform cache.</summary>
    private void RestoreCanvas()
    {
        _currentTransform = null;
        Canvas.Restore();
    }

    /// <summary>Configures the session paint for a visible pen.</summary>
    /// <param name = "paint">The paint.</param>
    /// <param name = "pen">The pen.</param>
    /// <param name = "targetRect">The target rect.</param>
    /// <returns>The requested result.</returns>
    private PaintWrapper? TryCreatePaint(SKPaint paint, IPen pen, in Rect targetRect)
    {
        if (pen.Brush is not { } brush || pen.Thickness == 0D)
        {
            return null;
        }

        var wrapper = CreatePaint(paint, brush, targetRect);
        paint.IsStroke = true;
        paint.StrokeWidth = (float)pen.Thickness;
        paint.StrokeCap = pen.LineCap.ToSKStrokeCap();
        paint.StrokeJoin = pen.LineJoin.ToSKStrokeJoin();
        paint.StrokeMiter = (float)pen.MiterLimit;
        if (pen.DashStyle?.Dashes is { Count: > 0 } srcDashes)
        {
            const int dashPairSize = 2;
            var count = srcDashes.Count % dashPairSize == 0 ? srcDashes.Count : srcDashes.Count * dashPairSize;
            var dashesArray = new float[count];
            for (var i = 0; i < count; ++i)
            {
                dashesArray[i] = (float)srcDashes[i % srcDashes.Count] * (float)pen.Thickness;
            }

            var offset = (float)(pen.DashStyle.Offset * pen.Thickness);
            var dashEffect = SKPathEffect.CreateDash(dashesArray, offset);
            paint.PathEffect = dashEffect;
            return new PaintWrapper(paint, wrapper.Disposable1, wrapper.Disposable2, dashEffect);
        }

        return wrapper;
    }

    /// <summary>Draws one clipped box shadow.</summary>
    /// <param name = "rc">The rc.</param>
    /// <param name = "roundedRectangle">The sk round rect.</param>
    /// <param name = "isRounded">The is rounded.</param>
    /// <param name = "boxShadow">The box shadow.</param>
    /// <param name = "isInset">The is inset.</param>
    private void DrawBoxShadow(SKRect rc, SKRoundRect? roundedRectangle, bool isRounded, BoxShadow boxShadow, bool isInset)
    {
        var spread = (float)boxShadow.Spread;
        if (isInset)
        {
            spread = -spread;
        }

        var sigma = SkBlurRadiusToSigma(boxShadow.Blur);
        using var filter = SKImageFilter.CreateBlur(sigma, sigma);
        var color = boxShadow.Color.ToSKColor();
        color = new(color.Red, color.Green, color.Blue, (byte)(color.Alpha * _currentOpacity));
        _boxShadowPaint.Reset();
        _boxShadowPaint.IsAntialias = true;
        _boxShadowPaint.Color = color;
        _boxShadowPaint.ImageFilter = filter;
        var clipOp = isInset ? SKClipOperation.Intersect : SKClipOperation.Difference;
        _ = Canvas.Save();
        if (isRounded && roundedRectangle is not null)
        {
            using var shadowRect = new SKRoundRect(roundedRectangle);
            if (spread != 0)
            {
                shadowRect.Inflate(spread, spread);
            }

            Canvas.ClipRoundRect(roundedRectangle, clipOp, true);
            var oldTransform = Transform;
            Transform = oldTransform * Matrix.CreateTranslation(boxShadow.OffsetX, boxShadow.OffsetY);
            Canvas.DrawRoundRect(shadowRect, _boxShadowPaint);
            Transform = oldTransform;
        }
        else
        {
            var shadowRect = rc;
            if (spread != 0)
            {
                shadowRect.Inflate(spread, spread);
            }

            Canvas.ClipRect(rc, clipOp);
            var oldTransform = Transform;
            Transform = oldTransform * Matrix.CreateTranslation(boxShadow.OffsetX, boxShadow.OffsetY);
            Canvas.DrawRect(shadowRect, _boxShadowPaint);
            Transform = oldTransform;
        }

        RestoreCanvas();
    }

    /// <summary>Creates the image filter for a supported effect.</summary>
    /// <param name = "effect">The effect.</param>
    /// <returns>The requested result.</returns>
    private SKImageFilter? CreateEffect(IEffect effect)
    {
        if (effect is IBlurEffect blur)
        {
            if (blur.Radius <= 0)
            {
                return null;
            }

            var sigma = SkBlurRadiusToSigma(blur.Radius);
            return SKImageFilter.CreateBlur(sigma, sigma);
        }

        if (effect is IDropShadowEffect drop)
        {
            var sigma = drop.BlurRadius > 0 ? SkBlurRadiusToSigma(drop.BlurRadius) : 0;
            var alpha = drop.Color.A * drop.Opacity;
            if (!_useOpacitySaveLayer)
            {
                alpha *= _currentOpacity;
            }

            var color = new SKColor(drop.Color.R, drop.Color.G, drop.Color.B, (byte)Math.Clamp(alpha, 0, byte.MaxValue));
            return SKImageFilter.CreateDropShadow((float)drop.OffsetX, (float)drop.OffsetY, sigma, sigma, color);
        }

        return null;
    }

    /// <summary>Renders brush content into a tiled shader.</summary>
    /// <param name = "paint">The paint.</param>
    /// <param name = "targetBox">The target box.</param>
    /// <param name = "tileBrush">The tile brush.</param>
    /// <param name = "tileBrushImage">The tile brush image.</param>
    /// <returns>The requested result.</returns>
    private PaintWrapper ConfigureTileBrush(SKPaint paint, in Rect targetBox, ITileBrush tileBrush, SurfaceRenderTarget tileBrushImage)
    {
        var calc = new TileBrushCalculator(tileBrush, tileBrushImage.PixelSize.ToSizeWithDpi(_intermediateSurfaceDpi), targetBox.Size);
        var intermediate = CreateRenderTarget(PixelSize.FromSizeWithDpi(calc.IntermediateSize, _intermediateSurfaceDpi), false, true);
        using (var context = intermediate.CreateDrawingContext())
        {
            var sourceRect = new Rect(tileBrushImage.PixelSize.ToSizeWithDpi(DpiScaleFactor));
            var targetRect = new Rect(tileBrushImage.PixelSize.ToSizeWithDpi(_intermediateSurfaceDpi));
            context.Clear(Colors.Transparent);
            context.PushClip(calc.IntermediateClip);
            context.PushRenderOptions(RenderOptions);
            context.Transform = calc.IntermediateTransform;
            context.DrawBitmap(tileBrushImage, 1.0, sourceRect, targetRect);
            context.PopRenderOptions();
            context.PopClip();
        }

        var tileTransform = tileBrush.TileMode != TileMode.None ? SKMatrix.CreateTranslation(-(float)calc.DestinationRect.X, -(float)calc.DestinationRect.Y) : SKMatrix.CreateIdentity();
        var tileX = tileBrush.TileMode switch
        {
            TileMode.None => SKShaderTileMode.Decal,
            TileMode.FlipX or TileMode.FlipXY => SKShaderTileMode.Mirror,
            _ => SKShaderTileMode.Repeat,
        };
        var tileY = tileBrush.TileMode switch
        {
            TileMode.None => SKShaderTileMode.Decal,
            TileMode.FlipY or TileMode.FlipXY => SKShaderTileMode.Mirror,
            _ => SKShaderTileMode.Repeat,
        };
        var image = intermediate.SnapshotImage();
        var scaleMatrix = SKMatrix.CreateScale((float)(DpiScaleFactor / _intermediateSurfaceDpi.X), (float)(DpiScaleFactor / _intermediateSurfaceDpi.Y));
        var paintTransform = SKMatrix.Concat(tileTransform, scaleMatrix);
        if (tileBrush.DestinationRect.Unit == RelativeUnit.Relative)
        {
            paintTransform = paintTransform.PreConcat(SKMatrix.CreateTranslation((float)targetBox.X, (float)targetBox.Y));
        }

        if (tileBrush.Transform is { } t)
        {
            var origin = tileBrush.TransformOrigin.ToPixels(targetBox);
            var offset = Matrix.CreateTranslation(origin);
            paintTransform = paintTransform.PostConcat((-offset * t.Value * offset).ToSKMatrix());
        }

        var shader = image.ToShader(tileX, tileY, paintTransform);
        paint.Shader = shader;
        return new(paint, intermediate, image, shader);
    }

    /// <summary>Renders scene content for the brush bounds.</summary>
    /// <param name = "paint">The paint.</param>
    /// <param name = "content">The content.</param>
    /// <param name = "targetRect">The target rect.</param>
    /// <returns>The requested result.</returns>
    private PaintWrapper ConfigureSceneBrushContent(SKPaint paint, ISceneBrushContent content, in Rect targetRect)
    {
        var rect = content.Rect;
        var intermediateSize = rect.Size;
        if (intermediateSize.Width <= 0 || intermediateSize.Height <= 0)
        {
            paint.Color = default;
            return new(paint);
        }

        using var intermediate = CreateRenderTarget(PixelSize.FromSizeWithDpi(intermediateSize, _intermediateSurfaceDpi), false, true);
        using (var ctx = intermediate.CreateDrawingContext())
        {
            ctx.PushRenderOptions(RenderOptions);
            ctx.Clear(Colors.Transparent);
            content.Render(ctx, rect.TopLeft == default ? null : Matrix.CreateTranslation(-rect.X, -rect.Y));
            ctx.PopRenderOptions();
        }

        return ConfigureTileBrush(paint, targetRect, content.Brush, intermediate);
    }

    /// <summary>Creates an intermediate surface for this session.</summary>
    /// <param name = "pixelSize">The pixel size.</param>
    /// <param name = "isLayer">The is layer.</param>
    /// <param name = "useScaledDrawing">The use scaled drawing.</param>
    /// <param name = "format">The format.</param>
    /// <returns>The requested result.</returns>
    private SurfaceRenderTarget CreateRenderTarget(PixelSize pixelSize, bool isLayer, bool useScaledDrawing, PixelFormat? format = null)
    {
        var createInfo = new SurfaceRenderTarget.CreateInfo
        {
            Width = pixelSize.Width,
            Height = pixelSize.Height,
            Dpi = _intermediateSurfaceDpi,
            Format = format,
            DisableTextLcdRendering = !isLayer || _disableSubpixelTextRendering,
            GrContext = _graphicsContext,
            Gpu = _gpu,
            Session = _session,
            DisableManualFbo = !isLayer,
            UseScaledDrawing = useScaledDrawing,
        };
        return new(createInfo);
    }

    /// <summary>Sets a solid fill without creating shader resources.</summary>
    /// <param name="brush">The fill brush.</param>
    /// <param name="pen">The outline pen.</param>
    /// <param name="rect">The rectangle.</param>
    /// <param name="shadows">The box shadows.</param>
    /// <returns>Whether the rectangle used the solid fill path.</returns>
    private bool TryDrawSolidRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows shadows)
    {
        if (brush is ISolidColorBrush solid && pen is null && !rect.IsRounded && shadows.Count == 0)
        {
            SetSolidPaint(_fillPaint, solid);
            Canvas.DrawRect(rect.Rect.ToSKRect(), _fillPaint);
            return true;
        }

        return false;
    }

    /// <summary>Sets a solid fill without creating shader resources.</summary>
    /// <param name="paint">The session paint.</param>
    /// <param name="brush">The solid brush.</param>
    private void SetSolidPaint(SKPaint paint, ISolidColorBrush brush)
    {
        paint.Reset();
        paint.IsAntialias = RenderOptions.EdgeMode != EdgeMode.Aliased;
        var opacity = brush.Opacity * (_useOpacitySaveLayer ? 1.0 : _currentOpacity);
        paint.Color = new(brush.Color.R, brush.Color.G, brush.Color.B, (byte)(brush.Color.A * opacity));
    }

    /// <summary>Prepares corner radii only for rounded rectangles or inset shadows.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="hasInsetShadows">Whether an inset shadow needs rounded bounds.</param>
    /// <returns>The reusable round rectangle, when needed.</returns>
    private SKRoundRect? PrepareRoundedRectangle(RoundedRect rect, bool hasInsetShadows)
    {
        if (!rect.IsRounded && !hasInsetShadows)
        {
            return null;
        }

        SetRoundRectangle(rect);
        return _roundRect;
    }

    /// <summary>Sets the session's rounded bounds using stack-allocated corner radii.</summary>
    /// <param name="rect">The rounded rectangle.</param>
    private void SetRoundRectangle(RoundedRect rect)
    {
        ReadOnlySpan<SKPoint> radii = [rect.RadiiTopLeft.ToSKPoint(), rect.RadiiTopRight.ToSKPoint(), rect.RadiiBottomRight.ToSKPoint(), rect.RadiiBottomLeft.ToSKPoint()];
        _roundRect.SetRectRadii(rect.Rect.ToSKRect(), radii);
    }

    /// <summary>Draws the rectangle with the prepared paint.</summary>
    /// <param name="bounds">The pixel bounds.</param>
    /// <param name="rounded">The corner radii, when present.</param>
    /// <param name="paint">The prepared paint.</param>
    private void DrawRectangleShape(SKRect bounds, SKRoundRect? rounded, SKPaint paint)
    {
        if (rounded is not null)
        {
            Canvas.DrawRoundRect(rounded, paint);
        }
        else
        {
            Canvas.DrawRect(bounds, paint);
        }
    }

    /// <summary>Draws the outer or inset shadows for a rectangle.</summary>
    /// <param name="bounds">The pixel bounds.</param>
    /// <param name="rounded">The corner radii, when present.</param>
    /// <param name="isRounded">Whether the rectangle has rounded corners.</param>
    /// <param name="shadows">The box shadows.</param>
    /// <param name="inset">Whether to draw inset shadows.</param>
    private void DrawRectangleShadows(SKRect bounds, SKRoundRect? rounded, bool isRounded, BoxShadows shadows, bool inset)
    {
        foreach (var shadow in shadows)
        {
            if (shadow != default && shadow.IsInset == inset)
            {
                DrawBoxShadow(bounds, rounded, isRounded, shadow, inset);
            }
        }
    }

    /// <summary>Structure holding creation information for a drawing context.</summary>
    internal readonly record struct CreateInfo
    {
        /// <summary>Gets the target canvas.</summary>
        public SKCanvas? Canvas { get; init; }

        /// <summary>Gets the target surface.</summary>
        public SKSurface? Surface { get; init; }

        /// <summary>Gets whether drawing should scale to DPI.</summary>
        public bool ScaleDrawingToDpi { get; init; }

        /// <summary>Gets the DPI of the drawing context.</summary>
        public Vector Dpi { get; init; }

        /// <summary>Gets whether subpixel text rendering is disabled.</summary>
        public bool DisableSubpixelTextRendering { get; init; }

        /// <summary>Gets the Skia GPU context if available.</summary>
        public GRContext? GrContext { get; init; }

        /// <summary>Gets the pixel channel order of the target surface.</summary>
        public SKColorType? SurfaceColorType { get; init; }

        /// <summary>Gets the Skia GPU platform provider if available.</summary>
        public ISkiaGpu? Gpu { get; init; }

        /// <summary>Gets the current GPU render session if available.</summary>
        public ISkiaGpuRenderSession? CurrentSession { get; init; }
    }

    /// <summary>Owns temporary brush resources while borrowing a session paint.</summary>
    internal readonly record struct PaintWrapper : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="PaintWrapper"/> struct.</summary>
        /// <param name="paint">The session paint.</param>
        /// <param name="d1">The first temporary resource.</param>
        /// <param name="d2">The second temporary resource.</param>
        /// <param name="d3">The third temporary resource.</param>
        public PaintWrapper(SKPaint paint, IDisposable? d1 = null, IDisposable? d2 = null, IDisposable? d3 = null)
        {
            Paint = paint;
            Disposable1 = d1;
            Disposable2 = d2;
            Disposable3 = d3;
        }

        /// <summary>Gets the paint.</summary>
        public SKPaint Paint { get; }

        /// <summary>Gets the disposable1.</summary>
        public IDisposable? Disposable1 { get; }

        /// <summary>Gets the disposable2.</summary>
        public IDisposable? Disposable2 { get; }

        /// <summary>Gets the disposable3.</summary>
        public IDisposable? Disposable3 { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Paint.Reset();
            Disposable1?.Dispose();
            Disposable2?.Dispose();
            Disposable3?.Dispose();
        }
    }

    /// <summary>Owns the skia lease feature.</summary>
    /// <param name = "context">The context.</param>
    private sealed class SkiaLeaseFeature(DrawingContextImpl context) : ISkiaSharpApiLeaseFeature
    {
        /// <summary>The context.</summary>
        private readonly DrawingContextImpl _context = context;

        /// <inheritdoc/>
        public ISkiaSharpApiLease Lease()
        {
            _context.CheckLease();
            return new ApiLease(_context);
        }

        /// <summary>Owns the api lease.</summary>
        private sealed class ApiLease : ISkiaSharpApiLease
        {
            /// <summary>The context.</summary>
            private readonly DrawingContextImpl _context;

            /// <summary>The revert transform.</summary>
            private readonly SKMatrix _revertTransform;

            /// <summary>The is disposed.</summary>
            private int _isDisposed;

            /// <summary>Initializes a new instance of the <see cref = "ApiLease"/> class.</summary>
            /// <param name = "context">The context.</param>
            public ApiLease(DrawingContextImpl context)
            {
                _context = context;
                _revertTransform = context.Canvas.TotalMatrix;
                _context._leased = true;
            }

            /// <summary>Gets the sk canvas.</summary>
            public SKCanvas SkCanvas => CheckLease(_context.Canvas);

            /// <summary>Gets the gr context.</summary>
            public GRContext? GrContext => _context.GrContext;

            /// <summary>Gets the surface channel order.</summary>
            public SKColorType SurfaceColorType => _context.SurfaceColorType;

            /// <summary>Gets the sk surface.</summary>
            public SKSurface? SkSurface => CheckLease(_context.Surface);

            /// <summary>Gets the current opacity.</summary>
            public double CurrentOpacity => CheckLease(_context._currentOpacity);

            /// <inheritdoc/>
            public ISkiaSharpPlatformGraphicsApiLease? TryLeasePlatformGraphicsApi()
            {
                _ = CheckLease(true);
                return _context._gpu?.PlatformGraphicsContext is { } context ? new PlatformApiLease(this, context) : null;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                {
                    return;
                }

                _context.Canvas.SetMatrix(_revertTransform);
                _context._leased = false;
            }

            /// <summary>Rejects access while the drawing API is leased.</summary>
            /// <typeparam name="T">The leased value type.</typeparam>
            /// <param name = "value">The value.</param>
            /// <returns>The requested result.</returns>
            private T CheckLease<T>(T value)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
                return value;
            }

            /// <summary>Owns the platform api lease.</summary>
            private sealed class PlatformApiLease : ISkiaSharpPlatformGraphicsApiLease
            {
                /// <summary>The parent.</summary>
                private readonly ApiLease _parent;

                /// <summary>Initializes a new instance of the <see cref = "PlatformApiLease"/> class.</summary>
                /// <param name = "parent">The parent.</param>
                /// <param name = "context">The context.</param>
                public PlatformApiLease(ApiLease parent, IPlatformGraphicsContext context)
                {
                    _parent = parent;
                    _parent.GrContext?.Flush();
                    Context = context;
                }

                /// <summary>Gets the context.</summary>
                public IPlatformGraphicsContext Context { get; }

                /// <inheritdoc/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Dispose() => _parent.GrContext?.ResetContext();
            }
        }
    }
}

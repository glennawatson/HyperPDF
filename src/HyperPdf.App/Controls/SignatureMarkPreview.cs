// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HyperPdf.Core.Annotations;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Controls;

/// <summary>Shows a signature or initials mark as it will look on the page, scaled to fit.</summary>
[DebuggerDisplay("SignatureMarkPreview: {Mark}")]
public sealed class SignatureMarkPreview : Control
{
    /// <summary>The mark shown, or <see langword="null"/> for an empty preview.</summary>
    public static readonly StyledProperty<SignatureMark?> MarkProperty = AvaloniaProperty.Register<SignatureMarkPreview, SignatureMark?>(nameof(Mark));

    /// <summary>The ink width in device independent pixels.</summary>
    private const double InkWidth = 2;

    /// <summary>How far the mark sits inside the preview's edge.</summary>
    private const double Inset = 8;

    /// <summary>The ink brush, the colour signatures are written in.</summary>
    private static readonly IImmutableSolidColorBrush InkBrush = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink));

    /// <summary>The pen for drawn strokes.</summary>
    private static readonly IPen InkPen = new ImmutablePen(InkBrush, InkWidth, lineCap: PenLineCap.Round);

    /// <summary>Draws the mark, while shown.</summary>
    private SignatureMarkPainter? _painter;

    /// <summary>Initializes static members of the <see cref="SignatureMarkPreview"/> class.</summary>
    static SignatureMarkPreview() => AffectsRender<SignatureMarkPreview>(MarkProperty);

    /// <summary>Initializes a new instance of the <see cref="SignatureMarkPreview"/> class.</summary>
    public SignatureMarkPreview() => _ = this.Events().DetachedFromVisualTree.SubscribeSafe(_ => Release(), OnError);

    /// <summary>Gets or sets the mark shown.</summary>
    public SignatureMark? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Mark is not { IsValid: true } mark)
        {
            return;
        }

        var area = new Rect(Bounds.Size).Deflate(Inset);
        (_painter ??= new()).Draw(context, mark, SignaturePad.Fit(mark, area), InkBrush, InkPen);
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Frees the cached picture once the preview is no longer shown.</summary>
    private void Release()
    {
        _painter?.Dispose();
        _painter = null;
    }
}

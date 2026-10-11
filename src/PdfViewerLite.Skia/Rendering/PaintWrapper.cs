// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace PdfViewerLite.Skia.Rendering;

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

    /// <summary>Gets the first temporary resource.</summary>
    public IDisposable? Disposable1 { get; }

    /// <summary>Gets the second temporary resource.</summary>
    public IDisposable? Disposable2 { get; }

    /// <summary>Gets the third temporary resource.</summary>
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

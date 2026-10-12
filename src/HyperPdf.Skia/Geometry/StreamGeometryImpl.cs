// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia.Geometry;

/// <summary>Modern stream geometry implementation utilizing SkiaSharp 4's <see cref = "SKPathBuilder"/>.</summary>
internal sealed class StreamGeometryImpl : GeometryImpl, IStreamGeometryImpl, IDisposable
{
    /// <summary>Precomputed or cached bounds.</summary>
    private Rect _bounds;

    /// <summary>The stroke path.</summary>
    private SKPath _strokePath;

    /// <summary>The fill path, if distinct from stroke.</summary>
    private SKPath? _fillPath;

    /// <summary>Initializes a new instance of the <see cref = "StreamGeometryImpl"/> class with specified paths.</summary>
    /// <param name = "stroke">The stroke path.</param>
    /// <param name = "fill">The fill path.</param>
    /// <param name = "bounds">Optional precomputed bounds.</param>
    /// <exception cref = "ArgumentNullException">Thrown when <c>stroke</c> is <see langword="null"/>.</exception>
    public StreamGeometryImpl(SKPath stroke, SKPath? fill, Rect? bounds = null)
    {
        _strokePath = stroke ?? throw new ArgumentNullException(nameof(stroke));
        _fillPath = fill;
        _bounds = bounds ?? stroke.TightBounds.ToAvaloniaRect();
    }

    /// <summary>Initializes a new instance of the <see cref = "StreamGeometryImpl"/> class.</summary>
    public StreamGeometryImpl()
    {
        _strokePath = new SKPath { FillType = SKPathFillType.EvenOdd };
        _fillPath = _strokePath;
        _bounds = default;
    }

    /// <inheritdoc/>
    public override Rect Bounds => _bounds;

    /// <inheritdoc/>
    public override SKPath StrokePath => _strokePath;

    /// <inheritdoc/>
    public override SKPath? FillPath => _fillPath;

    /// <inheritdoc/>
    public IStreamGeometryImpl Clone()
    {
        var stroke = new SKPath(_strokePath);
        var fill = stroke;
        if (!ReferenceEquals(_fillPath, _strokePath))
        {
            fill = _fillPath is { } original ? new SKPath(original) : null;
        }

        return new StreamGeometryImpl(stroke, fill, Bounds);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IStreamGeometryContextImpl Open() => new StreamContext(this);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!ReferenceEquals(_strokePath, _fillPath))
        {
            _fillPath?.Dispose();
        }

        _strokePath.Dispose();
    }

    /// <summary>Stream context implementation writing commands into an <see cref = "SKPathBuilder"/>.</summary>
    /// <param name = "geometry">The parent geometry.</param>
    private sealed class StreamContext(StreamGeometryImpl geometry) : IStreamGeometryContextImpl
    {
        /// <summary>Path builder for stroke path.</summary>
        private readonly SKPathBuilder _strokeBuilder = new();

        /// <summary>Path builder for fill path, if separate.</summary>
        private SKPathBuilder? _fillBuilder;

        /// <summary>The start point of the current figure.</summary>
        private Point _startPoint;

        /// <summary>Whether the current figure is filled.</summary>
        private bool _isFilled;

        /// <summary>Whether the figure is broken (has non-stroked segments).</summary>
        private bool _isFigureBroken;

        /// <summary>The fill type.</summary>
        private SKPathFillType _fillType = SKPathFillType.EvenOdd;

        /// <summary>Gets the parent geometry.</summary>
        private StreamGeometryImpl Geometry { get; } = geometry;

        /// <summary>Gets a value indicating whether fill operations should be duplicated to a separate builder.</summary>
        private bool Duplicate => _isFilled && _fillBuilder is not null;

        /// <inheritdoc/>
        public void BeginFigure(Point startPoint, bool isFilled)
        {
            _startPoint = startPoint;
            _isFilled = isFilled;
            _isFigureBroken = false;
            if (!isFilled)
            {
                EnsureSeparateFill();
            }

            _strokeBuilder.MoveTo((float)startPoint.X, (float)startPoint.Y);
            if (Duplicate)
            {
                _fillBuilder?.MoveTo((float)startPoint.X, (float)startPoint.Y);
            }
        }

        /// <inheritdoc/>
        public void EndFigure(bool isClosed)
        {
            if (!isClosed)
            {
                return;
            }

            if (_isFigureBroken)
            {
                _strokeBuilder.LineTo((float)_startPoint.X, (float)_startPoint.Y);
                _isFigureBroken = false;
            }
            else
            {
                _strokeBuilder.Close();
            }

            if (Duplicate)
            {
                _fillBuilder?.Close();
            }
        }

        /// <inheritdoc/>
        public void SetFillRule(FillRule fillRule) => _fillType = fillRule == FillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;

        /// <inheritdoc/>
        public void LineTo(Point point, bool isStroked = true)
        {
            if (isStroked)
            {
                _strokeBuilder.LineTo((float)point.X, (float)point.Y);
            }
            else
            {
                EnsureSeparateFill();
                _isFigureBroken = true;
                _strokeBuilder.MoveTo((float)point.X, (float)point.Y);
            }

            if (Duplicate)
            {
                _fillBuilder?.LineTo((float)point.X, (float)point.Y);
            }
        }

        /// <inheritdoc/>
        public void ArcTo(Point point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection, bool isStroked = true)
        {
            var arc = isLargeArc ? SKPathArcSize.Large : SKPathArcSize.Small;
            var sweep = sweepDirection == SweepDirection.Clockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise;
            if (isStroked)
            {
                _strokeBuilder.ArcTo((float)size.Width, (float)size.Height, (float)rotationAngle, arc, sweep, (float)point.X, (float)point.Y);
            }
            else
            {
                EnsureSeparateFill();
                _isFigureBroken = true;
                _strokeBuilder.MoveTo((float)point.X, (float)point.Y);
            }

            if (Duplicate)
            {
                _fillBuilder?.ArcTo((float)size.Width, (float)size.Height, (float)rotationAngle, arc, sweep, (float)point.X, (float)point.Y);
            }
        }

        /// <inheritdoc/>
        public void CubicBezierTo(Point controlPoint1, Point controlPoint2, Point endPoint, bool isStroked = true)
        {
            if (isStroked)
            {
                _strokeBuilder.CubicTo((float)controlPoint1.X, (float)controlPoint1.Y, (float)controlPoint2.X, (float)controlPoint2.Y, (float)endPoint.X, (float)endPoint.Y);
            }
            else
            {
                EnsureSeparateFill();
                _isFigureBroken = true;
                _strokeBuilder.MoveTo((float)endPoint.X, (float)endPoint.Y);
            }

            if (Duplicate)
            {
                _fillBuilder?.CubicTo((float)controlPoint1.X, (float)controlPoint1.Y, (float)controlPoint2.X, (float)controlPoint2.Y, (float)endPoint.X, (float)endPoint.Y);
            }
        }

        /// <inheritdoc/>
        public void QuadraticBezierTo(Point controlPoint, Point endPoint, bool isStroked = true)
        {
            if (isStroked)
            {
                _strokeBuilder.QuadTo((float)controlPoint.X, (float)controlPoint.Y, (float)endPoint.X, (float)endPoint.Y);
            }
            else
            {
                EnsureSeparateFill();
                _isFigureBroken = true;
                _strokeBuilder.MoveTo((float)endPoint.X, (float)endPoint.Y);
            }

            if (Duplicate)
            {
                _fillBuilder?.QuadTo((float)controlPoint.X, (float)controlPoint.Y, (float)endPoint.X, (float)endPoint.Y);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _strokeBuilder.FillType = _fillType;
            var stroke = _strokeBuilder.Detach();
            SKPath? fill = null;
            if (_fillBuilder is not null)
            {
                _fillBuilder.FillType = _fillType;
                fill = _fillBuilder.Detach();
            }
            else
            {
                fill = stroke;
            }

            Geometry.Dispose();
            Geometry._strokePath = stroke;
            Geometry._fillPath = fill;
            Geometry._bounds = stroke.TightBounds.ToAvaloniaRect();
            _strokeBuilder.Dispose();
            _fillBuilder?.Dispose();
        }

        /// <summary>Ensures separate fill builder exists when non-stroked segments occur.</summary>
        private void EnsureSeparateFill()
        {
            if (_fillBuilder is not null)
            {
                return;
            }

            using var prefix = _strokeBuilder.Snapshot();
            _fillBuilder = new(prefix) { FillType = _fillType };
        }
    }
}

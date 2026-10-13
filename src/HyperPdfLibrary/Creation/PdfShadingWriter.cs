// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Adds axial and radial shadings (ISO 32000-2 8.7.4.5.3 and 8.7.4.5.4) to a document as reusable resources. Colours are
/// DeviceRGB and are blended by an exponential function for two stops (7.10.3, type 2) or a stitching function for more
/// (7.10.4, type 3).
/// </summary>
public static class PdfShadingWriter
{
    /// <summary>The <c>/ShadingType</c> of an axial shading.</summary>
    private const int AxialType = 2;

    /// <summary>The <c>/ShadingType</c> of a radial shading.</summary>
    private const int RadialType = 3;

    /// <summary>The <c>/FunctionType</c> of an exponential interpolation function.</summary>
    private const int ExponentialFunction = 2;

    /// <summary>The smallest gradient that has a start and an end.</summary>
    private const int MinimumStops = 2;

    /// <summary>The entries of a shading dictionary.</summary>
    private const int ShadingEntries = 6;

    /// <summary>The entries of a function dictionary.</summary>
    private const int FunctionEntries = 6;

    /// <summary>Adds an axial shading, which blends colours along a line.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="start">The line's start in the space the shading is used in.</param>
    /// <param name="end">The line's end.</param>
    /// <param name="stops">The colours, with offsets from 0 to 1 that do not decrease; at least two.</param>
    /// <param name="extendStart">Whether the first colour continues before the start.</param>
    /// <param name="extendEnd">Whether the last colour continues after the end.</param>
    /// <returns>The shading dictionary's object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The stops are fewer than two, out of range or out of order.</exception>
    public static PdfObjectId AddAxial(IPdfObjectSink sink, PdfPoint start, PdfPoint end, ReadOnlySpan<PdfGradientStop> stops, bool extendStart, bool extendEnd)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var shading = Create(sink, AxialType, stops, extendStart, extendEnd);
        shading.Set(KnownName.Coords, PdfValue.FromArray(PdfArray.FromNumbers(sink.Owner, [start.X, start.Y, end.X, end.Y])));
        return sink.Add(PdfValue.FromDictionary(shading));
    }

    /// <summary>Adds a radial shading, which blends colours between two circles.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="start">The start circle.</param>
    /// <param name="end">The end circle.</param>
    /// <param name="stops">The colours, with offsets from 0 to 1 that do not decrease; at least two.</param>
    /// <param name="extendStart">Whether the first colour continues before the start circle.</param>
    /// <param name="extendEnd">Whether the last colour continues after the end circle.</param>
    /// <returns>The shading dictionary's object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A radius is negative.</exception>
    /// <exception cref="ArgumentException">The stops are fewer than two, out of range or out of order.</exception>
    public static PdfObjectId AddRadial(IPdfObjectSink sink, PdfCircle start, PdfCircle end, ReadOnlySpan<PdfGradientStop> stops, bool extendStart, bool extendEnd)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentOutOfRangeException.ThrowIfNegative(start.Radius);
        ArgumentOutOfRangeException.ThrowIfNegative(end.Radius);
        var shading = Create(sink, RadialType, stops, extendStart, extendEnd);
        shading.Set(KnownName.Coords, PdfValue.FromArray(PdfArray.FromNumbers(sink.Owner, [start.X, start.Y, start.Radius, end.X, end.Y, end.Radius])));
        return sink.Add(PdfValue.FromDictionary(shading));
    }

    /// <summary>Creates the parts of a shading dictionary that axial and radial shadings share.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="type">The shading type.</param>
    /// <param name="stops">The colours.</param>
    /// <param name="extendStart">Whether the first colour continues before the start.</param>
    /// <param name="extendEnd">Whether the last colour continues after the end.</param>
    /// <returns>The dictionary, without <c>/Coords</c>.</returns>
    private static PdfDictionary Create(IPdfObjectSink sink, int type, ReadOnlySpan<PdfGradientStop> stops, bool extendStart, bool extendEnd)
    {
        Validate(stops);
        var shading = new PdfDictionary(sink.Owner, ShadingEntries);
        shading.Set(KnownName.ShadingType, PdfValue.FromInteger(type));
        shading.Set(KnownName.ColorSpace, PdfValue.FromName(KnownName.DeviceRGB));
        shading.Set(KnownName.Function, PdfValue.FromDictionary(CreateFunction(sink, stops)));
        var extend = new PdfArray(sink.Owner, MinimumStops);
        extend.Add(PdfValue.FromBoolean(extendStart));
        extend.Add(PdfValue.FromBoolean(extendEnd));
        shading.Set(KnownName.Extend, PdfValue.FromArray(extend));
        return shading;
    }

    /// <summary>Checks that the stops can make a gradient.</summary>
    /// <param name="stops">The stops.</param>
    /// <exception cref="ArgumentException">The stops are fewer than two, out of range or out of order.</exception>
    private static void Validate(ReadOnlySpan<PdfGradientStop> stops)
    {
        if (stops.Length < MinimumStops)
        {
            throw new ArgumentException("A gradient needs at least two colours.", nameof(stops));
        }

        var previous = 0F;
        foreach (var stop in stops)
        {
            if (!float.IsFinite(stop.Offset) || stop.Offset < previous || stop.Offset > 1F)
            {
                throw new ArgumentException("Gradient offsets must be from 0 to 1 and must not decrease.", nameof(stops));
            }

            previous = stop.Offset;
        }
    }

    /// <summary>Creates the function that maps a position from 0 to 1 to a colour.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="stops">The stops.</param>
    /// <returns>An exponential function for two stops that span the gradient, otherwise a stitching function.</returns>
    private static PdfDictionary CreateFunction(IPdfObjectSink sink, ReadOnlySpan<PdfGradientStop> stops)
    {
        if (stops.Length == MinimumStops && stops[0].Offset <= 0F && stops[1].Offset >= 1F)
        {
            return CreateSegment(sink, stops[0], stops[1]);
        }

        // A stitching function must cover its whole domain, so the first colour is held flat up to the first stop and the last colour after the last.
        var stitch = new Stitch(sink, stops.Length + MinimumStops);
        var first = stops[0];
        stitch.Add(first, first, first.Offset);
        for (var i = 1; i < stops.Length; i++)
        {
            stitch.Add(stops[i - 1], stops[i], stops[i].Offset);
        }

        stitch.Add(stops[^1], stops[^1], 1F);
        return stitch.Build();
    }

    /// <summary>Creates an exponential function that blends two colours linearly.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="from">The colour at 0.</param>
    /// <param name="to">The colour at 1.</param>
    /// <returns>The function.</returns>
    private static PdfDictionary CreateSegment(IPdfObjectSink sink, PdfGradientStop from, PdfGradientStop to)
    {
        var function = new PdfDictionary(sink.Owner, FunctionEntries);
        function.Set(KnownName.FunctionType, PdfValue.FromInteger(ExponentialFunction));
        function.Set(KnownName.Domain, PdfValue.FromArray(PdfArray.FromNumbers(sink.Owner, [0F, 1F])));
        function.Set(KnownName.C0, PdfValue.FromArray(PdfArray.FromNumbers(sink.Owner, [from.Red, from.Green, from.Blue])));
        function.Set(KnownName.C1, PdfValue.FromArray(PdfArray.FromNumbers(sink.Owner, [to.Red, to.Green, to.Blue])));
        function.Set(KnownName.N, PdfValue.FromInteger(1));
        return function;
    }

    /// <summary>Collects the pieces of a stitching function, leaving out pieces that have no width.</summary>
    private sealed class Stitch
    {
        /// <summary>The <c>/FunctionType</c> of a stitching function.</summary>
        private const int StitchingFunction = 3;

        /// <summary>The document.</summary>
        private readonly IPdfObjectSink _sink;

        /// <summary>The pieces' functions.</summary>
        private readonly PdfArray _functions;

        /// <summary>The upper bounds of the pieces.</summary>
        private readonly PdfArray _bounds;

        /// <summary>The input range of each piece.</summary>
        private readonly PdfArray _encode;

        /// <summary>Where the last piece ended.</summary>
        private float _cursor;

        /// <summary>Initializes a new instance of the <see cref="Stitch"/> class.</summary>
        /// <param name="sink">The document.</param>
        /// <param name="capacity">The most pieces that can be added.</param>
        internal Stitch(IPdfObjectSink sink, int capacity)
        {
            _sink = sink;
            _functions = new(sink.Owner, capacity);
            _bounds = new(sink.Owner, capacity);
            _encode = new(sink.Owner, capacity * MinimumStops);
        }

        /// <summary>Adds a piece that blends two colours up to an offset.</summary>
        /// <param name="from">The colour at the piece's start.</param>
        /// <param name="to">The colour at the piece's end.</param>
        /// <param name="end">The offset the piece ends at.</param>
        internal void Add(PdfGradientStop from, PdfGradientStop to, float end)
        {
            if (end <= _cursor)
            {
                return;
            }

            _functions.Add(PdfValue.FromDictionary(CreateSegment(_sink, from, to)));
            _bounds.Add(PdfValue.FromReal(end));
            _encode.Add(PdfValue.FromReal(0));
            _encode.Add(PdfValue.FromReal(1));
            _cursor = end;
        }

        /// <summary>Creates the stitching function.</summary>
        /// <returns>The function.</returns>
        internal PdfDictionary Build()
        {
            // The last piece ends at the end of the domain, which /Bounds does not list.
            _bounds.RemoveAt(_bounds.Count - 1);
            var function = new PdfDictionary(_sink.Owner, FunctionEntries);
            function.Set(KnownName.FunctionType, PdfValue.FromInteger(StitchingFunction));
            function.Set(KnownName.Domain, PdfValue.FromArray(PdfArray.FromNumbers(_sink.Owner, [0F, 1F])));
            function.Set(KnownName.Functions, PdfValue.FromArray(_functions));
            function.Set(KnownName.Bounds, PdfValue.FromArray(_bounds));
            function.Set(KnownName.Encode, PdfValue.FromArray(_encode));
            return function;
        }
    }
}

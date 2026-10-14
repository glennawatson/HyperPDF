// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>The coding parameters of one encode: the image and the options resolved into the values the markers carry.</summary>
[DebuggerDisplay("JpxEncodeSettings: {Width}x{Height}, {Components} components, reversible {Reversible}")]
internal sealed record JpxEncodeSettings
{
    /// <summary>The guard bits an encode starts with (ISO 15444-1 A.6.4).</summary>
    internal const int DefaultGuardBits = 2;

    /// <summary>The most guard bits the QCD marker holds.</summary>
    internal const int MaxGuardBits = 7;

    /// <summary>The most decomposition levels the encoder chooses by itself.</summary>
    private const int MaxAutoLevels = 5;

    /// <summary>The size below which the smallest side leaves no use for further decomposition levels, as an exponent: levels stop 2 short of it.</summary>
    private const int LevelsBelowSmallestSide = 2;

    /// <summary>The largest side of an image coded as one tile.</summary>
    private const int SingleTileSide = 3072;

    /// <summary>The tile side used for larger images.</summary>
    private const int LargeTileSide = 2048;

    /// <summary>The smallest code-block side.</summary>
    private const int MinBlockSide = 4;

    /// <summary>The largest code-block side.</summary>
    private const int MaxBlockSide = 1024;

    /// <summary>The most samples in a code-block.</summary>
    private const int MaxBlockSamples = 4096;

    /// <summary>The smallest precinct side.</summary>
    private const int MinPrecinctSide = 2;

    /// <summary>The largest precinct side, 2^15.</summary>
    private const int MaxPrecinctSide = 1 << 15;

    /// <summary>The most decomposition levels.</summary>
    private const int MaxLevels = JpxCodingStyle.MaxLevels;

    /// <summary>The most quality layers the COD marker holds.</summary>
    private const int MaxLayers = ushort.MaxValue;

    /// <summary>The highest quality.</summary>
    private const int MaxQuality = 100;

    /// <summary>The quality points that halve the step.</summary>
    private const double QualityPerOctave = 16;

    /// <summary>The step, in 8-bit sample units, of the lowest quality point below the best.</summary>
    private const double BaseStep = 2;

    /// <summary>The step of the best quality.</summary>
    private const double FinestStep = 1;

    /// <summary>The samples that make up an 8-bit range, for scaling the step to other depths.</summary>
    private const int ReferenceBits = 8;

    /// <summary>The colour channels the colour transform covers.</summary>
    private const int TransformChannels = 3;

    /// <summary>Gets the image width.</summary>
    internal required int Width { get; init; }

    /// <summary>Gets the image height.</summary>
    internal required int Height { get; init; }

    /// <summary>Gets the number of components.</summary>
    internal required int Components { get; init; }

    /// <summary>Gets the number of colour channels.</summary>
    internal required int ColourChannels { get; init; }

    /// <summary>Gets the bits of every sample.</summary>
    internal required int Precision { get; init; }

    /// <summary>Gets a value indicating whether the 5/3 reversible wavelet is used.</summary>
    internal required bool Reversible { get; init; }

    /// <summary>Gets a value indicating whether the colour transform runs on the first three components.</summary>
    internal required bool UseTransform { get; init; }

    /// <summary>Gets the wavelet decomposition levels.</summary>
    internal required int Levels { get; init; }

    /// <summary>Gets the code-block width as a power of two.</summary>
    internal required int BlockWidthExponent { get; init; }

    /// <summary>Gets the code-block height as a power of two.</summary>
    internal required int BlockHeightExponent { get; init; }

    /// <summary>Gets the quality layers.</summary>
    internal required int Layers { get; init; }

    /// <summary>Gets the tile width.</summary>
    internal required int TileWidth { get; init; }

    /// <summary>Gets the tile height.</summary>
    internal required int TileHeight { get; init; }

    /// <summary>Gets the precinct side as a power of two, or zero for one precinct per resolution.</summary>
    internal required int PrecinctExponent { get; init; }

    /// <summary>Gets the packet order.</summary>
    internal required JpxProgressionOrder Order { get; init; }

    /// <summary>Gets the code-block mode switches.</summary>
    internal required JpxBlockStyle Style { get; init; }

    /// <summary>Gets a value indicating whether packets start with SOP markers.</summary>
    internal required bool StartOfPacketMarkers { get; init; }

    /// <summary>Gets a value indicating whether packet headers end with EPH markers.</summary>
    internal required bool EndOfHeaderMarkers { get; init; }

    /// <summary>Gets the quantization step of a lossy file, in sample units of the image's depth; zero for a lossless file.</summary>
    internal required double Step { get; init; }

    /// <summary>Gets the number of guard bits the QCD marker declares.</summary>
    internal int GuardBits { get; init; } = DefaultGuardBits;

    /// <summary>Gets the number of tile columns.</summary>
    internal int TilesAcross => JpxRectangle.CeilDivide(Width, TileWidth);

    /// <summary>Gets the number of tile rows.</summary>
    internal int TilesDown => JpxRectangle.CeilDivide(Height, TileHeight);

    /// <summary>Resolves an image and its options.</summary>
    /// <param name="image">The image, already validated.</param>
    /// <param name="options">The options.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
    internal static JpxEncodeSettings Create(PdfJpxImage image, PdfJpxOptions options)
    {
        var tileWidth = TileSide(options.TileWidth, image.Width);
        var tileHeight = TileSide(options.TileHeight, image.Height);
        var colour = image.ColourChannels;
        var lossy = options.Mode == PdfJpxMode.Lossy;
        return new()
        {
            Width = image.Width,
            Height = image.Height,
            Components = image.Components,
            ColourChannels = colour,
            Precision = image.BitsPerComponent,
            Reversible = !lossy,
            UseTransform = options.ColorTransform && colour == TransformChannels && image.ColorSpace != PdfJpxColorSpace.Cmyk,
            Levels = ChooseLevels(options.DecompositionLevels, tileWidth, tileHeight),
            BlockWidthExponent = BlockExponent(options.CodeBlockWidth, nameof(options.CodeBlockWidth)),
            BlockHeightExponent = BlockExponent(options.CodeBlockHeight, nameof(options.CodeBlockHeight)),
            Layers = CheckLayers(options.Layers),
            TileWidth = tileWidth,
            TileHeight = tileHeight,
            PrecinctExponent = ExponentOfPrecinct(options.PrecinctSize),
            Order = (JpxProgressionOrder)options.Progression,
            Style = (JpxBlockStyle)options.BlockStyle,
            StartOfPacketMarkers = options.StartOfPacketMarkers,
            EndOfHeaderMarkers = options.EndOfPacketHeaderMarkers,
            Step = lossy ? StepFor(options.Quality, image.BitsPerComponent) : 0,
        };
    }

    /// <summary>Chooses a tile side: the requested one, one tile for a small image, or 2048 samples for a large one.</summary>
    /// <param name="requested">The requested side, or zero.</param>
    /// <param name="side">The image side.</param>
    /// <returns>The tile side.</returns>
    private static int TileSide(int requested, int side)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requested);
        if (requested > 0)
        {
            return requested;
        }

        return side <= SingleTileSide ? side : LargeTileSide;
    }

    /// <summary>Chooses the decomposition levels.</summary>
    /// <param name="requested">The requested levels, or <see langword="null"/>.</param>
    /// <param name="tileWidth">The tile width.</param>
    /// <param name="tileHeight">The tile height.</param>
    /// <returns>The levels.</returns>
    private static int ChooseLevels(int? requested, int tileWidth, int tileHeight)
    {
        if (requested is { } levels)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(levels);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(levels, MaxLevels);
            return levels;
        }

        var side = BitOperations.Log2((uint)Math.Min(tileWidth, tileHeight)) - LevelsBelowSmallestSide;
        return Math.Clamp(side, 0, MaxAutoLevels);
    }

    /// <summary>Converts a code-block side to its exponent after checking it.</summary>
    /// <param name="side">The side.</param>
    /// <param name="name">The option name, for the error.</param>
    /// <returns>The exponent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The side is not a power of two from 4 to 1024.</exception>
    private static int BlockExponent(int side, string name)
    {
        if (side is < MinBlockSide or > MaxBlockSide || !BitOperations.IsPow2(side))
        {
            throw new ArgumentOutOfRangeException(name, side, "The code-block side must be a power of two from 4 to 1024.");
        }

        return BitOperations.Log2((uint)side);
    }

    /// <summary>Converts a precinct side to its exponent after checking it.</summary>
    /// <param name="side">The side, or zero.</param>
    /// <returns>The exponent, or zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The side is neither zero nor a power of two from 2 to 32768.</exception>
    private static int ExponentOfPrecinct(int side)
    {
        if (side == 0)
        {
            return 0;
        }

        if (side is < MinPrecinctSide or > MaxPrecinctSide || !BitOperations.IsPow2(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The precinct side must be zero or a power of two from 2 to 32768.");
        }

        return BitOperations.Log2((uint)side);
    }

    /// <summary>Checks the number of quality layers.</summary>
    /// <param name="layers">The requested layers.</param>
    /// <returns>The same layers, once found valid.</returns>
    private static int CheckLayers(int layers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layers);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(layers, MaxLayers);
        return layers;
    }

    /// <summary>Converts a quality to a quantization step.</summary>
    /// <param name="quality">The quality, from 1 to 100.</param>
    /// <param name="precision">The bits of a sample.</param>
    /// <returns>The step in units of the sample's bit depth.</returns>
    private static double StepFor(int quality, int precision)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, MaxQuality);
        var eightBit = quality == MaxQuality ? FinestStep : BaseStep * double.Exp2((MaxQuality - quality) / QualityPerOctave);
        return eightBit * Math.ScaleB(1.0, precision - ReferenceBits);
    }
}

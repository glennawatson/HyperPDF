// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Adds images to a document as image XObjects (ISO 32000-2 8.9.5). A JPEG is stored as it is under <c>/DCTDecode</c>, so it
/// is not decoded or recompressed. Decoded pixels are stored without loss: Flate with PNG predictors for gray, RGB and
/// BGRA pixels (alpha goes to a gray <c>/SMask</c>), and CCITT Group 4 for one-bit pixels. An ICC based colour space
/// can replace the device colour space.
/// </summary>
public static class PdfImageWriter
{
    /// <summary>The number of header bytes read to find the profile's colour space.</summary>
    private const int IccHeaderLength = 128;

    /// <summary>The offset of the data colour space signature in an ICC profile header.</summary>
    private const int IccColorSpaceOffset = 16;

    /// <summary>The offset of the <c>acsp</c> file signature in an ICC profile header.</summary>
    private const int IccSignatureOffset = 36;

    /// <summary>The length of a four character ICC signature.</summary>
    private const int IccTagLength = 4;

    /// <summary>The components of a gray image.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of an RGB image.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK image.</summary>
    private const int CmykComponents = 4;

    /// <summary>The items of an ICC based colour space array: the family name and the profile stream.</summary>
    private const int IccArrayItems = 2;

    /// <summary>The entries of a stream dictionary for a profile.</summary>
    private const int ProfileEntries = 4;

    /// <summary>Gets the <c>acsp</c> signature every ICC profile has.</summary>
    private static ReadOnlySpan<byte> IccSignature => "acsp"u8;

    /// <summary>Gets the data colour space signature of a gray profile.</summary>
    private static ReadOnlySpan<byte> GraySpace => "GRAY"u8;

    /// <summary>Gets the data colour space signature of an RGB profile.</summary>
    private static ReadOnlySpan<byte> RgbSpace => "RGB "u8;

    /// <summary>Gets the data colour space signature of a CMYK profile.</summary>
    private static ReadOnlySpan<byte> CmykSpace => "CMYK"u8;

    /// <summary>Adds an ICC profile as a colour space that images can use.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="profile">The profile file's bytes.</param>
    /// <returns>The colour space.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The bytes are not an ICC profile for a gray, RGB or CMYK colour space.</exception>
    public static PdfIccColorSpace AddIccColorSpace(IPdfObjectSink sink, ReadOnlySpan<byte> profile)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var components = ReadProfileComponents(profile);
        var dictionary = new PdfDictionary(sink.Owner, ProfileEntries);
        dictionary.Set(KnownName.N, PdfValue.FromInteger(components));
        dictionary.Set(KnownName.Alternate, PdfValue.FromName(GetDeviceSpace(components)));
        var stream = new PdfStream(dictionary, RasterImageEncoder.Deflate(dictionary, profile));
        var array = new PdfArray(sink.Owner, IccArrayItems);
        array.Add(PdfValue.FromName(KnownName.ICCBased));
        array.Add(PdfValue.FromReference(sink.Add(PdfValue.FromStream(stream))));
        return new(sink.Add(PdfValue.FromArray(array)), components);
    }

    /// <summary>Adds a JPEG, keeping its bytes.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="jpeg">The JPEG file's bytes: baseline, extended sequential or progressive, 8 bits a sample, one, three or four components.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The bytes are not a JPEG that <c>/DCTDecode</c> can carry.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCreatedImage AddJpeg(IPdfObjectSink sink, ReadOnlySpan<byte> jpeg) => AddJpeg(sink, jpeg, PdfImageOptions.Default);

    /// <summary>Adds a JPEG, keeping its bytes.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="jpeg">The JPEG file's bytes: baseline, extended sequential or progressive, 8 bits a sample, one, three or four components.</param>
    /// <param name="options">The colour space and interpolation.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The bytes are not a JPEG that <c>/DCTDecode</c> can carry, or the colour space has a different component count.</exception>
    public static PdfCreatedImage AddJpeg(IPdfObjectSink sink, ReadOnlySpan<byte> jpeg, PdfImageOptions options)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(options);
        if (!JpegMarkers.TryReadInfo(jpeg, out var info) || !info.IsSupported || info.Height == 0)
        {
            throw new ArgumentException("The bytes are not a JPEG that DCTDecode can carry.", nameof(jpeg));
        }

        var dictionary = RasterImageEncoder.CreateImageDictionary(sink, info.Width, info.Height, info.Precision, RasterImageEncoder.ColorSpaceFor(info.Components, options));
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.DCTDecode));
        if (info.Components == CmykComponents && info.HasAdobe)
        {
            // Adobe writes CMYK JPEGs with inverted samples; the inverse /Decode array restores them.
            dictionary.Set(KnownName.Decode, PdfValue.FromArray(PdfArray.FromNumbers(sink.Owner, [1, 0, 1, 0, 1, 0, 1, 0])));
        }

        RasterImageEncoder.ApplyInterpolation(dictionary, options);
        var id = sink.Add(PdfValue.FromStream(new(dictionary, jpeg.ToArray())));
        return new(id, info.Width, info.Height);
    }

    /// <summary>Reads a JPEG from a stream and adds it, keeping its bytes.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="source">The stream, read to its end.</param>
    /// <param name="options">The colour space and interpolation.</param>
    /// <param name="cancellationToken">Cancels reading the stream.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The bytes are not a JPEG that <c>/DCTDecode</c> can carry.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfCreatedImage> AddJpegAsync(IPdfObjectSink sink, Stream source, PdfImageOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(source);
        await using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        return AddJpeg(sink, memory.GetBuffer().AsSpan(0, (int)memory.Length), options);
    }

    /// <summary>Adds decoded pixels as a lossless image.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="image">The pixels.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCreatedImage AddRaster(IPdfObjectSink sink, PdfRasterImage image) => AddRaster(sink, image, PdfImageOptions.Default, CancellationToken.None);

    /// <summary>Adds decoded pixels as a lossless image.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="image">The pixels.</param>
    /// <param name="options">The colour space, interpolation and stencil choice.</param>
    /// <param name="cancellationToken">Cancels the encoding between its steps.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image, or the colour space has a different component count.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfCreatedImage AddRaster(IPdfObjectSink sink, PdfRasterImage image, PdfImageOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
        image.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var id = RasterImageEncoder.Add(sink, image, options, cancellationToken);
        return new(id, image.Width, image.Height);
    }

    /// <summary>Reads how many components an ICC profile describes.</summary>
    /// <param name="profile">The profile file.</param>
    /// <returns>1, 3 or 4.</returns>
    /// <exception cref="ArgumentException">The bytes are not an ICC profile for a gray, RGB or CMYK colour space.</exception>
    private static int ReadProfileComponents(ReadOnlySpan<byte> profile)
    {
        if (profile.Length < IccHeaderLength || !CryptographicOperations.FixedTimeEquals(profile.Slice(IccSignatureOffset, IccTagLength), IccSignature))
        {
            throw new ArgumentException("The bytes are not an ICC profile.", nameof(profile));
        }

        var space = profile.Slice(IccColorSpaceOffset, IccTagLength);
        if (space.SequenceEqual(GraySpace))
        {
            return GrayComponents;
        }

        if (space.SequenceEqual(RgbSpace))
        {
            return RgbComponents;
        }

        return space.SequenceEqual(CmykSpace) ? CmykComponents : throw new ArgumentException("The profile is not for a gray, RGB or CMYK colour space.", nameof(profile));
    }

    /// <summary>Gets the device colour space a profile falls back to.</summary>
    /// <param name="components">The number of components.</param>
    /// <returns>The name of the device colour space.</returns>
    private static KnownName GetDeviceSpace(int components) => components switch
    {
        GrayComponents => KnownName.DeviceGray,
        RgbComponents => KnownName.DeviceRGB,
        _ => KnownName.DeviceCMYK,
    };
}

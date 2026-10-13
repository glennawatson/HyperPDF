// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Xfa;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Describes media, 3D, rich media and XFA data as text so two reads of a document can be compared exactly.</summary>
internal static class MediaDataDescription
{
    /// <summary>Hashes bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The hash as hexadecimal text.</returns>
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>Describes a multimedia annotation.</summary>
    /// <param name="text">The description.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="includeRawBytes">Whether to hash the stored bytes of streams too.</param>
    internal static void Append(StringBuilder text, PdfMultimediaAnnotation annotation, bool includeRawBytes)
    {
        _ = text.Append(CultureInfo.InvariantCulture, $"mm={annotation.Kind}@{annotation.PageIndex}:{annotation.Bounds};");
        if (annotation.Screen is { } screen)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"screen={screen.Title}:{screen.Action?.Subtype}:{screen.Triggers.Length};");
        }

        if (annotation.Movie is { } movie)
        {
            var activation = movie.Activation;
            _ = text.Append(CultureInfo.InvariantCulture, $"movie={movie.Title}:{movie.File}:{string.Join(',', movie.Aspect)}:{movie.Rotation}:{movie.HasPoster};");
            _ = text.Append(CultureInfo.InvariantCulture, $"play={activation.Start}:{activation.Rate}:{activation.Volume}:{activation.ShowControls}:{activation.Mode};");
        }

        if (annotation.Sound is { } sound)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"sound={sound.Rate}:{sound.Channels}:{sound.BitsPerSample}:{sound.Encoding}:{sound.Compression}:{Stream(sound.Data, includeRawBytes)};");
        }

        if (annotation.RichMedia is { } richMedia)
        {
            Append(text, richMedia, includeRawBytes);
        }

        if (annotation.ThreeD is { } threeD)
        {
            Append(text, threeD, includeRawBytes);
        }
    }

    /// <summary>Describes the XFA packets.</summary>
    /// <param name="text">The description.</param>
    /// <param name="xfa">The XFA data, or null.</param>
    internal static void Append(StringBuilder text, PdfXfa? xfa)
    {
        if (xfa is null)
        {
            return;
        }

        _ = text.Append(CultureInfo.InvariantCulture, $"xfa={xfa.IsSingleStream};");
        foreach (var packet in xfa.Packets)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"xfaPacket={packet.Name}:{packet.Data.Length}:{Hash(packet.Data)};");
        }
    }

    /// <summary>Describes a stream by its decoded bytes, and optionally its stored bytes.</summary>
    /// <param name="stream">The stream, or null.</param>
    /// <param name="includeRawBytes">Whether to hash the stored bytes too.</param>
    /// <returns>The description.</returns>
    internal static string Stream(PdfStream? stream, bool includeRawBytes)
    {
        if (stream is null)
        {
            return "none";
        }

        var decoded = stream.DecodeToArray();
        var raw = includeRawBytes ? $"/{Hash(stream.CopyRawData())}" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{decoded.Length}:{Hash(decoded)}{raw}");
    }

    /// <summary>Describes a rich media annotation.</summary>
    /// <param name="text">The description.</param>
    /// <param name="richMedia">The annotation data.</param>
    /// <param name="includeRawBytes">Whether to hash the stored bytes of streams too.</param>
    private static void Append(StringBuilder text, PdfRichMediaAnnotation richMedia, bool includeRawBytes)
    {
        _ = text.Append(CultureInfo.InvariantCulture, $"rich={richMedia.ActivationCondition}:{richMedia.DeactivationCondition}:{richMedia.ViewCount};");
        foreach (var asset in richMedia.Assets)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"asset={asset.Name}:{Stream(asset.Data, includeRawBytes)};");
        }

        foreach (var configuration in richMedia.Configurations)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"config={configuration.Subtype}:{configuration.Name};");
            foreach (var instance in configuration.Instances)
            {
                _ = text.Append(CultureInfo.InvariantCulture, $"instance={instance.Subtype}:{instance.AssetName};");
            }
        }
    }

    /// <summary>Describes a 3D annotation.</summary>
    /// <param name="text">The description.</param>
    /// <param name="threeD">The annotation data.</param>
    /// <param name="includeRawBytes">Whether to hash the stored bytes of streams too.</param>
    private static void Append(StringBuilder text, Pdf3DAnnotation threeD, bool includeRawBytes)
    {
        _ = text.Append(CultureInfo.InvariantCulture, $"3d={threeD.Activation}:{threeD.InitialView?.ExternalName}:{threeD.InitialViewSelector};");
        if (threeD.Stream is not { } stream)
        {
            return;
        }

        _ = text.Append(CultureInfo.InvariantCulture, $"3dStream={stream.Subtype}:{stream.DefaultViewIndex}:{stream.DefaultViewSelector}:{Stream(stream.Data, includeRawBytes)};");
        foreach (var view in stream.Views)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"3dView={view.ExternalName}:{view.InternalName}:{view.MatrixMode}:{string.Join(',', view.CameraToWorld)}:{view.CameraDistance};");
        }
    }
}

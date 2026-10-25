// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Export;

/// <summary>Writes the portable anymap formats: PGM and PPM, which have no alpha, and PAM, which has.</summary>
internal static class NetpbmRasterWriter
{
    /// <summary>Writes a PGM (grey) or PPM (colour) file; transparent pixels are placed over white.</summary>
    /// <param name="samples">The image.</param>
    /// <returns>The file.</returns>
    internal static byte[] WritePnm(RasterSamples samples)
    {
        var flat = samples.OverWhite();
        var header = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{(flat.IsGrey ? "P5" : "P6")}\n{flat.Width} {flat.Height}\n255\n"));
        return Join(header, flat.Data);
    }

    /// <summary>Writes a PAM file, keeping the alpha channel.</summary>
    /// <param name="samples">The image.</param>
    /// <returns>The file.</returns>
    internal static byte[] WritePam(RasterSamples samples)
    {
        var tuple = samples.Channels switch
        {
            RasterSamples.GreyChannels => "GRAYSCALE",
            RasterSamples.RgbChannels => "RGB",
            _ => "RGB_ALPHA",
        };

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"P7\nWIDTH {samples.Width}\nHEIGHT {samples.Height}\nDEPTH {samples.Channels}\nMAXVAL 255\nTUPLTYPE {tuple}\nENDHDR\n");
        return Join(Encoding.ASCII.GetBytes(text), samples.Data);
    }

    /// <summary>Puts a header and the samples in one array.</summary>
    /// <param name="header">The header.</param>
    /// <param name="data">The samples.</param>
    /// <returns>The file.</returns>
    private static byte[] Join(byte[] header, byte[] data)
    {
        var file = new byte[header.Length + data.Length];
        header.CopyTo(file, 0);
        data.CopyTo(file, header.Length);
        return file;
    }
}

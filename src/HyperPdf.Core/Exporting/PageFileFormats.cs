// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Exporting;

/// <summary>The file formats a page can be exported as, with what to tell people about each.</summary>
public static class PageFileFormats
{
    /// <summary>The formats, pictures first and then vector files.</summary>
    private static readonly PageFileFormatInfo[] FormatList =
    [
        new(PageFileFormat.Png, "PNG picture", "A sharp picture that keeps transparency. A good choice for screens and the web.", ".png", true, false),
        new(PageFileFormat.Jpeg, "JPEG picture", "A smaller picture that can lose a little detail. Good for photos. Transparent areas turn white.", ".jpg", true, false),
        new(PageFileFormat.Tiff, "TIFF picture", "A picture format used in print and scanning. The file is not compressed, so it is large.", ".tif", true, false),
        new(PageFileFormat.Bmp, "BMP picture", "A simple Windows picture. The file is not compressed.", ".bmp", true, false),
        new(PageFileFormat.Pnm, "PBM, PGM or PPM picture", "The simplest picture format, which many tools read. There is no transparency.", ".ppm", true, false),
        new(PageFileFormat.Pam, "PAM picture", "Like PPM, with transparency.", ".pam", true, false),
        new(PageFileFormat.Tga, "TGA picture", "A picture format used in games and graphics programs. It keeps transparency.", ".tga", true, false),
        new(PageFileFormat.Psd, "Photoshop (PSD) picture", "A one layer picture that opens in Photoshop and similar programs.", ".psd", true, false),
        new(PageFileFormat.Svg, "SVG drawing", "A drawing that stays sharp at any size. Text, shapes and images are kept as they are.", ".svg", false, false),
        new(PageFileFormat.PostScript, "PostScript", "A printer language file with a page for each page you choose. Text is saved as outlines.", ".ps", false, true),
        new(PageFileFormat.EncapsulatedPostScript, "Encapsulated PostScript (EPS)", "One page that other documents can place. Text is saved as outlines.", ".eps", false, false),
    ];

    /// <summary>Gets the formats, pictures first and then vector files.</summary>
    public static IReadOnlyList<PageFileFormatInfo> All => FormatList;

    /// <summary>Finds what is known about a format.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The information.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The format is not a known one.</exception>
    public static PageFileFormatInfo Get(PageFileFormat format)
    {
        foreach (var info in FormatList)
        {
            if (info.Format == format)
            {
                return info;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(format), format, "The file format is not known.");
    }
}

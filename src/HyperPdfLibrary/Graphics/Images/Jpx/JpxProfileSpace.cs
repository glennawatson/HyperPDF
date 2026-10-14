// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Makes the colour space of an ICC profile in a JP2 colour specification box (T.800 I.5.3.3, methods 2 and 3).</summary>
internal static class JpxProfileSpace
{
    /// <summary>The components of a grey profile.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of an RGB profile.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK profile.</summary>
    private const int CmykComponents = 4;

    /// <summary>Reads a profile.</summary>
    /// <param name="profile">The profile bytes.</param>
    /// <returns>The ICCBased colour space, or <see langword="null"/> when the profile cannot be read or is not for one, three or four components.</returns>
    internal static PdfColorSpace? Create(ReadOnlySpan<byte> profile)
    {
        if (profile.IsEmpty || IccProfile.Create(profile) is not { } transform)
        {
            return null;
        }

        var device = transform.Components switch
        {
            GrayComponents => PdfColorSpace.DeviceGray,
            RgbComponents => PdfColorSpace.DeviceRgb,
            CmykComponents => PdfColorSpace.DeviceCmyk,
            _ => null,
        };

        if (device is null)
        {
            return null;
        }

        // An sRGB profile converts like device RGB, which keeps the vectorised row path.
        return transform.IsSrgb ? device : new IccBasedColorSpace(device, device.GetDefaultDecode(PdfColorSpace.ByteBits), transform);
    }
}

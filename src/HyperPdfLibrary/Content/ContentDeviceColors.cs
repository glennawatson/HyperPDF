// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's DeviceColors operations over its owned state.</summary>
internal static class ContentDeviceColors
{
    /// <summary>
    /// Gets the space a device colour converts through. The /DefaultGray, /DefaultRGB or /DefaultCMYK space of the current
    /// resources replaces the device space (ISO 32000-2 8.6.5.6); without one, the output intent profile does when the
    /// cache carries one, otherwise the device space stays.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "space">The device space the content named.</param>
    /// <returns>The space to resolve colours in.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfColorSpace MapDeviceSpace(ContentInterpreter self, PdfColorSpace space) =>
        ContentDeviceColors.FindDefault(self, space) ?? (self.Cache.DeviceColors is { } colors ? colors.Map(space) : space);

    /// <summary>
    /// Finds the default colour space for a device space in the current resources. It must have the device space's
    /// component count and may not be a device space or a pattern space itself, so a default never replaces itself.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "space">The device space.</param>
    /// <returns>The default space, or <see langword="null"/> when the resources name none that fits.</returns>
    internal static PdfColorSpace? FindDefault(ContentInterpreter self, PdfColorSpace space)
    {
        var name = space.Kind switch
        {
            PdfColorSpaceKind.DeviceGray => self.Cache.DefaultGray,
            PdfColorSpaceKind.DeviceRgb => self.Cache.DefaultRgb,
            PdfColorSpaceKind.DeviceCmyk => self.Cache.DefaultCmyk,
            _ => default,
        };
        if (name.IsNone || ContentExecution.CurrentResources(self)?.GetDictionary(KnownName.ColorSpace) is not { } resources)
        {
            return null;
        }

        return resources.Get(name).AsArray() is { } array && self.Cache.GetDefaultColorSpace(array, resources) is { } replacement
            ? ContentDeviceColors.Fits(replacement, space)
            : null;
    }

    /// <summary>Checks that a default space can stand in for a device space.</summary>
    /// <param name = "replacement">The default space.</param>
    /// <param name = "space">The device space.</param>
    /// <returns>The default space, or <see langword="null"/> when it has another component count or is itself a device or pattern space.</returns>
    private static PdfColorSpace? Fits(PdfColorSpace replacement, PdfColorSpace space) =>
        replacement.Components == space.Components && replacement.Kind is not (PdfColorSpaceKind.DeviceGray or PdfColorSpaceKind.DeviceRgb or
        PdfColorSpaceKind.DeviceCmyk or
        PdfColorSpaceKind.Pattern) ? replacement : null;
}

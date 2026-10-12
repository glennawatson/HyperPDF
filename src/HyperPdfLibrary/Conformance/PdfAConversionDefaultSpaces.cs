// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Which device colour spaces a resource dictionary replaces with a default colour space.</summary>
internal readonly record struct PdfAConversionDefaultSpaces
{
    /// <summary>Initializes a new instance of the <see cref="PdfAConversionDefaultSpaces"/> struct from resources.</summary>
    /// <param name="resources">The resources, or <see langword="null"/>.</param>
    internal PdfAConversionDefaultSpaces(PdfDictionary? resources)
    {
        if (resources?.GetDictionary(KnownName.ColorSpace) is not { } spaces || resources.Owner is not { } owner)
        {
            return;
        }

        var names = owner.Names;
        Rgb = spaces.ContainsKey(names.Intern("DefaultRGB"u8));
        Cmyk = spaces.ContainsKey(names.Intern("DefaultCMYK"u8));
        Gray = spaces.ContainsKey(names.Intern("DefaultGray"u8));
    }

    /// <summary>Gets a value indicating whether DefaultRGB is set.</summary>
    internal bool Rgb { get; }

    /// <summary>Gets a value indicating whether DefaultCMYK is set.</summary>
    internal bool Cmyk { get; }

    /// <summary>Gets a value indicating whether DefaultGray is set.</summary>
    internal bool Gray { get; }
}

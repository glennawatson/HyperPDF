// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Export;

/// <summary>Writes axial and radial shadings as PostScript level 3 shading dictionaries whose colours are a sampled ramp.</summary>
internal static class PostScriptShadings
{
    /// <summary>The colours sampled along the gradient.</summary>
    private const int Samples = 256;

    /// <summary>The bytes of one sampled colour.</summary>
    private const int RgbBytes = 3;

    /// <summary>The index of the green sample in a colour.</summary>
    private const int GreenIndex = 1;

    /// <summary>The index of the blue sample in a colour.</summary>
    private const int BlueIndex = 2;

    /// <summary>The numbers in an axial shading's coordinates.</summary>
    private const int AxialCoords = 4;

    /// <summary>The numbers in a radial shading's coordinates.</summary>
    private const int RadialCoords = 6;

    /// <summary>The numbers in a domain.</summary>
    private const int DomainNumbers = 2;

    /// <summary>The PostScript shading type of an axial shading.</summary>
    private const int AxialType = 2;

    /// <summary>The PostScript shading type of a radial shading.</summary>
    private const int RadialType = 3;

    /// <summary>Paints a shading over the current clip.</summary>
    /// <param name="writer">The PostScript writer.</param>
    /// <param name="shading">The shading.</param>
    /// <returns><see langword="false"/> when the shading is not an axial or radial one with a function, and nothing was written.</returns>
    internal static bool TryWrite(PostScriptWriter writer, PdfShading shading)
    {
        var axial = shading.Kind == PdfShadingKind.Axial;
        if (shading.Function is null || (!axial && shading.Kind != PdfShadingKind.Radial))
        {
            return false;
        }

        var count = axial ? AxialCoords : RadialCoords;
        Span<float> coords = stackalloc float[RadialCoords];
        if (shading.Dictionary.GetArray(KnownName.Coords) is not { } array || array.Count < count)
        {
            return false;
        }

        _ = array.ReadNumbers(coords[..count]);
        var domain = ReadDomain(shading.Dictionary);
        var colors = new PdfColor[Samples];
        ShadingShaders.SampleColors(shading, domain.Start, domain.End, colors);
        WriteDictionary(writer, shading.Dictionary, axial ? AxialType : RadialType, coords[..count], colors);
        return true;
    }

    /// <summary>Reads a gradient's parameter range.</summary>
    /// <param name="dictionary">The shading dictionary.</param>
    /// <returns>The start and end of the parameter; zero to one when none is given.</returns>
    private static ParameterRange ReadDomain(PdfDictionary dictionary) =>
        dictionary.GetArray(KnownName.Domain) is { Count: >= DomainNumbers } domain ? new(domain.GetSingle(0), domain.GetSingle(1)) : new(0, 1);

    /// <summary>Writes the shading dictionary and paints it.</summary>
    /// <param name="writer">The PostScript writer.</param>
    /// <param name="dictionary">The PDF shading dictionary, read for its /Extend.</param>
    /// <param name="type">The PostScript shading type.</param>
    /// <param name="coords">The coordinates.</param>
    /// <param name="colors">The sampled colours.</param>
    private static void WriteDictionary(PostScriptWriter writer, PdfDictionary dictionary, int type, ReadOnlySpan<float> coords, ReadOnlySpan<PdfColor> colors)
    {
        var extend = dictionary.GetArray(KnownName.Extend);
        var ramp = new byte[colors.Length * RgbBytes];
        for (var i = 0; i < colors.Length; i++)
        {
            ramp[i * RgbBytes] = colors[i].Red;
            ramp[(i * RgbBytes) + GreenIndex] = colors[i].Green;
            ramp[(i * RgbBytes) + BlueIndex] = colors[i].Blue;
        }

        writer.Word("<< /ShadingType");
        writer.Number(type);
        writer.Word("/ColorSpace /DeviceRGB /Coords [");
        foreach (var value in coords)
        {
            writer.Number(value);
        }

        writer.Word("] /Extend [");
        writer.Word(extend is not null && extend.Get(0).AsBoolean() ? "true" : "false");
        writer.Word(extend is not null && extend.Get(1).AsBoolean() ? "true" : "false");
        writer.Word("] /Function << /FunctionType 0 /Domain [0 1] /Range [0 1 0 1 0 1] /Size [");
        writer.Number(colors.Length);
        writer.Word("] /BitsPerSample 8 /DataSource");
        writer.EndLine();
        writer.HexData(ramp);
        writer.Line(">> >> shfill");
    }

    /// <summary>The start and end of a gradient's parameter.</summary>
    /// <param name="Start">The parameter at the start of the gradient.</param>
    /// <param name="End">The parameter at the end of the gradient.</param>
    private readonly record struct ParameterRange(float Start, float End);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Generation;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The substitute faces loaded on demand from an open font pack, so text in a font the PDF does not embed looks the same on every
/// operating system and on systems with few fonts. Each face is a TrueType font cached on
/// first use by documents that select the same drawing services.
/// </summary>
internal static class BundledFaces
{
    /// <summary>The number of styles of a family: regular, bold, italic and bold italic.</summary>
    private const int StylesPerFamily = 4;

    /// <summary>The bit that selects the bold style within a family.</summary>
    private const int BoldBit = 1;

    /// <summary>The bit that selects the italic style within a family.</summary>
    private const int ItalicBit = 2;

    /// <summary>The slot of the Symbol face.</summary>
    private const int SymbolSlot = 12;

    /// <summary>The slot of the ZapfDingbats face.</summary>
    private const int DingbatsSlot = 13;

    /// <summary>The resource names, family by family in regular, bold, italic, bold italic order, then Symbol and Dingbats.</summary>
    private static readonly string[] Names =
    [
        "Arimo-Regular", "Arimo-Bold", "Arimo-Italic", "Arimo-BoldItalic",
        "Tinos-Regular", "Tinos-Bold", "Tinos-Italic", "Tinos-BoldItalic",
        "Cousine-Regular", "Cousine-Bold", "Cousine-Italic", "Cousine-BoldItalic",
        "NotoSansMath-Regular", "NotoSansSymbols2-Regular",
    ];

    /// <summary>Generates the requested face before its first use.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <param name="italic">Whether the face slants.</param>
    /// <param name="cancellationToken">Cancels source I/O.</param>
    /// <returns>A task completing when the face is cached.</returns>
    internal static ValueTask EnsureAsync(PdfFontResources resources, BundledFamily family, bool bold, bool italic, CancellationToken cancellationToken)
    {
        var name = Names[SlotOf(family, bold, italic)];
        return FontDataResources.EnsureAsync(resources.Data, "Faces", $"{name}.ttf", token => FontDataGeneration.FaceAsync(resources, name, token), cancellationToken);
    }

    /// <summary>Gets a bundled face.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold; ignored for the symbol families.</param>
    /// <param name="italic">Whether the face slants; ignored for the symbol families.</param>
    /// <returns>The face, or <see langword="null"/> when the resource is missing or damaged.</returns>
    internal static SubstituteFace? Get(PdfFontResources resources, BundledFamily family, bool bold, bool italic)
    {
        var slot = SlotOf(family, bold, italic);
        var faces = resources.Faces.Faces;
        return Volatile.Read(ref faces[slot]) ?? Load(faces, slot);
    }

    /// <summary>Gets the resource name of a face, without its .ttf extension.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold; ignored for the symbol families.</param>
    /// <param name="italic">Whether the face slants; ignored for the symbol families.</param>
    /// <returns>The name, such as Arimo-Bold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ResourceName(BundledFamily family, bool bold, bool italic) => Names[SlotOf(family, bold, italic)];

    /// <summary>Reads a face from its resource without caching it, as the first use of a face does.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold; ignored for the symbol families.</param>
    /// <param name="italic">Whether the face slants; ignored for the symbol families.</param>
    /// <returns>A new face, or <see langword="null"/> when the resource is missing or damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SubstituteFace? Read(BundledFamily family, bool bold, bool italic) => Read(SlotOf(family, bold, italic));

    /// <summary>Finds the slot of a face.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <param name="italic">Whether the face slants.</param>
    /// <returns>The slot.</returns>
    private static int SlotOf(BundledFamily family, bool bold, bool italic) => family switch
    {
        BundledFamily.Symbol => SymbolSlot,
        BundledFamily.Dingbats => DingbatsSlot,
        _ => ((int)family * StylesPerFamily) + (bold ? BoldBit : 0) + (italic ? ItalicBit : 0),
    };

    /// <summary>Reads a face from its resource and publishes it.</summary>
    /// <param name="faces">The selected service's face slots.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The face, or <see langword="null"/>.</returns>
    private static SubstituteFace? Load(SubstituteFace?[] faces, int slot) =>
        Read(slot) is { } face ? Interlocked.CompareExchange(ref faces[slot], face, null) ?? face : null;

    /// <summary>Reads and parses a face from its resource.</summary>
    /// <param name="slot">The slot.</param>
    /// <returns>A new face, or <see langword="null"/>.</returns>
    private static SubstituteFace? Read(int slot)
    {
        using var stream = FontDataResources.Open("Faces", $"{Names[slot]}.ttf");
        if (stream is null)
        {
            return null;
        }

        var data = new byte[(int)stream.Length];
        stream.ReadExactly(data);
        return TrueTypeProgram.TryParse(data, out var program) ? new SubstituteFace(program, Names[slot]) : null;
    }
}

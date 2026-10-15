// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The substitute faces embedded in the library assembly, so text in a font the PDF does not embed looks the same on every
/// operating system and on systems with few fonts, and never needs the network. Each face is a zlib-compressed TrueType
/// font cut down to the code points the standard fonts and their usual substitutes need. Decoded bytes and parsed programs are
/// owned by the selected font resource set, which makes its faces on first use.
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

    /// <summary>Gets a bundled face.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold; ignored for the symbol families.</param>
    /// <param name="italic">Whether the face slants; ignored for the symbol families.</param>
    /// <returns>The face, or <see langword="null"/> when the resource is missing or damaged.</returns>
    internal static SubstituteFace? Get(PdfFontResources resources, BundledFamily family, bool bold, bool italic)
    {
        var slot = SlotOf(family, bold, italic);
        var state = resources.Faces;
        return Volatile.Read(ref state.Faces[slot]) ?? Load(state, slot);
    }

    /// <summary>Gets the resource name of a face, without its extension.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold; ignored for the symbol families.</param>
    /// <param name="italic">Whether the face slants; ignored for the symbol families.</param>
    /// <returns>The name, such as Arimo-Bold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ResourceName(BundledFamily family, bool bold, bool italic) => Names[SlotOf(family, bold, italic)];

    /// <summary>Gets a face's TrueType font program, decoding it on first use.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold; ignored for the symbol families.</param>
    /// <param name="italic">Whether the face slants; ignored for the symbol families.</param>
    /// <returns>The program, which callers must not change, or <see langword="null"/> when the resource is damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[]? Program(PdfFontResources resources, BundledFamily family, bool bold, bool italic) => Program(resources.Faces, SlotOf(family, bold, italic));

    /// <summary>Decodes a face's font program from its resource without caching it.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold; ignored for the symbol families.</param>
    /// <param name="italic">Whether the face slants; ignored for the symbol families.</param>
    /// <returns>A new copy of the program, or <see langword="null"/> when the resource is damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[]? Decode(BundledFamily family, bool bold, bool italic) => Decode(SlotOf(family, bold, italic));

    /// <summary>Reads and parses a face from its resource without caching the parsed face, as the first use of a face does.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold; ignored for the symbol families.</param>
    /// <param name="italic">Whether the face slants; ignored for the symbol families.</param>
    /// <returns>A new face, or <see langword="null"/> when the resource is damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SubstituteFace? Read(PdfFontResources resources, BundledFamily family, bool bold, bool italic) => Read(resources.Faces, SlotOf(family, bold, italic));

    /// <summary>Finds the slot of a face.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <param name="italic">Whether the face slants.</param>
    /// <returns>The slot.</returns>
    internal static int SlotOf(BundledFamily family, bool bold, bool italic) => family switch
    {
        BundledFamily.Symbol => SymbolSlot,
        BundledFamily.Dingbats => DingbatsSlot,
        _ => ((int)family * StylesPerFamily) + (bold ? BoldBit : 0) + (italic ? ItalicBit : 0),
    };

    /// <summary>Makes a face from the shared parsed program and publishes it.</summary>
    /// <param name="state">The selected service's face state.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The face, or <see langword="null"/>.</returns>
    private static SubstituteFace? Load(BundledFaceState state, int slot)
    {
        if (Parsed(state, slot) is not { } program)
        {
            return null;
        }

        var face = new SubstituteFace(program.Program, Names[slot], program.Maps);
        return Interlocked.CompareExchange(ref state.Faces[slot], face, null) ?? face;
    }

    /// <summary>
    /// Gets the parsed font program of a slot, parsing it on first use. A parsed program never changes and builds its glyph
    /// name index once, so documents using these resources share it, and the code maps built for it, instead of parsing it and
    /// sorting its names again.
    /// </summary>
    /// <param name="state">The selected service's face state.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The program, or <see langword="null"/>.</returns>
    private static BundledProgram? Parsed(BundledFaceState state, int slot)
    {
        var cached = Volatile.Read(ref state.ParsedPrograms[slot]);
        if (cached is not null)
        {
            return cached;
        }

        if (Program(state, slot) is not { } data || !TrueTypeProgram.TryParse(data, out var program))
        {
            return null;
        }

        var created = new BundledProgram(program, new());
        return Interlocked.CompareExchange(ref state.ParsedPrograms[slot], created, null) ?? created;
    }

    /// <summary>Parses a face from its shared font program.</summary>
    /// <param name="state">The selected service's face state.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>A new face, or <see langword="null"/>.</returns>
    private static SubstituteFace? Read(BundledFaceState state, int slot) =>
        Program(state, slot) is { } data && TrueTypeProgram.TryParse(data, out var program) ? new SubstituteFace(program, Names[slot], new()) : null;

    /// <summary>Gets the shared font program of a slot, decoding it on first use.</summary>
    /// <param name="state">The selected service's face state.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The program, or <see langword="null"/>.</returns>
    private static byte[]? Program(BundledFaceState state, int slot) =>
        Volatile.Read(ref state.Decoded[slot]) ?? (Decode(slot) is { } decoded ? Interlocked.CompareExchange(ref state.Decoded[slot], decoded, null) ?? decoded : null);

    /// <summary>Decodes a slot's font program from the assembly resource.</summary>
    /// <param name="slot">The slot.</param>
    /// <returns>A new copy of the program, or <see langword="null"/> when the resource is missing or damaged.</returns>
    private static byte[]? Decode(int slot)
    {
        using var stream = typeof(BundledFaces).Assembly.GetManifestResourceStream($"HyperPdfLibrary.Faces.{Names[slot]}.ttf.z");
        if (stream is null)
        {
            return null;
        }

        var length = (int)stream.Length;
        var packed = ArrayPool<byte>.Shared.Rent(length);
        var output = new PooledBuffer(0);
        try
        {
            stream.ReadExactly(packed, 0, length);
            return ZLibCodec.Decompress(packed.AsSpan(0, length), false, ref output) ? output.ToArray() : null;
        }
        finally
        {
            output.Dispose();
            ArrayPool<byte>.Shared.Return(packed);
        }
    }
}

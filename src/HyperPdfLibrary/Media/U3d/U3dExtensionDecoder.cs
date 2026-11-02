// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Decodes New Object Type blocks (ECMA-363 9.4.5), which name the block types of an extension. Only the free-form curve and surface extension is understood.</summary>
internal static class U3dExtensionDecoder
{
    /// <summary>The first part of the free-form extension's identifier.</summary>
    private const uint FreeFormA = 0x095467DF;

    /// <summary>The second part of the free-form extension's identifier.</summary>
    private const ushort FreeFormB = 0xEE24;

    /// <summary>The third part of the free-form extension's identifier.</summary>
    private const ushort FreeFormC = 0x4C59;

    /// <summary>The model resource modifier chain type.</summary>
    private const uint ModelResourceChain = 1;

    /// <summary>The bytes of the last eight parts of an extension identifier.</summary>
    private const int TailLength = 8;

    /// <summary>Gets the last eight bytes of the free-form extension's identifier.</summary>
    private static ReadOnlySpan<byte> FreeFormTail => [0x9A, 0xA5, 0x76, 0x8E, 0x39, 0xE2, 0x68, 0xDE];

    /// <summary>Reads a New Object Type block and notes the block types of the free-form extension when it is that.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the block types or a problem.</param>
    internal static void Read(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        var chain = reader.ReadU32();
        var a = reader.ReadU32();
        var b = reader.ReadU16();
        var c = reader.ReadU16();
        Span<byte> tail = stackalloc byte[TailLength];
        for (var i = 0; i < tail.Length; i++)
        {
            tail[i] = reader.ReadU8();
        }

        var declaration = reader.ReadU32();
        var continuations = reader.ReadU32();
        var continuation = continuations != 0 ? reader.ReadU32() : 0;
        if (reader.Failed)
        {
            builder.Problem("A New Object Type block is cut short.");
            return;
        }

        var isFreeForm = a == FreeFormA && b == FreeFormB && c == FreeFormC && tail.SequenceEqual(FreeFormTail) && chain == ModelResourceChain && continuations != 0;
        if (!isFreeForm)
        {
            builder.Problem($"The file uses an extension ({name}) that is not decoded.");
            return;
        }

        builder.FreeFormDeclarationType = declaration;
        builder.FreeFormContinuationType = continuation;
    }
}

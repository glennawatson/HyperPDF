// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Decodes GIF image data: Lempel-Ziv-Welch codes of growing width, packed from the low bit of each byte.</summary>
internal static class GifLzw
{
    /// <summary>The longest string the table can hold, plus one.</summary>
    private const int StackSize = 4097;

    /// <summary>The smallest code size a GIF may declare.</summary>
    private const int MinimumDeclaredSize = 2;

    /// <summary>The largest code size a GIF may declare.</summary>
    private const int MaximumDeclaredSize = 8;

    /// <summary>Decodes the data to colour indexes.</summary>
    /// <param name="data">The joined data sub-blocks.</param>
    /// <param name="minimumCodeSize">The code size the image declares, from 2 to 8.</param>
    /// <param name="pixelCount">The indexes wanted.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The indexes; any the data did not reach are zero.</returns>
    /// <exception cref="InvalidDataException">The code size is not allowed, or a code names an entry that does not exist.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static byte[] Decode(byte[] data, int minimumCodeSize, int pixelCount, CancellationToken cancellationToken)
    {
        if (minimumCodeSize is < MinimumDeclaredSize or > MaximumDeclaredSize)
        {
            throw new InvalidDataException("The GIF declares a code size it may not.");
        }

        var output = new byte[pixelCount];
        var table = new GifCodeTable(minimumCodeSize);
        var reader = new GifBitReader(data);
        var stack = new byte[StackSize];
        var written = 0;
        var previous = -1;
        var first = 0;
        while (written < pixelCount && reader.TryRead(table.CodeSize, out var code))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (code == table.End)
            {
                break;
            }

            if (code == table.Clear)
            {
                table.Reset();
                previous = -1;
                continue;
            }

            if (previous < 0)
            {
                output[written] = (byte)(code < table.Clear ? code : 0);
                written++;
                previous = code;
                first = code;
                continue;
            }

            var length = table.Expand(code, previous, first, stack);
            first = stack[length - 1];
            written = Emit(output, written, stack, length);
            table.Add(previous, (byte)first);
            previous = code;
        }

        return output;
    }

    /// <summary>Copies a string from the stack to the output, first character first.</summary>
    /// <param name="output">The indexes.</param>
    /// <param name="written">How many indexes are written.</param>
    /// <param name="stack">The string, last character first.</param>
    /// <param name="length">The characters on the stack.</param>
    /// <returns>How many indexes are written now.</returns>
    private static int Emit(byte[] output, int written, byte[] stack, int length)
    {
        for (var i = length - 1; i >= 0 && written < output.Length; i--)
        {
            output[written] = stack[i];
            written++;
        }

        return written;
    }
}

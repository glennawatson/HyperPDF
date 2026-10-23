// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The table of strings a GIF's Lempel-Ziv-Welch codes stand for.</summary>
[DebuggerDisplay("GifCodeTable: {Next} entries, {CodeSize} bits")]
internal sealed class GifCodeTable
{
    /// <summary>The most entries the table holds.</summary>
    private const int TableSize = 4096;

    /// <summary>The codes after the single-character ones that are not strings: clear and end.</summary>
    private const int ReservedCodes = 2;

    /// <summary>The widest a code is, in bits.</summary>
    private const int MaximumCodeSize = 12;

    /// <summary>The prefix link of each entry: the code of the string without its last character.</summary>
    private readonly short[] _prefix = new short[TableSize];

    /// <summary>The last character of each entry.</summary>
    private readonly byte[] _suffix = new byte[TableSize];

    /// <summary>The code size the image declared.</summary>
    private readonly int _minimumCodeSize;

    /// <summary>Initializes a new instance of the <see cref="GifCodeTable"/> class.</summary>
    /// <param name="minimumCodeSize">The code size the image declares, from 2 to 8.</param>
    internal GifCodeTable(int minimumCodeSize)
    {
        _minimumCodeSize = minimumCodeSize;
        Reset();
    }

    /// <summary>Gets the code that empties the table.</summary>
    internal int Clear => 1 << _minimumCodeSize;

    /// <summary>Gets the code that ends the data.</summary>
    internal int End => Clear + 1;

    /// <summary>Gets the next free entry.</summary>
    internal int Next { get; private set; }

    /// <summary>Gets the width of the next code, in bits.</summary>
    internal int CodeSize { get; private set; }

    /// <summary>Empties the table back to the single-character strings.</summary>
    internal void Reset()
    {
        CodeSize = _minimumCodeSize + 1;
        Next = Clear + ReservedCodes;
    }

    /// <summary>Adds the string made of a previous string and the first character of the one after it.</summary>
    /// <param name="previous">The code of the previous string.</param>
    /// <param name="first">The first character of the string that follows it.</param>
    internal void Add(int previous, byte first)
    {
        if (Next >= TableSize)
        {
            return;
        }

        _prefix[Next] = (short)previous;
        _suffix[Next] = first;
        Next++;
        if (Next == (1 << CodeSize) && CodeSize < MaximumCodeSize)
        {
            CodeSize++;
        }
    }

    /// <summary>Writes the string a code stands for onto a stack, last character first.</summary>
    /// <param name="code">The code.</param>
    /// <param name="previous">The code before it.</param>
    /// <param name="first">The first character of the previous string.</param>
    /// <param name="stack">Receives the characters.</param>
    /// <returns>The number of characters on the stack.</returns>
    /// <exception cref="InvalidDataException">The code names an entry that does not exist.</exception>
    internal int Expand(int code, int previous, int first, byte[] stack)
    {
        if (code > Next)
        {
            throw new InvalidDataException("The GIF data names a code that does not exist.");
        }

        var length = 0;
        var current = code;
        if (code == Next)
        {
            stack[length] = (byte)first;
            length++;
            current = previous;
        }

        while (current >= Clear)
        {
            stack[length] = _suffix[current];
            length++;
            current = _prefix[current];
        }

        stack[length] = (byte)current;
        return length + 1;
    }
}

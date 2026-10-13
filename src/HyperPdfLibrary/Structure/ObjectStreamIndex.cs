// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Structure;

/// <summary>A decoded object stream and the offsets of the objects packed in it.</summary>
[DebuggerDisplay("ObjectStreamIndex: {Count} objects")]
internal sealed class ObjectStreamIndex
{
    /// <summary>The fewest bytes a header entry takes ("1 0"), bounding the declared count.</summary>
    private const int MinimumHeaderEntry = 2;

    /// <summary>The integers kept per object: its number and its offset.</summary>
    private const int HeaderIntsPerObject = 2;

    /// <summary>The decoded stream data, which parsed strings point into.</summary>
    private readonly byte[] _data;

    /// <summary>The object numbers, in order.</summary>
    private readonly int[] _numbers;

    /// <summary>The offsets of the objects, relative to the first.</summary>
    private readonly int[] _offsets;

    /// <summary>The offset of the first object.</summary>
    private readonly int _first;

    /// <summary>Initializes a new instance of the <see cref="ObjectStreamIndex"/> class.</summary>
    /// <param name="data">The decoded data.</param>
    /// <param name="numbers">The object numbers.</param>
    /// <param name="offsets">The relative offsets.</param>
    /// <param name="first">The offset of the first object.</param>
    private ObjectStreamIndex(byte[] data, int[] numbers, int[] offsets, int first)
    {
        _data = data;
        _numbers = numbers;
        _offsets = offsets;
        _first = first;
    }

    /// <summary>Gets the number of objects.</summary>
    public int Count => _numbers.Length;

    /// <summary>Gets the bytes the decoded data and the header arrays hold.</summary>
    public long ByteSize => _data.Length + ((long)_numbers.Length * HeaderIntsPerObject * sizeof(int));

    /// <summary>Gets the object numbers, in order.</summary>
    public ReadOnlySpan<int> Numbers => _numbers;

    /// <summary>Decodes an object stream and reads its header of object numbers and offsets.</summary>
    /// <param name="stream">The object stream.</param>
    /// <returns>The index.</returns>
    internal static ObjectStreamIndex Read(PdfStream stream)
    {
        var data = stream.DecodeToArray();
        var count = Math.Clamp(stream.Dictionary.GetInt32(KnownName.N), 0, data.Length / MinimumHeaderEntry);
        var first = Math.Clamp(stream.Dictionary.GetInt32(KnownName.First), 0, data.Length);
        var numbers = new int[count];
        var offsets = new int[count];
        var lexer = new PdfLexer(data);
        var read = 0;
        var context = stream.Dictionary.Owner?.Context;
        while (read < count && lexer.Position < first)
        {
            PdfOpenContext.ThrowIfCancelled(context);
            if (lexer.Next() != PdfTokenKind.Number || !PdfNumber.TryParse(lexer.Lexeme, out var number)
                || lexer.Next() != PdfTokenKind.Number || !PdfNumber.TryParse(lexer.Lexeme, out var offset))
            {
                break;
            }

            numbers[read] = number.AsInt32();
            offsets[read] = offset.AsInt32();
            read++;
        }

        // A header that holds every declared entry, as a well-formed one does, keeps its arrays as read; only a short one is copied.
        return read == count ? new(data, numbers, offsets, first) : new(data, numbers.AsSpan(0, read).ToArray(), offsets.AsSpan(0, read).ToArray(), first);
    }

    /// <summary>Parses an object.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="index">The index the cross-reference table gives; searched for when it is wrong.</param>
    /// <param name="store">The objects references resolve against.</param>
    /// <returns>The value; null when the stream does not hold the object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfValue Parse(int number, int index, PdfObjectStore store) => TryParse(number, index, store, out var value) ? value : default;

    /// <summary>Parses an object, telling a missing object apart from a null one.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="index">The index the cross-reference table gives; searched for when it is wrong.</param>
    /// <param name="store">The objects references resolve against.</param>
    /// <param name="value">The value; null when the stream does not hold the object.</param>
    /// <returns><see langword="true"/> when the stream holds the object.</returns>
    internal bool TryParse(int number, int index, PdfObjectStore store, out PdfValue value)
    {
        value = default;
        if ((uint)index >= (uint)_numbers.Length || _numbers[index] != number)
        {
            index = Array.IndexOf(_numbers, number);
            if (index < 0)
            {
                return false;
            }
        }

        var position = (long)_first + _offsets[index];
        if (position < 0 || position >= _data.Length)
        {
            return false;
        }

        // Strings in object streams are encrypted as part of the stream, not individually.
        var parser = new PdfParser(_data, (int)position, store, store.Names);
        value = parser.ParseValue();
        return true;
    }

    /// <summary>Parses the object at a position in the stream, for rebuilding a damaged table.</summary>
    /// <param name="index">The position in the stream.</param>
    /// <param name="store">The objects references resolve against.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfValue ParseAt(int index, PdfObjectStore store) => Parse(_numbers[index], index, store);
}

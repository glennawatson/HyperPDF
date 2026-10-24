// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>
/// Turns the codes a network outputs back into characters. A simple script has one code for each character; scripts with
/// thousands of characters, such as Han, build each from a short run of codes, which a tree of the runs unpicks.
/// </summary>
[DebuggerDisplay("CharacterCoder: {CodeRange} codes")]
public sealed class CharacterCoder
{
    /// <summary>The most codes one character may use.</summary>
    private const int MaxCodeLength = 9;

    /// <summary>The root of the tree of code runs.</summary>
    private readonly CodeNode _root = new();

    /// <summary>Initializes a new instance of the <see cref="CharacterCoder"/> class.</summary>
    /// <param name="codes">The codes of each character, indexed by the character's number.</param>
    private CharacterCoder(int[][] codes)
    {
        var range = 0;
        for (var id = 0; id < codes.Length; id++)
        {
            var node = _root;
            foreach (var code in codes[id])
            {
                range = Math.Max(range, code + 1);
                node = node.Child(code);
            }

            node.Character = id;
        }

        CodeRange = range;
    }

    /// <summary>Gets the number of different codes, which is the number of outputs of the network.</summary>
    public int CodeRange { get; }

    /// <summary>Reads the coder from its stored form.</summary>
    /// <param name="reader">The reader, placed at the start of the table.</param>
    /// <returns>The coder.</returns>
    /// <exception cref="InvalidDataException">The data is damaged.</exception>
    public static CharacterCoder Read(ref ModelReader reader)
    {
        var count = reader.ReadUInt32();
        _ = reader.CheckedLength(count, sizeof(byte) + sizeof(int));
        var codes = new int[count][];
        for (var id = 0; id < codes.Length; id++)
        {
            _ = reader.ReadByte();
            var length = reader.ReadInt32();
            if ((uint)length > MaxCodeLength)
            {
                throw new InvalidDataException("A character has an invalid number of codes.");
            }

            codes[id] = new int[length];
            for (var i = 0; i < length; i++)
            {
                codes[id][i] = reader.ReadInt32();
            }
        }

        return new(codes);
    }

    /// <summary>Starts decoding a run of codes at the start of a character.</summary>
    /// <returns>The start of the tree.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CodeNode Start() => _root;

    /// <summary>A place in the tree of code runs.</summary>
    [DebuggerDisplay("CodeNode: character {Character}")]
    public sealed class CodeNode
    {
        /// <summary>The places reached by one more code.</summary>
        private Dictionary<int, CodeNode>? _children;

        /// <summary>Gets or sets the character the codes so far spell, or -1 when they spell none.</summary>
        public int Character { get; set; } = -1;

        /// <summary>Gets the place reached by one more code.</summary>
        /// <param name="code">The code.</param>
        /// <param name="child">Receives the place.</param>
        /// <returns><see langword="true"/> when some character continues with the code.</returns>
        public bool TryGetChild(int code, out CodeNode child)
        {
            if (_children is not null && _children.TryGetValue(code, out var found))
            {
                child = found;
                return true;
            }

            child = this;
            return false;
        }

        /// <summary>Gets the place reached by one more code, making it if it is missing.</summary>
        /// <param name="code">The code.</param>
        /// <returns>The place.</returns>
        internal CodeNode Child(int code)
        {
            _children ??= [];
            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_children, code, out _);
            return slot ??= new();
        }
    }
}

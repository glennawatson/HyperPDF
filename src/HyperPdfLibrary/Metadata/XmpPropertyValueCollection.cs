// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Metadata;

/// <summary>Collects array items and detaches the values retained by a metadata snapshot.</summary>
internal static class XmpPropertyValueCollection
{
    /// <summary>The initial array capacity.</summary>
    private const int InitialCapacity = 4;

    /// <summary>The capacity growth factor.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Adds an item, placing default-language items first.</summary>
    /// <param name="values">The collected items.</param>
    /// <param name="text">The item text.</param>
    /// <param name="isDefault">Whether to put it first.</param>
    internal static void Add(ref XmpPropertyValues values, string text, bool isDefault)
    {
        var buffer = values.Buffer;
        if (buffer is null)
        {
            buffer = ArrayPool<string>.Shared.Rent(InitialCapacity);
        }
        else if (values.Count == buffer.Length)
        {
            var larger = ArrayPool<string>.Shared.Rent(buffer.Length * GrowthFactor);
            buffer.AsSpan(0, values.Count).CopyTo(larger);
            ArrayPool<string>.Shared.Return(buffer, clearArray: true);
            buffer = larger;
        }

        if (isDefault)
        {
            Array.Copy(buffer, 0, buffer, 1, values.Count);
            buffer[0] = text;
        }
        else
        {
            buffer[values.Count] = text;
        }

        values = new(buffer, values.Count + 1);
    }

    /// <summary>Copies only collected values into independently owned storage.</summary>
    /// <param name="values">The collected items.</param>
    /// <returns>The detached values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string[] ToArray(in XmpPropertyValues values) => values.Buffer.AsSpan(0, values.Count).ToArray();

    /// <summary>Clears and returns the item storage.</summary>
    /// <param name="values">The collected items.</param>
    internal static void Release(ref XmpPropertyValues values)
    {
        if (values.Buffer is { } buffer)
        {
            ArrayPool<string>.Shared.Return(buffer, clearArray: true);
        }

        values = default;
    }
}

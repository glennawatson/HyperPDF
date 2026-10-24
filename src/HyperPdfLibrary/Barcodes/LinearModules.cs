// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;

namespace HyperPdfLibrary.Barcodes;

/// <summary>Builds the one row of modules of a linear barcode from runs of bars and spaces, in a pooled buffer that <see cref="ToBarcode"/> returns.</summary>
[DebuggerDisplay("LinearModules: {_count} modules")]
internal ref struct LinearModules
{
    /// <summary>The factor a full buffer grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The size of the first buffer, which holds a short barcode without growing.</summary>
    private const int InitialCapacity = 256;

    /// <summary>The modules so far; true is dark.</summary>
    private bool[]? _modules;

    /// <summary>The modules in use.</summary>
    private int _count;

    /// <summary>Whether the next run is a space instead of a bar; runs alternate between bars and spaces.</summary>
    private bool _nextIsSpace;

    /// <summary>Adds a run of modules.</summary>
    /// <param name="width">The width in modules.</param>
    internal void AddRun(int width)
    {
        Reserve(width);
        _modules.AsSpan(_count, width).Fill(!_nextIsSpace);
        _count += width;
        _nextIsSpace = !_nextIsSpace;
    }

    /// <summary>Adds runs from a string of digits, each the width of one run, starting with a bar.</summary>
    /// <param name="widths">The widths, such as <c>212222</c>.</param>
    internal void AddWidths(ReadOnlySpan<char> widths)
    {
        foreach (var width in widths)
        {
            AddRun(width - '0');
        }
    }

    /// <summary>Adds modules from a string of zeros and ones, where one is dark.</summary>
    /// <param name="bits">The modules, such as <c>0001101</c>.</param>
    internal void AddBits(ReadOnlySpan<char> bits)
    {
        Reserve(bits.Length);
        foreach (var bit in bits)
        {
            _modules![_count] = bit == '1';
            _count++;
        }

        _nextIsSpace = _modules![_count - 1];
    }

    /// <summary>Makes the barcode and returns the buffer to the pool.</summary>
    /// <param name="symbology">The barcode type.</param>
    /// <param name="text">The text it stands for.</param>
    /// <returns>The barcode.</returns>
    internal PdfBarcode ToBarcode(PdfBarcodeSymbology symbology, string text)
    {
        var modules = _modules.AsSpan(0, _count).ToArray();
        ArrayPool<bool>.Shared.Return(_modules!);
        _modules = null;
        return new(symbology, text, modules.Length, modules);
    }

    /// <summary>Makes room for more modules.</summary>
    /// <param name="extra">How many modules are about to be added.</param>
    private void Reserve(int extra)
    {
        if (_modules is null)
        {
            _modules = ArrayPool<bool>.Shared.Rent(Math.Max(InitialCapacity, extra));
            return;
        }

        if (_count + extra <= _modules.Length)
        {
            return;
        }

        var bigger = ArrayPool<bool>.Shared.Rent(Math.Max(_modules.Length * GrowthFactor, _count + extra));
        _modules.AsSpan(0, _count).CopyTo(bigger);
        ArrayPool<bool>.Shared.Return(_modules);
        _modules = bigger;
    }
}

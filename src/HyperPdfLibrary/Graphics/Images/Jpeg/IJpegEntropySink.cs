// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// Receives the Huffman symbols and raw bits of a scan. One scan routine drives both a counter that gathers symbol
/// frequencies for optimised tables and the writer that produces the entropy-coded bytes.
/// </summary>
internal interface IJpegEntropySink
{
    /// <summary>Takes a Huffman symbol followed by its extra bits.</summary>
    /// <param name="table">The table slot: 0 and 1 are the DC tables, 2 and 3 the AC tables.</param>
    /// <param name="symbol">The symbol from 0 to 255.</param>
    /// <param name="bits">The extra bits, right aligned.</param>
    /// <param name="bitCount">The number of extra bits, up to 15.</param>
    void Symbol(int table, int symbol, uint bits, int bitCount);

    /// <summary>Takes a Huffman symbol with no extra bits.</summary>
    /// <param name="table">The table slot: 0 and 1 are the DC tables, 2 and 3 the AC tables.</param>
    /// <param name="symbol">The symbol from 0 to 255.</param>
    void Symbol(int table, int symbol);

    /// <summary>Takes raw bits that no Huffman code precedes.</summary>
    /// <param name="bits">The bits, right aligned.</param>
    /// <param name="bitCount">The number of bits, up to 16.</param>
    void Bits(uint bits, int bitCount);

    /// <summary>Ends a restart interval: pads to a byte boundary and writes the next restart marker.</summary>
    /// <param name="index">The number of the interval that just ended, counting from zero.</param>
    void Restart(int index);

    /// <summary>Ends the scan: pads the last byte with ones.</summary>
    void EndScan();
}

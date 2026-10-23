// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>One square Data Matrix size and how it splits its codewords.</summary>
/// <param name="Side">The modules across, including the finder and clock edges.</param>
/// <param name="DataBytes">The data codewords it holds.</param>
/// <param name="EccBytes">The error correction codewords across all blocks.</param>
/// <param name="Regions">The data regions along each side.</param>
/// <param name="Blocks">The Reed-Solomon blocks the codewords are interleaved in.</param>
internal readonly record struct DataMatrixSize(int Side, int DataBytes, int EccBytes, int Regions, int Blocks)
{
    /// <summary>The finder and clock edge modules a region adds to its data, counting both sides.</summary>
    internal const int EdgeModules = 2;

    /// <summary>Gets the modules across the data area the codewords are placed in, without the finder and clock edges.</summary>
    internal int MappingSide => Side - (EdgeModules * Regions);

    /// <summary>Gets the modules across one data region.</summary>
    internal int RegionSide => MappingSide / Regions;
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Holds leased range builders and lazily created Unicode storage for one binary CMap parse.</summary>
/// <param name="Parent">The inherited CMap name.</param>
/// <param name="Vertical">Whether the writing mode is vertical.</param>
/// <param name="Codespaces">The leased codespace builder.</param>
/// <param name="CodespaceCount">The number of populated codespaces.</param>
/// <param name="Ranges">The leased CID range builder.</param>
/// <param name="RangeCount">The number of populated CID ranges.</param>
/// <param name="Unicode">The leased Unicode table builder, absent until a usable mapping is read.</param>
/// <param name="UnicodeCount">The table length through the last populated CID.</param>
internal readonly record struct BinaryCMapState(
    string Parent,
    bool Vertical,
    Codespace[]? Codespaces,
    int CodespaceCount,
    CidRange[]? Ranges,
    int RangeCount,
    int[]? Unicode,
    int UnicodeCount);

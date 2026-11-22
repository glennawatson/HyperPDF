// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Compat;

/// <summary>What reading zlib data through a window, without keeping its output, found.</summary>
internal enum ZLibCheck
{
    /// <summary>The data is not plainly sound: a full decode finds what is wrong, or what the lenient reader makes of it.</summary>
    Unsound = 0,

    /// <summary>The data ended cleanly within the length allowed.</summary>
    Clean = 1,

    /// <summary>The data was sound as far as it was read, and decodes to more than the length allowed.</summary>
    TooLong = 2,
}

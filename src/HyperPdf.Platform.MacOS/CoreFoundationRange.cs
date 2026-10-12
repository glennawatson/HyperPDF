// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.MacOS;

/// <summary>Native <c>CFRange</c>; CFIndex is 64 bits on every supported Mac.</summary>
/// <param name="Location">The first index.</param>
/// <param name="Length">The count.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CoreFoundationRange(long Location, long Length);

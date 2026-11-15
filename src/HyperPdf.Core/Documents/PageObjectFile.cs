// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A file made from a page object, such as a picture saved from the page.</summary>
/// <param name="Extension">The file's extension, without the dot, such as <c>png</c> or <c>jpg</c>.</param>
/// <param name="Data">The file's bytes.</param>
[DebuggerDisplay("PageObjectFile: .{Extension}, {Data.Length} bytes")]
public sealed record PageObjectFile(string Extension, byte[] Data);

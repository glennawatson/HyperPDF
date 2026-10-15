// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Http;

/// <summary>The resources used to copy and hash a download.</summary>
/// <param name="Buffer">The reusable copy buffer.</param>
/// <param name="Hashes">The contexts owned by the download service.</param>
internal readonly record struct DownloadCopy(byte[] Buffer, DownloadHashes Hashes);

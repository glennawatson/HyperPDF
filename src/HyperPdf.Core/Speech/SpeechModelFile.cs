// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Speech;

/// <summary>A file the on-device voice needs, downloaded once.</summary>
/// <param name="Source">Where it is downloaded from.</param>
/// <param name="LocalName">Its path inside the voice folder.</param>
/// <param name="Bytes">Its size in bytes; a file of another size is downloaded again.</param>
/// <param name="Sha256">Its SHA-256 as lowercase hexadecimal; a download that does not match is discarded.</param>
[DebuggerDisplay("SpeechModelFile: {LocalName}")]
public sealed record SpeechModelFile(Uri Source, string LocalName, long Bytes, string Sha256);

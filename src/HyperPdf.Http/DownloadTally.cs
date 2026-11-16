// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Http;

/// <summary>The bytes received so far by the downloads of one batch.</summary>
internal sealed class DownloadTally
{
    /// <summary>Gets or sets the bytes received by the files that are finished.</summary>
    internal long Done { get; set; }
}

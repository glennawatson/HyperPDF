// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Which data an AES-GCM test file protects.</summary>
public enum AesGcmLayout
{
    /// <summary>The default crypt filter protects every string and stream.</summary>
    Everything = 0,

    /// <summary>Only embedded file streams are protected, through /EFF; strings and other streams use Identity.</summary>
    EmbeddedFilesOnly = 1,
}

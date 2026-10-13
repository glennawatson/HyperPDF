// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.ViewModels;

/// <summary>What an external signer gives back.</summary>
public enum ExternalResultKind
{
    /// <summary>Only the signature value over the bytes to sign; the app builds the signature container.</summary>
    SignatureValue = 0,

    /// <summary>A whole signature container (CMS) that a signing service built over the document digest.</summary>
    Container = 1,
}

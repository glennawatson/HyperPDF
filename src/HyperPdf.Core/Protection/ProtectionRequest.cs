// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Protection;

/// <summary>
/// What to protect a copy with. It holds passwords, so it has no readable text form: <see cref="ToString"/> names the type
/// only, and nothing here is logged or saved.
/// </summary>
[DebuggerDisplay("ProtectionRequest: {Format}")]
public sealed class ProtectionRequest
{
    /// <summary>Gets the encryption.</summary>
    public ProtectionFormat Format { get; init; }

    /// <summary>Gets the password that opens the copy with the permissions below; empty opens it without asking.</summary>
    public string UserPassword { get; init; } = string.Empty;

    /// <summary>Gets the password that opens the copy with every permission and may change or remove the protection.</summary>
    public string OwnerPassword { get; init; } = string.Empty;

    /// <summary>Gets what the user password allows.</summary>
    public ProtectionPermissions Permissions { get; init; } = ProtectionPermissions.All;

    /// <summary>Gets a value indicating whether the document properties (title, author) are hidden too.</summary>
    public bool EncryptMetadata { get; init; } = true;

    /// <summary>Gets the owner password of the open document's current protection, needed to change it when the document was opened with its user password; or <see langword="null"/>.</summary>
    public string? CurrentOwnerPassword { get; init; }

    /// <inheritdoc/>
    public override string ToString() => nameof(ProtectionRequest);
}

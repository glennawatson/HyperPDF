// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.App.ViewModels;

/// <summary>A kind of signature an external signer's key makes, offered in the external signing window.</summary>
/// <param name="Kind">The kind of signature.</param>
/// <param name="Name">The kind, in plain words.</param>
[DebuggerDisplay("SigningKeyChoice: {Name}")]
public sealed record SigningKeyChoice(SigningKeyKind Kind, string Name);

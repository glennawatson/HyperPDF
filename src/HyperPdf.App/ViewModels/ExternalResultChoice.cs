// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.ViewModels;

/// <summary>A kind of answer the external signer can give, offered in the external signing window.</summary>
/// <param name="Kind">The kind of answer.</param>
/// <param name="Name">What the signer gives, in plain words.</param>
[DebuggerDisplay("ExternalResultChoice: {Name}")]
public sealed record ExternalResultChoice(ExternalResultKind Kind, string Name);

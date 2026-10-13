// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Security;

/// <summary>The values of a new public-key encryption dictionary, and the file key they protect.</summary>
/// <param name="FileKey">The file encryption key.</param>
/// <param name="Recipients">The CMS objects, one for each set of permissions, in the order they are written and hashed.</param>
/// <param name="Permissions">The permission word of the first recipient list.</param>
[DebuggerDisplay("PublicKeyEntries: {Recipients.Length} recipient lists")]
internal sealed record PublicKeyEntries(byte[] FileKey, byte[][] Recipients, int Permissions);

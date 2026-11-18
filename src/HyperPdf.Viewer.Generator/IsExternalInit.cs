// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices;

/// <summary>Lets <c>init</c> accessors, and so records, compile for netstandard2.0, whose reference assemblies lack this type; the compiler only looks for its name.</summary>
internal static class IsExternalInit
{
    /// <summary>The compiler matches the type by name; the constant only gives it a member.</summary>
    private const int Marker = 0;
}

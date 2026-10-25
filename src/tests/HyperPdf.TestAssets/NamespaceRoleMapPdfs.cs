// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.TestAssets;

/// <summary>Builds raw namespace role-map chains with a finite end or a cycle.</summary>
public static class NamespaceRoleMapPdfs
{
    /// <summary>The first namespace's object number.</summary>
    private const int FirstNamespaceNumber = 4;

    /// <summary>The catalog and page objects before the namespaces.</summary>
    private const int FixedObjects = FirstNamespaceNumber - 1;

    /// <summary>Gets the first namespace's object number.</summary>
    public static int FirstNamespace => FirstNamespaceNumber;

    /// <summary>Creates a chain whose namespaces have distinct URI strings.</summary>
    /// <param name="length">The number of namespace dictionaries.</param>
    /// <param name="circular">Whether the last map points to the first namespace.</param>
    /// <returns>The complete PDF bytes.</returns>
    public static byte[] Build(int length, bool circular)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var objects = new string[checked(FixedObjects + length)];
        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[1] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";
        objects[FixedObjects - 1] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>";
        for (var index = 0; index < length; index++)
        {
            var next = 0;
            if (index + 1 < length)
            {
                next = FirstNamespace + index + 1;
            }
            else if (circular)
            {
                next = FirstNamespace;
            }

            var mapping = next == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"/RoleMapNS << /X [/X {next} 0 R] >>");
            objects[FixedObjects + index] = string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Namespace /NS (urn:example:namespace:{index}) {mapping} >>");
        }

        return MiniPdf.Build(objects);
    }
}

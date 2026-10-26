// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdf.Core.Licences;

namespace HyperPdf.ViewModels.Services;

/// <summary>
/// Reads the third-party notices file that the app copies to its output folder (<c>src/licenses/notices/HyperPdf.App.md</c>,
/// kept current by the refresh-notices workflow with <c>src/tools/GenerateThirdPartyNotices.cs</c>). Each application
/// lifetime reads the file once.
/// </summary>
internal sealed class LicenceNotices
{
    /// <summary>The name of the notices file beside the app.</summary>
    internal const string FileName = "THIRD-PARTY-NOTICES.md";

    /// <summary>The folder that holds the notices file.</summary>
    private readonly string _folder;

    /// <summary>The notices once read.</summary>
    private NoticeDocument? _document;

    /// <summary>Initializes a new instance of the <see cref="LicenceNotices"/> class.</summary>
    /// <param name="folder">The folder that holds the notices file.</param>
    internal LicenceNotices(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        _folder = folder;
    }

    /// <summary>Gets the notices shipped with the app.</summary>
    /// <returns>The notices; empty when the file is missing.</returns>
    internal NoticeDocument Load() => Volatile.Read(ref _document) ?? Read();

    /// <summary>Reads the file and publishes the parsed notices.</summary>
    /// <returns>The notices.</returns>
    private NoticeDocument Read()
    {
        var document = NoticeDocument.Empty;
        var path = Path.Combine(_folder, FileName);
        if (File.Exists(path))
        {
            using var stream = File.OpenRead(path);
            var length = checked((int)stream.Length);
            var bytes = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                stream.ReadExactly(bytes.AsSpan(0, length));
                document = NoticeDocument.Parse(bytes.AsSpan(0, length));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        return Interlocked.CompareExchange(ref _document, document, null) ?? document;
    }
}

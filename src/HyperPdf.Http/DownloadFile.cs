// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace HyperPdf.Http;

/// <summary>
/// One file a verified download keeps in a folder: its paths, built once, and the file's facts, read again each time
/// they are asked for so a change on disk is always seen. The hash recorded beside the file is read again only when
/// that file's size or time changes.
/// </summary>
[DebuggerDisplay("DownloadFile: {Target}")]
internal sealed class DownloadFile
{
    /// <summary>The suffix of the file beside each download that records its SHA-256.</summary>
    internal const string HashSuffix = ".sha256";

    /// <summary>The characters a hexadecimal GUID takes.</summary>
    private const int GuidLength = 32;

    /// <summary>The text between the target and the GUID of a partial file.</summary>
    private const string PartialSeparator = ".";

    /// <summary>The ending of a partial file's name.</summary>
    private const string PartialExtension = ".part";

    /// <summary>The format of a GUID with no separators.</summary>
    private const string GuidFormat = "N";

    /// <summary>Guards the facts read from the file system.</summary>
    private readonly Lock _gate = new();

    /// <summary>The file.</summary>
    private readonly FileInfo _info;

    /// <summary>The file beside it that records its hash.</summary>
    private readonly FileInfo _hashInfo;

    /// <summary>The path kept for the first download of the file at a time, or <see langword="null"/> before it is made.</summary>
    private string? _keptPartial;

    /// <summary>1 while a download holds <see cref="_keptPartial"/>.</summary>
    private int _partialTaken;

    /// <summary>The hash recorded beside the file when it was last read.</summary>
    private string _recorded = string.Empty;

    /// <summary>The time the recording file was last written when it was last read.</summary>
    private DateTime _recordedWritten;

    /// <summary>The size of the recording file when it was last read, or -1 before it is read.</summary>
    private long _recordedLength = -1;

    /// <summary>The expected hash whose UTF-8 bytes have been encoded.</summary>
    private string _encodedHash = string.Empty;

    /// <summary>The UTF-8 bytes written beside the download.</summary>
    private byte[] _encodedBytes = [];

    /// <summary>Initializes a new instance of the <see cref="DownloadFile"/> class.</summary>
    /// <param name="target">The full path of the file.</param>
    internal DownloadFile(string target)
    {
        Target = target;
        HashTarget = target + HashSuffix;
        Folder = Path.GetDirectoryName(target)!;
        _info = new(target);
        _hashInfo = new(HashTarget);
    }

    /// <summary>Gets the full path of the file.</summary>
    internal string Target { get; }

    /// <summary>Gets the full path of the file that records the hash.</summary>
    internal string HashTarget { get; }

    /// <summary>Gets the folder the file is kept in.</summary>
    internal string Folder { get; }

    /// <summary>
    /// Determines whether the file is the one wanted: it exists at the expected size and, when its download recorded a hash
    /// beside it, that hash matches. A new release changes the size or hash of any file it replaces, so updated files are
    /// downloaded again without hashing large files at every start.
    /// </summary>
    /// <param name="bytes">The expected size.</param>
    /// <param name="sha256">The expected SHA-256 as lowercase hexadecimal.</param>
    /// <returns><see langword="true"/> when it need not be downloaded.</returns>
    internal bool IsCurrent(long bytes, string sha256)
    {
        lock (_gate)
        {
            _info.Refresh();
            if (!_info.Exists || _info.Length != bytes)
            {
                return false;
            }

            _hashInfo.Refresh();
            return !_hashInfo.Exists || string.Equals(Recorded(), sha256, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Takes the path of a file a download writes to before it is complete. The first download takes a path kept for the
    /// file, so later downloads make no new path; a download that starts while another holds it gets a path of its own, so
    /// two downloads at once never write to the same file.
    /// </summary>
    /// <returns>The path; give it back with <see cref="ReturnPartialPath"/> when the download has finished.</returns>
    internal string TakePartialPath() => Interlocked.Exchange(ref _partialTaken, 1) != 0 ? NewPartialPath() : _keptPartial ??= NewPartialPath();

    /// <summary>Gives back a path taken with <see cref="TakePartialPath"/>.</summary>
    /// <param name="partial">The path.</param>
    internal void ReturnPartialPath(string partial)
    {
        if (ReferenceEquals(partial, _keptPartial))
        {
            Volatile.Write(ref _partialTaken, 0);
        }
    }

    /// <summary>Deletes the file and the one recording its hash.</summary>
    internal void Delete()
    {
        File.Delete(Target);
        File.Delete(HashTarget);
    }

    /// <summary>Gets the expected hash as UTF-8, encoding it only when the expected value changes.</summary>
    /// <param name="sha256">The expected hash.</param>
    /// <returns>The bytes to record beside the file.</returns>
    internal ReadOnlyMemory<byte> EncodedHash(string sha256)
    {
        lock (_gate)
        {
            if (!CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(_encodedHash.AsSpan()), MemoryMarshal.AsBytes(sha256.AsSpan())))
            {
                _encodedBytes = Encoding.UTF8.GetBytes(sha256);
                _encodedHash = sha256;
            }

            return _encodedBytes;
        }
    }

    /// <summary>Makes a path no other download uses, beside the target.</summary>
    /// <returns>The path.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string NewPartialPath() =>
        string.Create(Target.Length + PartialSeparator.Length + GuidLength + PartialExtension.Length, Target, static (span, target) =>
        {
            target.CopyTo(span);
            PartialSeparator.CopyTo(span[target.Length..]);
            _ = Guid.NewGuid().TryFormat(span[(target.Length + PartialSeparator.Length)..], out _, GuidFormat);
            PartialExtension.CopyTo(span[^PartialExtension.Length..]);
        });

    /// <summary>Gets the recorded hash, reading the recording file again only when it has changed.</summary>
    /// <returns>The hash.</returns>
    private string Recorded()
    {
        if (_recordedLength != _hashInfo.Length || _recordedWritten != _hashInfo.LastWriteTimeUtc)
        {
            _recorded = File.ReadAllText(HashTarget).Trim();
            _recordedLength = _hashInfo.Length;
            _recordedWritten = _hashInfo.LastWriteTimeUtc;
        }

        return _recorded;
    }
}

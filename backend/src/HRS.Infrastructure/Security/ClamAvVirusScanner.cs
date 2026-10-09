using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using HRS.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace HRS.Infrastructure.Security;

/// <summary>
/// A minimal client for clamd's INSTREAM protocol (stable since early ClamAV, undocumented
/// beyond clamd's own man page — hand-rolled here rather than taking a NuGet dependency for
/// ~60 lines of a simple, long-stable wire format): send "zINSTREAM\0", then the file as
/// length-prefixed chunks (4-byte big-endian length + bytes), terminated by a zero-length
/// chunk, then read back "stream: OK" (clean) or "stream: ... FOUND" (infected).
/// </summary>
public sealed class ClamAvVirusScanner(IOptions<ClamAvOptions> options) : IVirusScanner
{
    private const int ChunkSize = 8192;

    public async Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(options.Value.Host, options.Value.Port, cancellationToken);
        await using var stream = client.GetStream();

        var command = "zINSTREAM\0"u8.ToArray();
        await stream.WriteAsync(command, cancellationToken);

        var buffer = new byte[ChunkSize];
        var lengthPrefix = new byte[4];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, (uint)read);
            await stream.WriteAsync(lengthPrefix, cancellationToken);
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, 0);
        await stream.WriteAsync(lengthPrefix, cancellationToken);

        using var responseReader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var response = await responseReader.ReadLineAsync(cancellationToken) ?? string.Empty;

        if (response.Contains("FOUND", StringComparison.Ordinal))
        {
            return VirusScanResult.Infected;
        }

        if (response.Contains("OK", StringComparison.Ordinal))
        {
            return VirusScanResult.Clean;
        }

        throw new InvalidOperationException($"Unexpected response from clamd: '{response}'.");
    }
}

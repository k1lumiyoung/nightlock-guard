using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace NightLock.Core;

/// <summary>
/// Minimal SNTP client for obtaining a UTC transmit timestamp from an NTP server.
///
/// @spec spec://modules/core/FEAT-006-trusted-time-source#synchronization
/// </summary>
public static class SntpClient
{
    private const int PacketLength = 48;
    private const int TransmitTimestampOffset = 40;
    private static readonly DateTimeOffset NtpEpoch = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] BuildRequest()
    {
        var request = new byte[PacketLength];
        request[0] = 0x23;
        return request;
    }

    public static DateTimeOffset ParseTransmitTimestamp(byte[] response)
    {
        if (response.Length < PacketLength)
        {
            throw new ArgumentException("SNTP response is too short.", nameof(response));
        }

        var seconds = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(TransmitTimestampOffset, 4));
        var fraction = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(TransmitTimestampOffset + 4, 4));
        if (seconds == 0)
        {
            throw new ArgumentException("SNTP response has an empty transmit timestamp.", nameof(response));
        }

        var fractionalTicks = (long)Math.Round(fraction * (TimeSpan.TicksPerSecond / 4294967296d));
        var timestamp = NtpEpoch.AddSeconds(seconds).AddTicks(fractionalTicks);
        if (timestamp.Year is < 2020 or > 2100)
        {
            throw new ArgumentException("SNTP response transmit timestamp is not plausible.", nameof(response));
        }

        return timestamp;
    }

    public static DateTimeOffset? Query(string host, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(host) || timeout <= TimeSpan.Zero)
        {
            return null;
        }

        try
        {
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = (int)Math.Clamp(timeout.TotalMilliseconds, 1, int.MaxValue);
            udp.Connect(host.Trim(), 123);

            var request = BuildRequest();
            udp.Send(request, request.Length);

            var remote = new IPEndPoint(IPAddress.Any, 0);
            var response = udp.Receive(ref remote);
            if (!HasValidServerHeader(response))
            {
                return null;
            }

            return ParseTransmitTimestamp(response).ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    private static bool HasValidServerHeader(byte[] response)
    {
        if (response.Length < PacketLength)
        {
            return false;
        }

        var leapIndicator = response[0] >> 6;
        var mode = response[0] & 0x7;
        var stratum = response[1];
        return leapIndicator != 3 && mode is 4 or 5 && stratum is >= 1 and <= 15;
    }
}

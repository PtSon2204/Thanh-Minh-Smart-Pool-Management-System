using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SmartPool.IntegrationTests;

// A loopback RTSP server emitting genuine H.264/H.265 RTP packets. No application success responses are mocked.
internal sealed class LocalRtspCamera : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentBag<TcpClient> clients = [];
    private readonly byte[][] nals;
    private readonly bool hevc;
    public bool FragmentVideo { get; set; }
    public bool AggregateParameters { get; set; }
    public bool OmitSdpParameters { get; set; }
    private readonly Task accepting;
    public bool RequireAuthentication { get; set; }
    public bool InvalidVideo { get; set; }
    public string? RedirectUrl { get; set; }
    public int AcceptedConnections;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public string Url => $"rtsp://127.0.0.1:{Port}/live";

    private LocalRtspCamera(byte[][] nals, bool hevc)
    {
        this.nals = nals;
        this.hevc = hevc;
        listener.Start();
        accepting = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(stop.Token);
                    clients.Add(client);
                    Interlocked.Increment(ref AcceptedConnections);
                    _ = HandleAsync(client);
                }
            }
            catch (Exception) when (stop.IsCancellationRequested) { }
        });
    }

    public static async Task<LocalRtspCamera> StartAsync(string ffmpeg, bool hevc = false, string color = "blue")
    {
        var file = Path.Combine(Path.GetTempPath(), "smartpool-camera-" + Guid.NewGuid().ToString("N") + ".h264");
        try
        {
            var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"color=c={color}:s=64x64:r=1", "-frames:v", "1", "-c:v", hevc ? "libx265" : "libx264", "-preset", "ultrafast", "-tune", "zerolatency", "-f", hevc ? "hevc" : "h264", file }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new InvalidOperationException("Unable to generate H.264 test fixture: " + await error);
            return new LocalRtspCamera(SplitNals(await File.ReadAllBytesAsync(file)), hevc);
        }
        finally { File.Delete(file); }
    }

    private async Task HandleAsync(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            while (!stop.IsCancellationRequested)
            {
                var header = await ReadHeaderAsync(stream, stop.Token);
                if (header == null) return;
                var lines = header.Split("\r\n");
                var method = lines[0].Split(' ')[0];
                var cseq = lines.First(x => x.StartsWith("CSeq:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim();
                var extra = string.Empty;
                var body = string.Empty;
                var code = "200 OK";
                if (method != "OPTIONS" && RequireAuthentication && !lines.Any(x => x.Trim().Equals("Authorization: Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("camera-test:camera-password-test")), StringComparison.OrdinalIgnoreCase)))
                {
                    code = "401 Unauthorized";
                    extra = "WWW-Authenticate: Basic realm=\"camera-test\"\r\n";
                }
                else if (method == "DESCRIBE" && RedirectUrl != null) { code = "302 Moved Temporarily"; extra = $"Location: {RedirectUrl}\r\n"; }
                else if (method == "OPTIONS") extra = "Public: OPTIONS, DESCRIBE, SETUP, PLAY, TEARDOWN\r\n";
                else if (method == "DESCRIBE")
                {
                    var sps = Convert.ToBase64String(nals.First(x => NalType(x) == (hevc ? 33 : 7)));
                    var pps = Convert.ToBase64String(nals.First(x => NalType(x) == (hevc ? 34 : 8)));
                    var fmtp = hevc ? $"sprop-vps={Convert.ToBase64String(nals.First(x => NalType(x) == 32))};sprop-sps={sps};sprop-pps={pps}" : $"packetization-mode=1;sprop-parameter-sets={sps},{pps}";
                    body = $"v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=Camera test\r\nt=0 0\r\na=control:*\r\nm=video 0 RTP/AVP 96\r\na=rtpmap:96 {(hevc ? "H265" : "H264")}/90000\r\n";
                    if (!OmitSdpParameters) body += $"a=fmtp:96 {fmtp}\r\n";
                    body += "a=control:trackID=0\r\n";
                    extra = $"Content-Type: application/sdp\r\nContent-Base: {Url}/\r\n";
                }
                else if (method == "SETUP") extra = "Transport: RTP/AVP/TCP;unicast;interleaved=0-1\r\nSession: test-session\r\n";
                else if (method == "PLAY") extra = "Session: test-session\r\n";
                var bytes = Encoding.ASCII.GetBytes($"RTSP/1.0 {code}\r\nCSeq: {cseq}\r\n{extra}Content-Length: {Encoding.ASCII.GetByteCount(body)}\r\n\r\n{body}");
                await stream.WriteAsync(bytes, stop.Token);
                if (method == "PLAY" && code == "200 OK") { await SendVideoAsync(stream); return; }
            }
        }
        catch (Exception) { /* Client disconnects and test shutdown are expected. */ }
        finally { client.Dispose(); }
    }

    private async Task SendVideoAsync(NetworkStream stream)
    {
        ushort sequence = 0;
        uint timestamp = 0;
        while (!stop.IsCancellationRequested)
        {
            var parameters = nals.Where(x => hevc ? NalType(x) is 32 or 33 or 34 : NalType(x) is 7 or 8).ToArray();
            var payloads = new List<(byte[] Data, bool Marker)>();
            if (hevc && AggregateParameters)
            {
                using var ap = new MemoryStream();
                ap.Write(new byte[] { 48 << 1, 1 });
                foreach (var nal in parameters) { ap.WriteByte((byte)(nal.Length >> 8)); ap.WriteByte((byte)nal.Length); ap.Write(nal); }
                payloads.Add((ap.ToArray(), false));
            }
            else payloads.AddRange(parameters.Select(x => (x, false)));
            foreach (var original in nals.Where(x => hevc ? NalType(x) is >= 16 and <= 21 : NalType(x) == 5))
            {
                var nal = InvalidVideo ? hevc ? new byte[] { 38, 1, 0 } : new byte[] { 0x65, 0 } : original;
                if (hevc && FragmentVideo)
                {
                    for (var offset = 2; offset < nal.Length; offset += 8)
                    {
                        var count = Math.Min(8, nal.Length - offset);
                        var last = offset + count == nal.Length;
                        var fu = new byte[count + 3];
                        fu[0] = (byte)((nal[0] & 0x81) | 49 << 1); fu[1] = nal[1];
                        fu[2] = (byte)(NalType(nal) | (offset == 2 ? 128 : 0) | (last ? 64 : 0));
                        Buffer.BlockCopy(nal, offset, fu, 3, count);
                        payloads.Add((fu, last));
                    }
                }
                else payloads.Add((nal, true));
            }
            foreach (var (nal, marker) in payloads)
            {
                var packet = new byte[16 + nal.Length];
                packet[0] = (byte)'$'; packet[1] = 0;
                packet[2] = (byte)((12 + nal.Length) >> 8); packet[3] = (byte)(12 + nal.Length);
                packet[4] = 0x80; packet[5] = (byte)(96 | (marker ? 128 : 0));
                packet[6] = (byte)(sequence >> 8); packet[7] = (byte)sequence++;
                packet[8] = (byte)(timestamp >> 24); packet[9] = (byte)(timestamp >> 16); packet[10] = (byte)(timestamp >> 8); packet[11] = (byte)timestamp;
                packet[15] = 1;
                Buffer.BlockCopy(nal, 0, packet, 16, nal.Length);
                await stream.WriteAsync(packet, stop.Token);
            }
            timestamp += 90000;
            await Task.Delay(500, stop.Token);
        }
    }

    public void DisconnectClients() { foreach (var client in clients) client.Dispose(); }
    private int NalType(byte[] nal) => hevc ? (nal[0] >> 1) & 63 : nal[0] & 31;

    private static async Task<string?> ReadHeaderAsync(Stream stream, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        var one = new byte[1];
        while (buffer.Length < 16384 && await stream.ReadAsync(one, token) > 0)
        {
            buffer.WriteByte(one[0]);
            var bytes = buffer.GetBuffer();
            var length = (int)buffer.Length;
            if (length >= 4 && bytes[length - 4] == 13 && bytes[length - 3] == 10 && bytes[length - 2] == 13 && bytes[length - 1] == 10)
                return Encoding.ASCII.GetString(bytes, 0, length);
        }
        return null;
    }

    private static byte[][] SplitNals(byte[] data)
    {
        var result = new List<byte[]>();
        var start = -1;
        for (var i = 0; i < data.Length - 3; i++)
        {
            var count = data[i] == 0 && data[i + 1] == 0 ? data[i + 2] == 1 ? 3 : data[i + 2] == 0 && data[i + 3] == 1 ? 4 : 0 : 0;
            if (count == 0) continue;
            if (start >= 0) result.Add(data[start..i]);
            start = i + count;
            i += count - 1;
        }
        if (start >= 0) result.Add(data[start..]);
        return result.Where(x => x.Length > 0).ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop(); DisconnectClients();
        await accepting;
    }
}

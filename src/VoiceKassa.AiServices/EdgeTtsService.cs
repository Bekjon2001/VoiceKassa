using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace VoiceKassa.AiServices;

/// <summary>
/// Microsoft Edge read-aloud (Bing) text-to-speech client.
/// Bepul, kalitsiz. Nervli ovozlar, jumladan o'zbekcha:
/// uz-UZ-MadinaNeural (ayol), uz-UZ-SardorNeural (erkak).
/// </summary>
public sealed class EdgeTtsService
{
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string SecMsGecVersion = "1-143.0.3650.75";
    private const long WinEpochSecs = 11644473600L;
    private const string BaseUrl =
        "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";

    private static readonly string[] HeaderPairs =
    {
        "Pragma", "no-cache",
        "Cache-Control", "no-cache",
        "Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold",
        "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0",
        "Accept-Language", "en-US,en;q=0.9",
        "Cookie", "muid=PLACEHOLDER;",
    };

    public async Task<byte[]> SynthesizeAsync(string text, string? voice = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Matn bo'sh", nameof(text));
        voice ??= "uz-UZ-MadinaNeural";
        if (text.Length > 1500) text = text[..1500];

        var url = BuildUrl();
        var ws = new ClientWebSocket();
        try
        {
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            ApplyHeaders(ws);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(45));

            await ws.ConnectAsync(new Uri(url), linked.Token);
            await SendTextAsync(ws, BuildSpeechConfig(), linked.Token);
            await SendTextAsync(ws, BuildSsml(text, voice), linked.Token);

            var frames = new List<byte[]>();
            var buffer = new byte[128 * 1024];
            var msgBytes = new List<byte>();

            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(buffer, linked.Token);
                }
                catch
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                msgBytes.AddRange(buffer.AsSpan(0, result.Count).ToArray());
                if (!result.EndOfMessage) continue;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var textMsg = Encoding.UTF8.GetString(msgBytes.ToArray());
                    if (IsTurnEnd(textMsg)) break;
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var payload = msgBytes.ToArray();
                    if (TryReadAudio(payload, out var audio) && audio.Length > 0)
                        frames.Add(audio);
                }
                msgBytes.Clear();
            }

            if (frames.Count == 0)
                throw new InvalidOperationException("Ovozlash xizmati audio qaytarmadi.");

            using var audioStream = new MemoryStream();
            foreach (var frame in frames) audioStream.Write(frame, 0, frame.Length);
            return audioStream.ToArray();
        }
        finally
        {
            try { ws.Abort(); } catch { /* ignore */ }
            ws.Dispose();
        }
    }

    private static bool IsTurnEnd(string msg)
    {
        var pathIdx = msg.IndexOf("Path:", StringComparison.Ordinal);
        return pathIdx >= 0 &&
               msg.AsSpan(pathIdx, Math.Min(12, msg.Length - pathIdx)).StartsWith("Path:turn.end", StringComparison.Ordinal);
    }

    private static bool TryReadAudio(byte[] payload, out byte[] audio)
    {
        audio = Array.Empty<byte>();
        if (payload.Length < 2) return false;
        int headerLen = (payload[0] << 8) | payload[1];
        if (headerLen <= 0 || 2 + headerLen > payload.Length) return false;
        var header = Encoding.UTF8.GetString(payload, 2, headerLen);
        if (header.IndexOf("Path:audio", StringComparison.Ordinal) < 0) return false;
        int audioLen = payload.Length - (2 + headerLen);
        if (audioLen <= 0) return false;
        audio = payload.AsSpan(2 + headerLen, audioLen).ToArray();
        return true;
    }
private static string BuildUrl()
    {
        var connectionId = Guid.NewGuid().ToString("N");
        return $"{BaseUrl}?TrustedClientToken={TrustedClientToken}" +
               $"&ConnectionId={connectionId}" +
               $"&Sec-MS-GEC={GenerateSecMsGec()}" +
               $"&Sec-MS-GEC-Version={SecMsGecVersion}";
    }

    private static string GenerateSecMsGec()
    {
        long ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ticks += WinEpochSecs;
        ticks -= ticks % 300;
        ticks *= 10_000_000;
        var toHash = ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) + TrustedClientToken;
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(toHash)));
    }

    private static void ApplyHeaders(ClientWebSocket ws)
    {
        var muid = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        for (int i = 0; i < HeaderPairs.Length; i += 2)
        {
            try
            {
                ws.Options.SetRequestHeader(HeaderPairs[i],
                    HeaderPairs[i] == "Cookie" ? $"muid={muid};" : HeaderPairs[i + 1]);
            }
            catch { /* ba'zi boshqarilmaydigan headerlar e'tiborsiz */ }
        }
    }

    private static async Task SendTextAsync(ClientWebSocket ws, string message, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static string DateToString()
    {
        var now = DateTime.UtcNow;
        return now.ToString("ddd MMM dd yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
               + " GMT+0000 (Coordinated Universal Time)";
    }

    private static string BuildSpeechConfig()
    {
        const string json = "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":" +
                            "{\"sentenceBoundaryEnabled\":\"true\",\"wordBoundaryEnabled\":\"false\"}," +
                            "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";
        return $"X-Timestamp:{DateToString()}\r\n" +
               "Content-Type:application/json; charset=utf-8\r\n" +
               "Path:speech.config\r\n\r\n" + json + "\r\n";
    }

    private static string BuildSsml(string text, string voice)
    {
        var escaped = EscapeXml(text);
        return $"X-RequestId:{Guid.NewGuid():N}\r\n" +
               "Content-Type:application/ssml+xml\r\n" +
               $"X-Timestamp:{DateToString()}Z\r\n" +
               "Path:ssml\r\n\r\n" +
               $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
               $"<voice name='{voice}'><prosody pitch='+0Hz' rate='+10%' volume='+0%'>" +
               $"{escaped}</prosody></voice></speak>";
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
         .Replace("\"", "&quot;").Replace("'", "&apos;");
}
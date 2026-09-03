using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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

        // Tilni avtomatik aniqlash: matnda kirill harflari bo'lsa (AI ruscha
        // javob qaytargan bo'lsa) ruscha ovoz, aks holda o'zbekcha ovoz.
        var isRussian = HasCyrillic(text);
        var defaultVoice = isRussian ? "ru-RU-SvetlanaNeural" : "uz-UZ-MadinaNeural";
        if (string.IsNullOrWhiteSpace(voice) ||
            !Regex.IsMatch(voice, @"^[A-Za-z]{2}-[A-Za-z]{2}-[A-Za-z0-9]+$"))
            voice = defaultVoice;

        // Madina/Sardor raqamlarni rus talaffuzida o'qiydi — shuning uchun
        // o'zbekcha matnda raqamlar/sanalarni o'zbekcha so'zlarga aylantiramiz.
        if (!isRussian && voice.StartsWith("uz", StringComparison.OrdinalIgnoreCase))
            text = NormalizeUzbekNumbers(text);

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
        var lang = voice.Length >= 5 ? voice[..5] : "uz-UZ"; // "uz-UZ-MadinaNeural" → "uz-UZ"
        return $"X-RequestId:{Guid.NewGuid():N}\r\n" +
               "Content-Type:application/ssml+xml\r\n" +
               $"X-Timestamp:{DateToString()}Z\r\n" +
               "Path:ssml\r\n\r\n" +
               $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{lang}'>" +
               $"<voice name='{voice}'><prosody pitch='+0Hz' rate='+10%' volume='+0%'>" +
               $"{escaped}</prosody></voice></speak>";
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
         .Replace("\"", "&quot;").Replace("'", "&apos;");

    // ============== O'zbekcha raqam/sana talaffuzini to'g'irlash ==============
    // Madina/Sardor ovozlari raqamlarni rus talaffuzida o'qiydi ("100 000" →
    // "sto tisyach"). Shuning uchun raqamlar, sanalar, foizlar o'zbekcha
    // so'zlarga aylantiriladi: "100 000" → "yuz ming".
    private static readonly string[] UzOnes =
        { "nol", "bir", "ikki", "uch", "to'rt", "besh", "olti", "yetti", "sakkiz", "to'qqiz" };
    private static readonly string[] UzTeens =
    {
        "o'n", "o'n bir", "o'n ikki", "o'n uch", "o'n to'rt",
        "o'n besh", "o'n olti", "o'n yetti", "o'n sakkiz", "o'n to'qqiz",
    };
    private static readonly string[] UzTens =
        { "", "", "yigirma", "o'ttiz", "qirq", "ellik", "oltmish", "yetmish", "sakson", "to'qson" };
    private static readonly string[] UzScale = { "", "ming", "million", "milliard", "trillion" };
    private static readonly string[] UzMonths =
    {
        "yanvar", "fevral", "mart", "aprel", "may", "iyun",
        "iyul", "avgust", "sentabr", "oktabr", "noyabr", "dekabr",
    };

    /// <summary>Matnda kirill (rus) harflari bor-yo'qligini tekshiradi.</summary>
    public static bool HasCyrillic(string text)
    {
        foreach (var ch in text)
            if (ch is >= '\u0400' and <= '\u04FF')
                return true;
        return false;
    }

    /// <summary>Raqamlar/sanalarni o'zbekcha so'zlarga aylantiradi (TTS to'g'ri o'qishi uchun).</summary>
    public static string NormalizeUzbekNumbers(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? "";

        // 1) Sanalar: "2027-01-25" → "25 yanvar 2027-yil", "25.08.2026" → "25 avgust 2026-yil"
        text = Regex.Replace(text, @"\b(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})\b",
            m => DatePhrase(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)));
        text = Regex.Replace(text, @"\b(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})\b",
            m => DatePhrase(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value)));
        text = text.Replace("-yil", " yil");

        // 2) Foiz: "50%" → "50 foiz"
        text = Regex.Replace(text, @"(\d)\s*%", "$1 foiz");

        // 3) O'nlik kasr: "1.5" yoki "1,5" → "1 nuqta 5"
        text = Regex.Replace(text, @"(?<=\d)[.,](?=\d)", " nuqta ");

        // 4) Ming ajratgichlari: "100 000" / "100'000" / "100,000" → "100000"
        text = Regex.Replace(text, @"(?<=\d)[\s'’](?=\d{3}(\D|$))", "");
        text = Regex.Replace(text, @"(?<=\d),(?=\d{3}(\D|$))", "");

        // 5) Vaqt: "14:30" → "14 soat 30"
        text = Regex.Replace(text, @"\b(\d{1,2}):(\d{2})\b", "$1 soat $2");

        // 6) Qolgan barcha sonlar → o'zbekcha so'zlar
        text = Regex.Replace(text, @"\d+", m => NumberToUzbekWords(long.Parse(m.Value)));

        // 7) Ortiqcha bo'shliqlarni yig'ish
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }

    private static string DatePhrase(int year, int month, int day)
    {
        if (month < 1 || month > 12 || day < 1 || day > 31)
            return $"{day}-{month}-{year}";
        return $"{day} {UzMonths[month - 1]} {year}-yil";
    }

    private static string NumberToUzbekWords(long n)
    {
        if (n == 0) return "nol";
        if (n < 0) return "minus " + NumberToUzbekWords(-n);
        if (n >= 1_000_000_000_000_000) return n.ToString(); // juda katta son — TTS o'zicha o'qisin
        var parts = new List<string>();
        var scale = 0;
        while (n > 0)
        {
            var g = (int)(n % 1000);
            n /= 1000;
            if (g > 0)
            {
                var s = UzScale[scale];
                // "bir ming" o'rniga tabiiy "ming", "bir million" o'rniga "million"
                parts.Insert(0, g == 1 && scale > 0 ? s : ThreeDigitWords(g) + (s.Length > 0 ? " " + s : ""));
            }
            scale++;
        }
        return string.Join(" ", parts);
    }

    private static string ThreeDigitWords(int g)
    {
        var parts = new List<string>();
        var hundreds = g / 100;
        var rest = g % 100;
        if (hundreds > 0)
            parts.Add(hundreds == 1 ? "yuz" : UzOnes[hundreds] + " yuz");
        if (rest >= 10 && rest < 20)
        {
            parts.Add(UzTeens[rest - 10]);
        }
        else
        {
            if (rest >= 20) parts.Add(UzTens[rest / 10]);
            if (rest % 10 > 0) parts.Add(UzOnes[rest % 10]);
        }
        return string.Join(" ", parts);
    }
}
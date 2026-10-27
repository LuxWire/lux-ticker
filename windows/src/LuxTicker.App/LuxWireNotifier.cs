using System.Reflection;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace LuxTicker;

sealed class LuxWireNotifier
{
    readonly object _gate = new();
    string? _preparedExe;
    Action<string, string, string>? _balloon;
    string? _lastError;

    public string? LastError
    {
        get { lock (_gate) return _lastError; }
    }

    public void UseBalloon(Action<string, string, string> balloon) => _balloon = balloon;

    public void Prepare()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
            return;
        ToastShortcut.Ensure(exe);
        _preparedExe = exe;
    }

    public bool ShowNotice(string title, string body, string url)
    {
        var canonical = LuxWireLinks.Canonical(url);
        if (canonical == null)
        {
            Remember("The alert URL is not an http(s) link.");
            return false;
        }

        var post = new LuxWirePost(canonical, false, null, null, body, null) { TitleOverride = title };
        return Show(post);
    }

    public bool Show(LuxWirePost post)
    {
        var url = LuxWireLinks.Canonical(post.Url);
        if (url == null)
        {
            Remember("The post URL is not an http(s) link.");
            return false;
        }

        var shown = TryToast(post);
        if (shown)
            return true;

        var balloon = _balloon;
        if (balloon == null)
            return false;

        try
        {
            balloon(post.Title, post.Body, url);
            return true;
        }
        catch (Exception ex)
        {
            Remember(ex.Message);
            Log.Error("lux wire balloon " + ex.Message);
            return false;
        }
    }

    bool TryToast(LuxWirePost post)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
                throw new InvalidOperationException("The running exe path is unknown.");
            if (!string.Equals(_preparedExe, exe, StringComparison.OrdinalIgnoreCase))
            {
                ToastShortcut.Ensure(exe);
                _preparedExe = exe;
            }

            var logo = EnsureLogo();
            var xml = new XmlDocument();
            xml.LoadXml(LuxWireToast.Xml(post, logo, EnsurePaper()));
            ToastNotificationManager.CreateToastNotifier(ToastShortcut.AppId).Show(new ToastNotification(xml));
            return true;
        }
        catch (Exception ex)
        {
            Remember(ex.Message);
            Log.Error("lux wire toast " + ex.Message);
            return false;
        }
    }

    void Remember(string message)
    {
        lock (_gate)
            _lastError = message;
    }

    static string? EnsureLogo()
    {
        try
        {
            var path = AppPaths.LogoPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LuxTicker.Assets.lux-wire-toast.png")
                ?? throw new InvalidOperationException("Embedded Lux Wire logo is missing.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            var current = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
            if (!bytes.AsSpan().SequenceEqual(current))
                File.WriteAllBytes(path, bytes);

            return path;
        }
        catch (Exception ex)
        {
            Log.Error("lux wire logo " + ex.Message);
            return null;
        }
    }

    static string? EnsurePaper()
    {
        try
        {
            var path = Path.Combine(AppPaths.Root(), "toast-paper.png");
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            if (!File.Exists(path) || new FileInfo(path).Length < 32)
            {
                var (r, g, b) = PaperInk.Rgb(PaperInk.Background);
                File.WriteAllBytes(path, SolidPng.Rgb(32, 12, r, g, b));
            }
            return path;
        }
        catch (Exception ex)
        {
            Log.Error("lux wire paper " + ex.Message);
            return null;
        }
    }
}

static class SolidPng
{
    public static byte[] Rgb(int width, int height, byte r, byte g, byte b)
    {
        var raw = new byte[(width * 3 + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int row = y * (width * 3 + 1);
            for (int x = 0; x < width; x++)
            {
                int i = row + 1 + x * 3;
                raw[i] = r;
                raw[i + 1] = g;
                raw[i + 2] = b;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(raw, 0, raw.Length);
        var idat = compressed.ToArray();

        var ihdr = new byte[13];
        WriteInt(ihdr, 0, width);
        WriteInt(ihdr, 4, height);
        ihdr[8] = 8;
        ihdr[9] = 2;

        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Chunk(png, "IHDR"u8, ihdr);
        Chunk(png, "IDAT"u8, idat);
        Chunk(png, "IEND"u8, Array.Empty<byte>());
        return png.ToArray();
    }

    static void Chunk(Stream png, ReadOnlySpan<byte> type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        png.Write(length);
        png.Write(type);
        png.Write(data);
        var crcBuf = new byte[type.Length + data.Length];
        type.CopyTo(crcBuf);
        data.CopyTo(crcBuf.AsSpan(type.Length));
        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc32(crcBuf));
        png.Write(crc);
    }

    static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }

        return ~crc;
    }
}

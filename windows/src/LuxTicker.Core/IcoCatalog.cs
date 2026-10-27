namespace LuxTicker;

/// <summary>One image inside an .ico file. <see cref="ImageBytes"/> is the RT_ICON blob.</summary>
public sealed class IcoImage
{
    public IcoImage(int width, int height, byte[] imageBytes)
    {
        Width = width;
        Height = height;
        ImageBytes = imageBytes;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] ImageBytes { get; }
}

/// <summary>Reads a classic icon directory. Used for the tray and window icons.</summary>
public static class IcoCatalog
{
    public static IReadOnlyList<IcoImage> Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < 6)
            throw new InvalidDataException("Icon file is too small.");

        var reserved = BitConverter.ToUInt16(file[..2]);
        var kind = BitConverter.ToUInt16(file[2..4]);
        var count = BitConverter.ToUInt16(file[4..6]);
        if (reserved != 0 || kind != 1 || count == 0)
            throw new InvalidDataException("Icon file header is not an icon directory.");

        var images = new List<IcoImage>(count);
        var entry = 6;
        for (var i = 0; i < count; i++)
        {
            if (entry + 16 > file.Length)
                throw new InvalidDataException("Icon directory is truncated.");

            var width = file[entry] == 0 ? 256 : file[entry];
            var height = file[entry + 1] == 0 ? 256 : file[entry + 1];
            var bytes = BitConverter.ToInt32(file[(entry + 8)..(entry + 12)]);
            var offset = BitConverter.ToInt32(file[(entry + 12)..(entry + 16)]);
            if (bytes <= 0 || offset < 0 || offset + bytes > file.Length)
                throw new InvalidDataException("Icon image is outside the file.");

            images.Add(new IcoImage(width, height, file.Slice(offset, bytes).ToArray()));
            entry += 16;
        }

        return images;
    }

    public static IcoImage Best(IReadOnlyList<IcoImage> images, int size)
    {
        if (images.Count == 0)
            throw new InvalidOperationException("Icon has no images.");

        IcoImage best = images[0];
        var bestDelta = Math.Abs(best.Width - size);
        foreach (var image in images)
        {
            var delta = Math.Abs(image.Width - size);
            if (delta < bestDelta || (delta == bestDelta && image.Width > best.Width))
            {
                best = image;
                bestDelta = delta;
            }
        }

        return best;
    }
}

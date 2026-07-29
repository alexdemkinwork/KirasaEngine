namespace KirasaEngine.MGL.Smoke;

/// <summary>Minimal dependency-free 24bpp BMP encoder, just for eyeballing smoke-test output.</summary>
public static class BmpWriter
{
    public static void WriteRgba(string path, byte[] rgbaTopLeftOrigin, int width, int height)
    {
        var rowSize = (width * 3 + 3) / 4 * 4;
        var imageSize = rowSize * height;
        var fileSize = 54 + imageSize;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);

        writer.Write(40);
        writer.Write(width);
        writer.Write(height); // positive height => BMP rows are stored bottom-up
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        var row = new byte[rowSize];
        // Source row 0 is the top of the image; BMP wants the bottom row written first.
        for (var srcY = height - 1; srcY >= 0; srcY--)
        {
            var srcOffset = srcY * width * 4;
            for (var x = 0; x < width; x++)
            {
                var si = srcOffset + x * 4;
                row[x * 3 + 0] = rgbaTopLeftOrigin[si + 2];
                row[x * 3 + 1] = rgbaTopLeftOrigin[si + 1];
                row[x * 3 + 2] = rgbaTopLeftOrigin[si + 0];
            }
            writer.Write(row, 0, rowSize);
        }
    }
}

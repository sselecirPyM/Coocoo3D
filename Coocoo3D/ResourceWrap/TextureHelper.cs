using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Coocoo3D.ResourceWrap;

public static class TextureHelper
{
    public static void SaveToFile(Span<byte> data, int width, int height, string fileName)
    {
        var pack = FindPack();

        pack.width = width;
        pack.height = height;
        pack.saveFileName = fileName;
        if (pack.imageData == null || pack.imageData.Length != data.Length)
        {
            pack.imageData = new byte[data.Length];
        }
        data.CopyTo(pack.imageData);

        pack.runningTask = Task.Run(pack.Save);

        totalCount++;
    }

    public static void SaveToFile(Span<byte> data, int width, int height, string fileName, Stream stream)
    {
        var pack = new _TextureStreamSavePack();

        pack.width = width;
        pack.height = height;
        pack.saveFileName = fileName;
        pack.stream = stream;
        if (pack.imageData == null || pack.imageData.Length != data.Length)
        {
            pack.imageData = new byte[data.Length];
        }
        data.CopyTo(pack.imageData);

        pack.StreamSave();

        totalCount++;
    }

    static _TextureSavePack FindPack()
    {
        int saveIndex = -1;
        for (int i = 0; i < _saves.Length; i++)
        {
            if (_saves[i] == null || _saves[i].runningTask.IsCompleted)
            {
                saveIndex = i;
                break;
            }
        }
        if (saveIndex == -1)
            saveIndex = (totalCount + 1) % _saves.Length;

        if (_saves[saveIndex] == null)
            _saves[saveIndex] = new _TextureSavePack();

        var pack = _saves[saveIndex];
        if (pack.runningTask != null && pack.runningTask.Status != TaskStatus.RanToCompletion)
            pack.runningTask.Wait();

        return pack;
    }

    internal static _TextureSavePack[] _saves = new _TextureSavePack[8];

    static int totalCount;
}
class _TextureSavePack
{
    public Task runningTask;
    public byte[] imageData;
    public int width;
    public int height;
    public string saveFileName;

    public void Save()
    {
        Image<Rgba32> image = Image.WrapMemory<Rgba32>(imageData, width, height);
        string extension = Path.GetExtension(saveFileName).ToLower();
        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                image.SaveAsJpeg(saveFileName);
                break;
            case ".bmp":
                image.SaveAsBmp(saveFileName);
                break;
            case ".png":
                image.SaveAsPng(saveFileName);
                break;
            case ".tga":
                TgaEncoder encoder = new TgaEncoder
                {
                    Compression = TgaCompression.None,
                    BitsPerPixel = TgaBitsPerPixel.Pixel24
                };
                image.SaveAsTga(saveFileName, encoder);
                break;
        }
    }

}
class _TextureStreamSavePack
{
    public byte[] imageData;
    public int width;
    public int height;
    public string saveFileName;
    public Stream stream;

    public void StreamSave()
    {
        Image<Rgba32> image = Image.WrapMemory<Rgba32>(imageData, width, height);
        string extension = Path.GetExtension(saveFileName).ToLower();
        try
        {
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    image.SaveAsJpeg(stream);
                    break;
                case ".bmp":
                    image.SaveAsBmp(stream);
                    break;
                case ".png":
                    PngEncoder pngEncoder = new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.NoCompression,
                        ColorType = PngColorType.Rgb
                    };
                    image.SaveAsPng(stream, pngEncoder);
                    break;
                case ".tga":
                    TgaEncoder encoder = new TgaEncoder
                    {
                        Compression = TgaCompression.None,
                        BitsPerPixel = TgaBitsPerPixel.Pixel24
                    };
                    image.SaveAsTga(stream, encoder);
                    break;
            }
        }
        catch
        {

        }
    }
}

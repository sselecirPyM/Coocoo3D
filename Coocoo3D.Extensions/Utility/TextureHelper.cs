using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Coocoo3D.Extensions.Utility;

public static class TextureHelper
{
    public static void SaveToFile(ReadOnlySpan<byte> data, int width, int height, string fileName)
    {
        var data1 = data.ToArray();
        tasks[taskIndex].Wait();
        tasks[taskIndex] = Save(fileName, data1, width, height);
        totalCount++;
        taskIndex = (taskIndex + 1) % tasks.Length;
    }

    public static void SaveToFile(ReadOnlySpan<byte> data, int width, int height, string fileName, Stream stream)
    {
        StreamSave(fileName, data.ToArray(), stream, width, height);
        totalCount++;
    }

    static Task[] tasks = new Task[8];
    static int taskIndex = 0;
    static TextureHelper()
    {
        for (int i = 0; i < 8; i++)
        {
            tasks[i] = Task.CompletedTask;
        }
    }

    static int totalCount;


    static async Task Save(string saveFileName, byte[] imageData, int width, int height)
    {
        await Task.Yield();
        Image<Rgba32> image = Image.WrapMemory<Rgba32>(imageData, width, height);
        string extension = Path.GetExtension(saveFileName).ToLower();
        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                await image.SaveAsJpegAsync(saveFileName);
                break;
            case ".bmp":
                await image.SaveAsBmpAsync(saveFileName);
                break;
            case ".png":
                await image.SaveAsPngAsync(saveFileName);
                break;
            case ".tga":
                TgaEncoder encoder = new TgaEncoder
                {
                    Compression = TgaCompression.None,
                    BitsPerPixel = TgaBitsPerPixel.Pixel24
                };
                await image.SaveAsTgaAsync(saveFileName, encoder);
                break;
        }
    }
    static void StreamSave(string saveFileName, byte[] imageData, Stream stream, int width, int height)
    {
        Image<Rgba32> image = Image.WrapMemory<Rgba32>(imageData, width, height);
        string extension = Path.GetExtension(saveFileName).ToLower();
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
}

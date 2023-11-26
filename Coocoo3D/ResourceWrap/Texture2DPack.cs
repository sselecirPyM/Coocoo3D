using Coocoo3DGraphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using Vortice.DXGI;
using ImageMagick;
using System.Numerics;

namespace Coocoo3D.ResourceWrap;

public class Texture2DPack : IDisposable
{
    public Texture2D texture2D = new Texture2D();
    //public readonly Texture2D loadedTexture = new Texture2D();
    public string fullPath;

    public GraphicsObjectStatus Status;

    public static bool LoadTexture(string fileName, Stream stream, Uploader uploader)
    {
        try
        {
            switch (Path.GetExtension(fileName).ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".jfif":
                case ".png":
                case ".gif":
                case ".tga":
                case ".bmp":
                case ".webp":
                    {
                        byte[] data = GetImageData(stream, out int width, out int height, out int bitPerPixel, out int mipMap);
                        Upload(uploader, data, width, height, bitPerPixel, mipMap);
                    }
                    break;
                default:
                    {
                        byte[] data = GetImageDataMagick(stream, out int width, out int height, out int bitPerPixel, out int mipMap);
                        Upload(uploader, data, width, height, bitPerPixel, mipMap);
                    }
                    break;
            }

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Console.WriteLine(e.StackTrace);
            return false;
        }
    }

    public static byte[] GetImageData(Stream stream, out int width, out int height, out int bitPerPixel, out int mipMap)
    {
        Image<Rgba32> image = Image.Load<Rgba32>(stream);
        var frame0 = image.Frames[0];
        int width1 = frame0.Width;
        int height1 = frame0.Height;
        int sizex = GetAlign(width1);
        int sizey = GetAlign(height1);
        if (width1 != sizex || height1 != sizey)
        {
            image.Mutate(x => x.Resize(sizex, sizey, KnownResamplers.Bicubic));
        }
        width1 = sizex;
        height1 = sizey;
        width = sizex;
        height = sizey;

        bitPerPixel = image.PixelType.BitsPerPixel;
        int bytePerPixel = bitPerPixel / 8;

        int totalCount = sizex * sizey * bytePerPixel;
        int totalSize = GetTotalSize(totalCount, width, height, out mipMap);

        byte[] bytes = new byte[totalSize];

        frame0.CopyPixelDataTo(bytes);

        for (int i = 1; i < mipMap; i++)
        {
            width1 /= 2;
            height1 /= 2;
            int d = bytePerPixel * width1 * height1;
            image.Mutate(x => x.Resize(width1, height1, KnownResamplers.Box));
            var frame1 = image.Frames[0];
            frame1.CopyPixelDataTo(new Span<byte>(bytes, totalCount, d));
            totalCount += d;
        }
        image.Dispose();
        return bytes;
    }
    public static byte[] GetImageDataMagick(Stream stream, out int width, out int height, out int bitPerPixel, out int mipMap)
    {
        var img = new MagickImage(stream);
        int width1 = img.Width;
        int height1 = img.Height;
        int sizex = GetAlign(width1);
        int sizey = GetAlign(height1);

        if (width1 != sizex || height1 != sizey)
        {
            img.Resize(new MagickGeometry(sizex, sizey)
            {
                IgnoreAspectRatio = true,
            });
        }
        width1 = sizex;
        height1 = sizey;
        width = sizex;
        height = sizey;

        bitPerPixel = Math.Max(img.DetermineBitDepth(), 8) * 4;
        int bytePerPixel = bitPerPixel / 8;

        int totalCount = sizex * sizey * bytePerPixel;
        int totalSize = GetTotalSize(totalCount, width, height, out mipMap);

        byte[] bytes = new byte[totalSize];

        img.ToByteArray(MagickFormat.Rgba).CopyTo(new Span<byte>(bytes, 0, bytePerPixel * width1 * height1));

        for (int i = 1; i < mipMap; i++)
        {
            width1 /= 2;
            height1 /= 2;
            int d = bytePerPixel * width1 * height1;
            img.Resize(width1, height1);

            img.ToByteArray(MagickFormat.Rgba).CopyTo(new Span<byte>(bytes, totalCount, d));
            totalCount += d;
        }
        img.Dispose();
        return bytes;
    }

    static int GetAlign(int x)
    {
        int size;
        for (size = 16; size < 8192; size <<= 1)
            if (size >= x * 0.90)
                break;
        return size;
    }

    static int GetTotalSize(int size, int width, int height, out int level)
    {
        int d = size;
        int bytePerPixel = d / (width * height);
        int totalCount = size;
        level = 1;
        if (width > 4096 || height > 4096)
            return totalCount;
        while (width > 2 && height > 2)
        {
            width /= 2;
            height /= 2;
            d = bytePerPixel * width * height;
            totalCount += d;
            level++;
        }
        return totalCount;
    }

    static void Upload(Uploader uploader, byte[] data, int width, int height, int bitPerPixel, int mipMap)
    {
        Format format = bitPerPixel == 16 * 4 ? Format.R16G16B16A16_UNorm : Format.R8G8B8A8_UNorm_SRgb;

        //Test whether the image is a normal map.
        if (bitPerPixel == 8 * 4)
        {
            if (DetectNormalMap(data, width, height, bitPerPixel))
            {
                format = Format.R8G8B8A8_UNorm;
            }
        }
        uploader.Texture2DRawLessCopy(data, format, width, height, mipMap);
    }

    static bool DetectNormalMap(byte[] data, int width, int height, int bitPerPixel)
    {
        int c = Vector<byte>.Count;
        byte[] bytes = new byte[c];
        //<107,107,220,250>-<147,147,255,255>
        for (int i = 0; i < c; i += 4)
        {
            bytes[i + 0] = 107;
            bytes[i + 1] = 107;
            bytes[i + 2] = 220;
            bytes[i + 3] = 250;
        }
        Vector<byte> baseVec = new Vector<byte>(bytes);

        for (int i = 0; i < c; i += 4)
        {
            bytes[i + 0] = 40;
            bytes[i + 1] = 40;
            bytes[i + 2] = 35;
            bytes[i + 3] = 5;
        }
        Vector<byte> sigmaVec = new Vector<byte>(bytes);

        int inPixCount = width * height;
        int endCount = inPixCount * bitPerPixel / 8 / c * c;
        Vector<int> pixCountVec = Vector<int>.Zero;
        for (int i = 0; i < endCount; i += c)
        {
            Vector<byte> a = Vector.LessThanOrEqual(new Vector<byte>(data, i) - baseVec, sigmaVec);
            var d = Vector.Equals(Vector.As<byte, int>(a), new Vector<int>(-1));
            pixCountVec += Vector.BitwiseAnd(d, Vector<int>.One);
        }
        int pixCount = Vector.Sum(pixCountVec);
        if ((float)pixCount / (float)inPixCount > 0.10f)
        {
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        texture2D.Dispose();
        //loadedTexture?.Dispose();
    }
}

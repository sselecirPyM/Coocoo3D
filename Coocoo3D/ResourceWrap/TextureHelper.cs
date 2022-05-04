using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.ResourceWrap
{
    public static class TextureHelper
    {
        public static void SaveToFile(byte[] data, int width, int height, string fileName)
        {
            var pack = FindPack();

            pack.width = width;
            pack.height = height;
            pack.saveFileName = fileName;

            pack.imageData = data;
            pack.runningTask = Task.Run(pack.Save);

            totalCount++;
        }

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
            image.SaveAsPng(saveFileName);
        }

    }
}

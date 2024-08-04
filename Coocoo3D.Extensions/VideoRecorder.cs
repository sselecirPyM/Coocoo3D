using Coocoo3D.Core;
using Coocoo3D.Extensions.UI;
using Coocoo3D.Extensions.Utility;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Coocoo3D.Extensions
{
    public class VideoRecorder : IVisualChannelAttach, IDisposable
    {
        public GameDriverContext gameDriverContext;

        public GraphicsDevice graphicsDevice;
        public GraphicsContext graphicsContext;

        public EngineContext engineContext;

        public RecordSettings recordSettings;
        public string saveDirectory;
        public int maxRecordCount;
        public int RecordCount = 0;

        public Stream pipe;
        public Process ffmpegProcess;

        bool stopRecord = false;

        public void OnRender(VisualChannel visualChannel)
        {
            if (gameDriverContext.PlayTime >= recordSettings.StartTime && RecordCount < maxRecordCount)
            {
                var texture = visualChannel.GetAOV(Caprice.Attributes.AOVType.Color);
                string fileName;
                if (pipe == null)
                    fileName = Path.GetFullPath(string.Format("{0}.png", RecordCount), saveDirectory);
                else
                    fileName = "";

                bool endFrame = RecordCount + 1 >= maxRecordCount || stopRecord;
                Record(texture, pipe, fileName, endFrame);
                RecordCount++;
                if (endFrame)
                {
                    visualChannel.resolusionSizeSource = ResolusionSizeSource.Default;
                    visualChannel.Detach(this);
                }
            }
            else if (stopRecord)
            {
                visualChannel.resolusionSizeSource = ResolusionSizeSource.Default;
                visualChannel.Detach(this);
            }
        }


        void Record(Texture2D texture, Stream stream, string output, bool endFrame)
        {
            engineContext.FrameEnd(() =>
            {
                graphicsContext.ReadBack(texture, (info, data) =>
                {
                    int width = info.width;
                    int height = info.height;
                    if (stream == null)
                    {
                        TextureHelper.SaveToFile(data, width, height, output);
                    }
                    else
                    {
                        stream.Write(data);
                        stream.Flush();
                    }
                    if (endFrame)
                    {
                        pipe?.Dispose();
                        pipe = null;
                    }
                }, null);
            });
        }

        public void StartRecord(VisualChannel visualChannel, IEnumerable<string> ffmpegArgs, bool useFFmpeg)
        {
            visualChannel.outputSize = (recordSettings.Width, recordSettings.Height);
            visualChannel.camera.AspectRatio = (float)recordSettings.Width / (float)recordSettings.Height;
            visualChannel.resolusionSizeSource = ResolusionSizeSource.Custom;

            maxRecordCount = (int)((recordSettings.StopTime - recordSettings.StartTime) * recordSettings.FPS);
            RecordCount = 0;

            if (useFFmpeg)
            {
                StartRecordFFmpeg(ffmpegArgs);
            }
            else
            {
                pipe?.Dispose();
                pipe = null;
            }
        }

        void StartRecordFFmpeg(IEnumerable<string> args)
        {
            var processStartInfo = new ProcessStartInfo();
            processStartInfo.FileName = "ffmpeg";
            processStartInfo.RedirectStandardInput = true;
            foreach (var arg in args)
                processStartInfo.ArgumentList.Add(arg);
            ffmpegProcess = Process.Start(processStartInfo);
            this.pipe = ffmpegProcess.StandardInput.BaseStream;
        }

        public void StopRecord()
        {
            stopRecord = true;
            Console.WriteLine("stoping record");
        }

        public void Dispose()
        {

        }
    }
}

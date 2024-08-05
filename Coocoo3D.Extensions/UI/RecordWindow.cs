using Caprice.Display;
using Coocoo3D.Core;
using Coocoo3D.UI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;

namespace Coocoo3D.Extensions.UI;

public class RecordSettings
{
    [UIDragFloat(1, 0, 10000, name: "FPS")]
    public float FPS;
    [UIDragFloat(1, 0, 10000, name: "开始时间")]
    public float StartTime;
    [UIDragFloat(1, 0, 10000, name: "结束时间")]
    public float StopTime;
    [UIDragInt(32, 32, 16384, name: "宽度")]
    public int Width;
    [UIDragInt(8, 8, 16384, name: "高度")]
    public int Height;

    public string preset;
    public float Crf;
    public float CQ;

    public RecordSettings Clone()
    {
        return (RecordSettings)MemberwiseClone();
    }
}

[Export(typeof(IWindow))]
[Export(typeof(IEditorAccess))]
[ExportMetadata("MenuItem", "录制")]
public class RecordWindow : IWindow, IEditorAccess
{
    public string Title { get => "录制"; }

    public GameDriver gameDriver;
    public UIImGui UIImGui;
    public EditorContext editorContext;
    public EngineContext engineContext;
    public RenderSystem renderSystem;

    bool ffmpegInstalled;
    bool useFFmpeg;
    bool testFFmepg;

    int encoderIndex = 0;
    string[] encoders = new string[]
    {
        "x264",
        "h264_nvenc"
    };
    int presetIndex = 1;
    string[] presets = new string[]
    {
        "fast",
        "medium",
        "slow",
        "slower",
        "veryslow"
    };


    public RecordSettings recordSettings = new RecordSettings()
    {
        FPS = 60,
        Width = 3840,
        Height = 2160,
        StartTime = 0,
        StopTime = 300,
        Crf = 17,
        CQ = 26,
    };

    public void OnGUI()
    {
        if (ffmpegInstalled)
        {
            ImGui.Text("已安装FFmpeg。");
            ImGui.Checkbox("使用FFmpeg输出视频", ref useFFmpeg);
        }
        UIImGui.ShowObject(recordSettings);

        if (encoderIndex == 0)
        {
            ImGui.DragFloat("CRF", ref recordSettings.Crf, 0.5f);
        }
        else
        {
            ImGui.DragFloat("CQ", ref recordSettings.CQ, 0.5f);
        }
        if (encoderIndex == 0 && ImGui.Combo("preset", ref presetIndex, presets, presets.Length))
        {

        }

        if (ImGui.Combo("编码器", ref encoderIndex, encoders, encoders.Length))
        {

        }

        if (ImGui.Button("开始录制"))
        {
            Record();
        }
        ImGui.TreePop();
    }

    void Record()
    {
        var task = new PlatformIOTask()
        {
            title = "录制视频",
            type = PlatformIOTaskType.SaveFolder,
            callback = (string path) =>
            {
                recordSettings.FPS = Math.Max(recordSettings.FPS, 1);
                gameDriver.gameDriverContext.NeedRender = 0;
                gameDriver.FrameIntervalF = 1.0f / recordSettings.FPS;
                var recorder = new VideoRecorder()
                {
                    recordSettings = recordSettings.Clone()
                };
                if (!useFFmpeg)
                {
                    DirectoryInfo folder = new DirectoryInfo(path);
                    if (!folder.Exists)
                        return;
                    recorder.saveDirectory = folder.FullName;
                }
                var visualChannel = editorContext.currentChannel;
                engineContext.FillProperties(recorder);
                List<string> args = new List<string>();
                args.AddRange(new string[]
                {
                    "-y",
                    "-f","rawvideo",
                    "-pixel_format","rgba",
                    "-r", recordSettings.FPS.ToString(),
                    "-colorspace","bt709",
                    "-s", recordSettings.Width + "X" + recordSettings.Height,
                    "-i", @"pipe:0",
                });
                switch (encoderIndex)
                {
                    case 0:
                        Console.WriteLine("encoder x264");
                        args.AddRange(new string[]
                        {
                            "-colorspace","bt709",
                            "-c:v", "libx264",
                            "-vf", "format=yuv420p",
                            "-preset", presets[presetIndex],
                            "-crf", recordSettings.Crf.ToString(),
                             path,
                        });
                        break;
                    case 1:
                        Console.WriteLine("encoder nvenc");
                        args.AddRange(new string[]
                        {
                            "-colorspace","bt709",
                            "-vf", "format=yuv420p",
                            "-c:v", "h264_nvenc",
                            //"-preset", "slow",
                            //"-preset", "lossless",
                            //"-crf", recordSettings.Crf.ToString(),
                            "-cq", recordSettings.CQ.ToString(),
                             path,
                        });
                        break;
                }

                recorder.StartRecord(visualChannel, args, useFFmpeg);
                visualChannel.Attach(recorder);

                gameDriver.ToRecordMode();
                gameDriver.OnEnterPlayMode += _OnStopRecord;
            }
        };
        if (useFFmpeg)
        {
            task.filter = ".mp4\0*.mp4\0\0";
            task.fileExtension = ".mp4\0\0";
            task.type = PlatformIOTaskType.SaveFile;
        }

        UIImGui.UITaskQueue.Enqueue(task);
    }

    void _OnStopRecord()
    {
        foreach (var visualChannel in renderSystem.visualChannels.Values)
        {
            foreach (var attach in visualChannel.attaches)
            {
                if (attach is VideoRecorder recorder)
                {
                    recorder.StopRecord();
                }
            }
        }
        gameDriver.OnEnterPlayMode -= _OnStopRecord;
    }

    public void OnShow()
    {
        if (!testFFmepg)
        {
            testFFmepg = true;
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.UseShellExecute = false;
                processStartInfo.FileName = "ffmpeg";
                processStartInfo.CreateNoWindow = true;
                var process = Process.Start(processStartInfo);
                ffmpegInstalled = true;
                useFFmpeg = true;
                process.CloseMainWindow();
                process.Dispose();
            }
            catch
            {
            }
        }
    }
}
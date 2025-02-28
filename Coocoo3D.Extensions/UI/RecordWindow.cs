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
    public float global_quality;

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

    public string audioFile;

    bool ffmpegInstalled;
    bool useFFmpeg;
    bool testFFmepg;

    int encoderIndex = 0;
    string[] encoders = new string[]
    {
        "x264",
        "h264_nvenc",
        "hevc_nvenc",
        "h264_qsv"
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

    int nvenc_presetIndex = 2;
    string[] nvenc_presets = new string[]
    {
        "fast",
        "medium",
        "slow"
    };

    int audioBitrate = 320000;

    public RecordSettings recordSettings = new RecordSettings()
    {
        FPS = 60,
        Width = 3840,
        Height = 2160,
        StartTime = 0,
        StopTime = 300,
        Crf = 17,
        CQ = 26,
        global_quality = 18,
    };

    int _f1 = 0;
    public void OnGUI()
    {
        if (ffmpegInstalled)
        {
            ImGui.Text("已安装FFmpeg。");
            ImGui.Checkbox("使用FFmpeg编码", ref useFFmpeg);
            ImGui.Text($"选择的音频文件: {audioFile}");
            if (ImGui.Button("选择音频文件"))
            {
                var task = new PlatformIOTask()
                {
                    title = "选择音频文件",
                    type = PlatformIOTaskType.OpenFile,
                    callback = (string path) =>
                    {
                        audioFile = path;
                    }
                };
                UIImGui.UITaskQueue.Enqueue(task);
            }
            ImGui.SameLine();
            if (ImGui.Button("清除音频文件"))
            {
                audioFile = null;
            }
            ImGui.DragInt("音频码率", ref audioBitrate);
        }
        else
        {
            ImGui.Text("未安装FFmpeg，请安装FFmpeg或将FFmpeg放置在软件目录以使用FFmpeg编码。");
            if (ImGui.Button("检测FFmpeg"))
            {
                TestFFmpegInstalled();
                if (!ffmpegInstalled)
                {
                    _f1 = 60;
                }
            }
            if (_f1 > 0)
            {
                _f1--;
                ImGui.Text("未检测到FFmpeg" + new string('*', (_f1 + 19) / 20));
            }
        }
        UIImGui.ShowObject(recordSettings);
        if (ffmpegInstalled)
        {
            if (encoderIndex == 0)
            {
                ImGui.DragFloat("CRF", ref recordSettings.Crf, 0.5f);
                ImGui.Combo("preset", ref presetIndex, presets, presets.Length);
            }
            else if (encoderIndex == 1 || encoderIndex == 2)
            {
                ImGui.DragFloat("CQ", ref recordSettings.CQ, 0.5f);
                ImGui.Combo("preset", ref nvenc_presetIndex, nvenc_presets, nvenc_presets.Length);
            }
            else
            {
                ImGui.DragFloat("global_quality", ref recordSettings.global_quality, 0.5f);
                ImGui.Combo("preset", ref presetIndex, presets, presets.Length);
            }

            ImGui.Combo("编码器", ref encoderIndex, encoders, encoders.Length);
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
                engineContext.FillProperties(recorder);
                var visualChannel = editorContext.currentChannel;
                List<string> args = new List<string>();
                args.AddRange(new string[]
                {
                    "-y",
                    "-f","rawvideo",
                    "-pixel_format","rgba",
                    "-r", recordSettings.FPS.ToString(),
                    "-colorspace","bt709",
                    "-s", recordSettings.Width + "X" + recordSettings.Height,
                    "-i", @"pipe:0"
                });
                if (audioFile != null)
                {
                    args.AddRange(new string[]
                    {
                        "-i",audioFile,
                        "-b:a",audioBitrate.ToString(),
                        "-map","0:v",
                        "-map","1:a",
                    });
                }
                args.AddRange(new string[]
                {
                    "-color_primaries","bt709",
                    "-color_trc"," bt709",
                    "-colorspace","bt709",
                    "-color_range", "tv",
                    "-vf", "format=yuv420p",
                });
                switch (encoderIndex)
                {
                    case 0:
                        Console.WriteLine("encoder: x264");
                        args.AddRange(new string[]
                        {
                            "-c:v", "libx264",
                            "-preset", presets[presetIndex],
                            "-crf", recordSettings.Crf.ToString(),
                        });
                        break;
                    case 1:
                        Console.WriteLine("encoder: h264_nvenc");
                        args.AddRange(new string[]
                        {
                            "-c:v", "h264_nvenc",
                            "-preset", nvenc_presets[nvenc_presetIndex],
                            "-cq", recordSettings.CQ.ToString(),
                        });
                        break;
                    case 2:
                        Console.WriteLine("encoder: hevc_nvenc");
                        args.AddRange(new string[]
                        {
                            "-c:v", "hevc_nvenc",
                            "-preset", nvenc_presets[nvenc_presetIndex],
                            "-cq", recordSettings.CQ.ToString(),
                        });
                        break;
                    case 3:
                        Console.WriteLine("encoder: h264_qsv");
                        args.AddRange(new string[]
                        {
                            "-c:v", "h264_qsv",
                            "-preset", presets[presetIndex],
                            "-global_quality", recordSettings.global_quality.ToString(),
                        });
                        break;
                }
                args.Add(path);
                if (useFFmpeg)
                {
                    recorder.StartRecordFFmpeg(visualChannel, args);
                }
                else
                {
                    recorder.StartRecord(visualChannel);
                }
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
            TestFFmpegInstalled();
        }
    }

    public void TestFFmpegInstalled()
    {
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
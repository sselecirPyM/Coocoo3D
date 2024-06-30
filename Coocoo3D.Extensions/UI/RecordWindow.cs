using Caprice.Display;
using Coocoo3D.Core;
using Coocoo3D.UI;
using ImGuiNET;
using System;
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


    public RecordSettings recordSettings = new RecordSettings()
    {
        FPS = 60,
        Width = 1920,
        Height = 1080,
        StartTime = 0,
        StopTime = 300,
    };

    public void OnGUI()
    {
        if (ffmpegInstalled)
        {
            ImGui.Text("已安装FFmpeg。输出文件名output.mp4");
            ImGui.Checkbox("使用FFmpeg输出mp4", ref useFFmpeg);
        }
        UIImGui.ShowObject(recordSettings);

        if (ImGui.Button("开始录制"))
        {
            Record();
        }
        ImGui.TreePop();
    }

    void Record()
    {
        UIImGui.UITaskQueue.Enqueue(new PlatformIOTask()
        {
            title = "录制视频",
            type = PlatformIOTaskType.SaveFolder,
            callback = (string path) =>
            {
                recordSettings.FPS = Math.Max(recordSettings.FPS, 1);
                gameDriver.gameDriverContext.NeedRender = 0;
                gameDriver.FrameIntervalF = 1.0f / recordSettings.FPS;
                DirectoryInfo folder = new DirectoryInfo(path);
                if (!folder.Exists) return;
                var recorder = new VideoRecorder()
                {
                    recordSettings = recordSettings.Clone()
                };
                var visualChannel = editorContext.currentChannel;
                engineContext.FillProperties(recorder);
                recorder.saveDirectory = folder.FullName;
                recorder.StartRecord(visualChannel, useFFmpeg);
                visualChannel.Attach(recorder);

                gameDriver.ToRecordMode();
                gameDriver.OnEnterPlayMode += _OnStopRecord;
            }
        });
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
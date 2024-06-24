using Coocoo3D.Core;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Coocoo3D.UI;

public class ResourceWindow : IWindow2
{
    public SceneExtensionsSystem sceneExtensions;
    public bool Removing { get; private set; }

    public void OnGUI()
    {
        ImGui.SetNextWindowPos(new Vector2(300, 400), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(500, 300), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("资源"))
        {
            var _openRequest = Resources();
            if (_openRequest != null)
                sceneExtensions.OpenFile(_openRequest.FullName);
        }
        ImGui.End();
    }

    public static Stack<DirectoryInfo> navigationStack = new Stack<DirectoryInfo>();

    public static FileInfo Resources()
    {
        if (ImGui.Button("打开文件夹"))
        {
            UIImGui.UITaskQueue.Enqueue(new PlatformIOTask()
            {
                type = PlatformIOTaskType.SaveFolder,
                callback = (s) =>
                {
                    DirectoryInfo folder = new DirectoryInfo(s);
                    UIImGui.viewRequest = folder;
                }
            });
        }
        ImGui.SameLine();
        if (ImGui.Button("刷新"))
        {
            UIImGui.viewRequest = UIImGui.currentFolder;
        }
        ImGui.SameLine();
        if (ImGui.Button("后退"))
        {
            if (navigationStack.Count > 0)
                UIImGui.viewRequest = navigationStack.Pop();
        }
        string filter = ImGuiExt.ImFilter("查找文件", "查找文件");

        ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.ScrollY;

        var windowSize = ImGui.GetWindowSize();
        var itemSize = windowSize - ImGui.GetCursorPos();
        itemSize.X = 0;
        itemSize.Y -= 8;

        FileInfo open1 = null;
        if (ImGui.BeginTable("resources", 2, tableFlags, Vector2.Max(itemSize, new Vector2(0, 28)), 0))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("文件名");
            ImGui.TableSetupColumn("大小");
            ImGui.TableHeadersRow();

            lock (UIImGui.storageItems)
            {
                bool _requireClear = false;
                foreach (var item in UIImGui.storageItems)
                {
                    if (!Contains(item.Name, filter))
                        continue;
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    DirectoryInfo folder = item as DirectoryInfo;
                    FileInfo file = item as FileInfo;
                    if (ImGui.Selectable(item.Name, false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (folder != null)
                        {
                            navigationStack.Push(UIImGui.currentFolder);
                            UIImGui.viewRequest = folder;
                            _requireClear = true;
                        }
                        else if (file != null)
                        {
                            open1 = file;
                        }
                        ImGui.SaveIniSettingsToDisk("imgui.ini");
                    }
                    ImGui.TableSetColumnIndex(1);
                    if (file != null)
                    {
                        ImGui.TextUnformatted(string.Format("{0} KB", (file.Length + 1023) / 1024));
                    }
                }
                if (_requireClear)
                    UIImGui.storageItems.Clear();
            }
            ImGui.EndTable();
        }

        return open1;
    }

    static bool Contains(string input, string filter)
    {
        return input.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }
}

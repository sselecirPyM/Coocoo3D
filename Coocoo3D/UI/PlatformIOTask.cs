using System;

namespace Coocoo3D.UI
{
    public enum PlatformIOTaskType
    {
        None,
        Exit,
        OpenFile,
        SaveFile,
        SaveFolder
    }
    public class PlatformIOTask
    {
        public PlatformIOTaskType type;
        public string filter;
        public string fileExtension;
        public string title;
        public Action<string> callback;
    }
}

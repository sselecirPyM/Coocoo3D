using Coocoo3D.UI;
using System.Collections.Generic;
using System;
using System.ComponentModel.Composition;

namespace Coocoo3D.Core
{
    [Export]
    public class ExtensionFactory
    {
        [ImportMany]
        public IEnumerable<Lazy<IWindow, IUIMeta>> Windows { get; set; }

        [ImportMany]
        public IEnumerable<IEditorAccess> EditorAccess { get; set; }

        [ImportMany]
        public IEnumerable<IFileLoader> FileLoaders { get; set; }
    }
    public interface IUIMeta
    {
        public string MenuItem { get; }
    }
    public interface IEditorAccess
    {
    }
    public interface IFileLoader
    {
        public bool Load(string path);
    }
}

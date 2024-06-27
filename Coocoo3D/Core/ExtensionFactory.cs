﻿using Coocoo3D.UI;
using System.Collections.Generic;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

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

        [ImportMany("ResourceLoader")]
        public IEnumerable<object> ResourceLoaders { get; set; }
    }
    public interface IUIMeta
    {
        public string MenuItem { get; }
    }
    public interface IEditorAccess
    {
        public virtual void Initialize()
        {

        }
    }
    public interface IFileLoader
    {
        public bool Load(string path);
    }
    public interface IResourceLoader<T>
    {
        public bool TryLoad(string path,out T value);
    }

    public class AsyncProxy
    {
        public Func<Task> calls;
        public long cost;
    }
}

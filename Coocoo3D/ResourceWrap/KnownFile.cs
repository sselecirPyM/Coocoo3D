using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Coocoo3D.ResourceWrap
{
    public class KnownFile
    {
        public DateTimeOffset lastModifiedTime;
        public FileInfo file;
        public string fullPath;
        public bool requireReload;
        public int modifiyIndex;

        public int GetModifyIndex(FileInfo[] fileInfos)
        {
            IsModified(fileInfos);
            return modifiyIndex;
        }

        public bool IsModified(FileInfo[] fileInfos)
        {
            try
            {
                var file = fileInfos.Where(u => u.Name.Equals(Path.GetFileName(fullPath), StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                var attr = file.LastWriteTime;
                bool modified = false;
                if (lastModifiedTime != attr)
                {
                    modified = true;
                    this.file = file;
                    lastModifiedTime = attr;
                }
                if (modified) modifiyIndex++;
                return modified;
            }
            catch (Exception e)
            {
                lastModifiedTime = new DateTimeOffset();
                throw;
            }
        }
    }
}

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
        public int modifiyCount;

        public int GetModifyCount(FileInfo[] fileInfos)
        {
            IsModified(fileInfos);
            return modifiyCount;
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
                if (modified)
                    modifiyCount++;
                return modified;
            }
            catch
            {
                lastModifiedTime = new DateTimeOffset();
                throw;
            }
        }
    }
}

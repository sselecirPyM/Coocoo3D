using Coocoo3D.Components;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.FileFormat
{
    public static class VMDFormatExtension
    {
        public static void Load(this MMDMotion motion, VMDFormat vmd)
        {
            motion.BoneKeyFrameSet.Clear();
            motion.MorphKeyFrameSet.Clear();

            foreach (var pair in vmd.BoneKeyFrameSet)
            {
                var keyFrames = new List<BoneKeyFrame>(pair.Value);
                motion.BoneKeyFrameSet.Add(pair.Key, keyFrames);
                for (int i = 0; i < keyFrames.Count; i++)
                {
                    BoneKeyFrame keyFrame = keyFrames[i];
                    keyFrame.Translation *= 0.1f;
                    keyFrames[i] = keyFrame;
                }
            }
            foreach (var pair in vmd.MorphKeyFrameSet)
            {
                motion.MorphKeyFrameSet.Add(pair.Key, new List<MorphKeyFrame>(pair.Value));
            }
        }
    }
}

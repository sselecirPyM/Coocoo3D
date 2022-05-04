using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public class VectorConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(Vector2) || objectType == typeof(Vector3) || objectType == typeof(Vector4))
            {
                return true;
            }
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType == typeof(Vector2))
            {
                float[] a = serializer.Deserialize<float[]>(reader);
                return new Vector2(a[0], a[1]);
            }
            else if (objectType == typeof(Vector3))
            {
                float[] a = serializer.Deserialize<float[]>(reader);
                return new Vector3(a[0], a[1], a[2]);
            }
            else if (objectType == typeof(Vector4))
            {
                float[] a = serializer.Deserialize<float[]>(reader);
                return new Vector4(a[0], a[1], a[2], a[3]);
            }
            //else if (objectType == typeof(Int2))
            //{
            //    int[] a = serializer.Deserialize<int[]>(reader);
            //    return new Int2(a[0], a[1]);
            //}
            //else if (objectType == typeof(Int3))
            //{
            //    int[] a = serializer.Deserialize<int[]>(reader);
            //    return new Int3(a[0], a[1], a[2]);
            //}
            //else if (objectType == typeof(Int4))
            //{
            //    int[] a = serializer.Deserialize<int[]>(reader);
            //    return new Int4(a[0], a[1], a[2], a[3]);
            //}
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Vector2 v2)
                serializer.Serialize(writer, new float[] { v2.X, v2.Y });
            else if (value is Vector3 v3)
                serializer.Serialize(writer, new float[] { v3.X, v3.Y, v3.Z });
            else if (value is Vector4 v4)
                serializer.Serialize(writer, new float[] { v4.X, v4.Y, v4.Z, v4.W });
            //else if (value is Int2 i2)
            //    serializer.Serialize(writer, new int[] { i2.X, i2.Y });
            //else if (value is Int3 i3)
            //    serializer.Serialize(writer, new int[] { i3.X, i3.Y, i3.Z });
            //else if (value is Int4 i4)
            //    serializer.Serialize(writer, new int[] { i4.X, i4.Y, i4.Z, i4.W });
            else throw new NotImplementedException();
        }
    }
}

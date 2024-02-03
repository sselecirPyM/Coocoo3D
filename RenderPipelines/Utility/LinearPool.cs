using System;
using System.Collections.Generic;

namespace RenderPipelines.Utility;

public class LinearPool<T>
{
    public List<T> list1 = new List<T>();
    public int cur = 0;

    public T Get(Func<T> f1)
    {
        if (cur >= list1.Count)
        {
            list1.Add(f1());
        }
        var result = list1[cur];
        cur++;
        return result;
    }

    public void Reset()
    {
        cur = 0;
    }
}

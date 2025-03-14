﻿using System;
using Vortice.DXGI;

namespace Coocoo3DGraphics;

public class Uploader
{
    public int m_width;
    public int m_height;
    public int m_mipLevels;
    public Format m_format;

    public byte[] m_data;

    public void Texture2DRawLessCopy(byte[] rawData, Format format, int width, int height, int mipLevel)
    {
        m_width = width;
        m_height = height;
        m_format = format;
        m_mipLevels = mipLevel;
        m_data = rawData;
    }
}

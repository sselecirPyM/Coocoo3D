using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Coocoo3DGraphics;

public static class DXHelper
{
    public const int c_frameCount = 3;

    public static void ThrowIfFailed(SharpGen.Runtime.Result hr)
    {
        if (hr != SharpGen.Runtime.Result.Ok)
            throw new NotImplementedException(hr.ToString());
    }

    public static void memcpy<T>(Span<T> t1, Span<T> t2, int size) where T : unmanaged
    {
        int d1 = Marshal.SizeOf(typeof(T));
        t2.Slice(0, size / d1).CopyTo(t1);
    }

    unsafe public static void memcpy<T>(Span<T> t2, void* p1, int size) where T : unmanaged
    {
        int d1 = Marshal.SizeOf(typeof(T));
        new Span<T>(p1, size / d1).CopyTo(t2);
    }

    unsafe public static void memcpy<T>(T[] t2, void* p1, int size) where T : unmanaged
    {
        int d1 = Marshal.SizeOf(typeof(T));
        new Span<T>(p1, size / d1).CopyTo(t2);
    }

    unsafe public static void memcpy<T>(void* p1, Span<T> t2, int size) where T : unmanaged
    {
        int d1 = Marshal.SizeOf(typeof(T));
        t2.CopyTo(new Span<T>(p1, size / d1));
    }

    unsafe public static void memcpy<T>(void* p1, T[] t2, int size) where T : unmanaged
    {
        int d1 = Marshal.SizeOf(typeof(T));
        t2.CopyTo(new Span<T>(p1, size / d1));
    }

    unsafe public static void memcpy(void* p1, void* p2, int size)
    {
        new Span<byte>(p2, size).CopyTo(new Span<byte>(p1, size));
    }

    public static float ConvertDipsToPixels(float dips, float dpi)
    {
        const float dipsPerInch = 96.0f;
        return (float)Math.Floor(dips * dpi / dipsPerInch + 0.5f); // 舍入到最接近的整数。
    }

    unsafe static void MemcpySubresource(
        ulong RowPitch,
        ulong SlicePitch,
        Span<byte> dest,
        IntPtr pSrcData,
        ulong srcRowPitch,
        int RowSizeInBytes,
        int NumRows,
        int NumSlices)
    {
        for (uint z = 0; z < NumSlices; ++z)
        {
            byte* pSrcSlice = (byte*)pSrcData + (long)SlicePitch * z;
            Span<byte> pDestSlice = dest.Slice((int)(SlicePitch * z));
            for (int y = 0; y < NumRows; ++y)
            {
                new Span<byte>(pSrcSlice + ((long)srcRowPitch * y), RowSizeInBytes).CopyTo(pDestSlice.Slice((int)RowPitch * y, RowSizeInBytes));
            }
        }
    }

    public static ulong UpdateSubresources(
        ID3D12GraphicsCommandList pCmdList,
        ID3D12Resource pDestinationResource,
        ID3D12Resource pIntermediate,
        int FirstSubresource,
        int NumSubresources,
        ulong RequiredSize,
        ReadOnlySpan<PlacedSubresourceFootPrint> pLayouts,
        ReadOnlySpan<int> pNumRows,
        ReadOnlySpan<ulong> pRowSizesInBytes,
        ReadOnlySpan<SubresourceData> pSrcData)
    {
        var IntermediateDesc = pIntermediate.Description;
        var DestinationDesc = pDestinationResource.Description;
        if (IntermediateDesc.Dimension != ResourceDimension.Buffer ||
            IntermediateDesc.Width < RequiredSize + pLayouts[0].Offset ||
            (DestinationDesc.Dimension == ResourceDimension.Buffer &&
                (FirstSubresource != 0 || NumSubresources != 1)))
        {
            return 0;
        }

        int sum = 0;
        for (int i = pRowSizesInBytes.Length - 1; i < pRowSizesInBytes.Length; i++)
        {
            sum = Math.Max((int)pLayouts[i].Offset + pLayouts[i].Footprint.RowPitch * pNumRows[i] * pLayouts[i].Footprint.Depth, sum);
        }

        Span<byte> pData = pIntermediate.Map<byte>(0, sum);

        for (int i = 0; i < NumSubresources; ++i)
        {
            MemcpySubresource((ulong)pLayouts[i].Footprint.RowPitch, (uint)pLayouts[i].Footprint.RowPitch * (uint)pNumRows[i],
                pData.Slice((int)pLayouts[i].Offset), pSrcData[i].Data, (ulong)pSrcData[i].RowPitch, (int)pRowSizesInBytes[i], pNumRows[i], pLayouts[i].Footprint.Depth);
        }
        pIntermediate.Unmap(0, null);

        if (DestinationDesc.Dimension == ResourceDimension.Buffer)
        {
            pCmdList.CopyBufferRegion(
                pDestinationResource, 0, pIntermediate, pLayouts[0].Offset, (ulong)pLayouts[0].Footprint.Width);
        }
        else
        {
            for (int i = 0; i < NumSubresources; ++i)
            {
                TextureCopyLocation Dst = new TextureCopyLocation(pDestinationResource, i + FirstSubresource);
                TextureCopyLocation Src = new TextureCopyLocation(pIntermediate, pLayouts[i]);
                pCmdList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
            }
        }
        return RequiredSize;
    }

    public static ulong UpdateSubresources(
        ID3D12GraphicsCommandList pCmdList,
        ID3D12Resource pDestinationResource,
        ID3D12Resource pIntermediate,
        ulong IntermediateOffset,
        int FirstSubresource,
        int NumSubresources,
        Span<SubresourceData> pSrcData)
    {
        Span<PlacedSubresourceFootPrint> pLayouts = stackalloc PlacedSubresourceFootPrint[NumSubresources];
        Span<ulong> pRowSizesInBytes = stackalloc ulong[NumSubresources];
        Span<int> pNumRows = stackalloc int[NumSubresources];

        var Desc = pDestinationResource.Description;
        ID3D12Device pDevice = null;
        pDestinationResource.GetDevice(out pDevice);
        pDevice.GetCopyableFootprints(Desc, (int)FirstSubresource, (int)NumSubresources, IntermediateOffset, pLayouts, pNumRows, pRowSizesInBytes, out ulong RequiredSize);
        pDevice.Release();

        ulong Result = UpdateSubresources(pCmdList, pDestinationResource, pIntermediate, FirstSubresource, NumSubresources, RequiredSize, pLayouts, pNumRows, pRowSizesInBytes, pSrcData);
        return Result;
    }


    public static uint align_to(uint _alignment, uint _val)
    {
        return (((_val + _alignment - 1) / _alignment) * _alignment);
    }

    public static int align_to(int _alignment, int _val)
    {
        return (((_val + _alignment - 1) / _alignment) * _alignment);
    }

    public static Matrix3x4 GetMatrix3X4(Matrix4x4 mat)
    {
        return new Matrix3x4(mat.M11, mat.M12, mat.M13, mat.M14,
            mat.M21, mat.M22, mat.M23, mat.M24,
            mat.M31, mat.M32, mat.M33, mat.M34);
    }

    public static uint BitsPerPixel(Format format)
    {
        switch (format)
        {
            case Format.R32G32B32A32_Typeless:
            case Format.R32G32B32A32_Float:
            case Format.R32G32B32A32_UInt:
            case Format.R32G32B32A32_SInt:
                return 128;

            case Format.R32G32B32_Typeless:
            case Format.R32G32B32_Float:
            case Format.R32G32B32_UInt:
            case Format.R32G32B32_SInt:
                return 96;

            case Format.R16G16B16A16_Typeless:
            case Format.R16G16B16A16_Float:
            case Format.R16G16B16A16_UNorm:
            case Format.R16G16B16A16_UInt:
            case Format.R16G16B16A16_SNorm:
            case Format.R16G16B16A16_SInt:
            case Format.R32G32_Typeless:
            case Format.R32G32_Float:
            case Format.R32G32_UInt:
            case Format.R32G32_SInt:
            case Format.R32G8X24_Typeless:
            case Format.D32_Float_S8X24_UInt:
            case Format.R32_Float_X8X24_Typeless:
            case Format.X32_Typeless_G8X24_UInt:
            case Format.Y416:
            case Format.Y210:
            case Format.Y216:
                return 64;

            case Format.R10G10B10A2_Typeless:
            case Format.R10G10B10A2_UNorm:
            case Format.R10G10B10A2_UInt:
            case Format.R11G11B10_Float:
            case Format.R8G8B8A8_Typeless:
            case Format.R8G8B8A8_UNorm:
            case Format.R8G8B8A8_UNorm_SRgb:
            case Format.R8G8B8A8_UInt:
            case Format.R8G8B8A8_SNorm:
            case Format.R8G8B8A8_SInt:
            case Format.R16G16_Typeless:
            case Format.R16G16_Float:
            case Format.R16G16_UNorm:
            case Format.R16G16_UInt:
            case Format.R16G16_SNorm:
            case Format.R16G16_SInt:
            case Format.R32_Typeless:
            case Format.D32_Float:
            case Format.R32_Float:
            case Format.R32_UInt:
            case Format.R32_SInt:
            case Format.R24G8_Typeless:
            case Format.D24_UNorm_S8_UInt:
            case Format.R24_UNorm_X8_Typeless:
            case Format.X24_Typeless_G8_UInt:
            case Format.R9G9B9E5_SharedExp:
            case Format.R8G8_B8G8_UNorm:
            case Format.G8R8_G8B8_UNorm:
            case Format.B8G8R8A8_UNorm:
            case Format.B8G8R8X8_UNorm:
            case Format.R10G10B10_Xr_Bias_A2_UNorm:
            case Format.B8G8R8A8_Typeless:
            case Format.B8G8R8A8_UNorm_SRgb:
            case Format.B8G8R8X8_Typeless:
            case Format.B8G8R8X8_UNorm_SRgb:
            case Format.AYUV:
            case Format.Y410:
            case Format.YUY2:
                return 32;

            case Format.P010:
            case Format.P016:
                return 24;

            case Format.R8G8_Typeless:
            case Format.R8G8_UNorm:
            case Format.R8G8_UInt:
            case Format.R8G8_SNorm:
            case Format.R8G8_SInt:
            case Format.R16_Typeless:
            case Format.R16_Float:
            case Format.D16_UNorm:
            case Format.R16_UNorm:
            case Format.R16_UInt:
            case Format.R16_SNorm:
            case Format.R16_SInt:
            case Format.B5G6R5_UNorm:
            case Format.B5G5R5A1_UNorm:
            case Format.A8P8:
            case Format.B4G4R4A4_UNorm:
                return 16;

            case Format.NV12:
            //case Format.420_OPAQUE:
            case Format.Opaque420:
            case Format.NV11:
                return 12;

            case Format.R8_Typeless:
            case Format.R8_UNorm:
            case Format.R8_UInt:
            case Format.R8_SNorm:
            case Format.R8_SInt:
            case Format.A8_UNorm:
            case Format.AI44:
            case Format.IA44:
            case Format.P8:
                return 8;

            case Format.R1_UNorm:
                return 1;

            case Format.BC1_Typeless:
            case Format.BC1_UNorm:
            case Format.BC1_UNorm_SRgb:
            case Format.BC4_Typeless:
            case Format.BC4_UNorm:
            case Format.BC4_SNorm:
                return 4;

            case Format.BC2_Typeless:
            case Format.BC2_UNorm:
            case Format.BC2_UNorm_SRgb:
            case Format.BC3_Typeless:
            case Format.BC3_UNorm:
            case Format.BC3_UNorm_SRgb:
            case Format.BC5_Typeless:
            case Format.BC5_UNorm:
            case Format.BC5_SNorm:
            case Format.BC6H_Typeless:
            case Format.BC6H_Uf16:
            case Format.BC6H_Sf16:
            case Format.BC7_Typeless:
            case Format.BC7_UNorm:
            case Format.BC7_UNorm_SRgb:
                return 8;

            default:
                return 0;
        }
    }
}

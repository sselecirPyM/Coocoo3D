using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics.Management;

public class DX12InputElementDescription
{
    public string SemanticName;

    public int SemanticIndex;
    public Format Format;
    public int Slot;
    public int AlignedByteOffset;
    public InputClassification Classification;
    public int InstanceDataStepRate;

    public InputElementDescription GetDescription()
    {
        return new InputElementDescription
        {
            Format = Format,
            SemanticName = SemanticName,
            SemanticIndex = SemanticIndex,
            AlignedByteOffset = AlignedByteOffset,
            Classification = Classification,
            InstanceDataStepRate = InstanceDataStepRate,
            Slot = Slot
        };
    }
}

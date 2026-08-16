using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silly6502;

namespace Test;

internal sealed class TestBus : IAddressBus
{
	public readonly byte[] Ram = new byte[0x1_0000];

	private ref byte GetRamUnsafe(ushort address) => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Ram), address);

	public byte Read(ushort address) => GetRamUnsafe(address);
	public void Write(ushort address, byte value) => GetRamUnsafe(address) = value;
}

namespace Silly6502;

public interface IAddressBus
{
	public byte Read(ushort address);
	public void Write(ushort address, byte value);
}

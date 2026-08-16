namespace Silly6502;

partial class MOS6502
{
	public enum AddressingMode : byte
	{
		Implied,

		Accumulator,
		Immediate,
		Relative,

		Absolute,
		AbsoluteXIndexed,
		AbsoluteYIndexed,

		Zeropage,
		ZeropageXIndexed,
		ZeropageYIndexed,

		Indirect,
		XIndexedIndirect,
		IndirectYIndexed,
	}
}

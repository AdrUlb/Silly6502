using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Silly6502;

partial class MOS6502
{
	public readonly record struct Instruction(Operation Operation, AddressingMode AddressingMode, bool IsIllegal = false)
	{
		public static Instruction None => default;

		public OperationMemoryAccess MemoryAccess { get; } = GetOperationMemoryAccess(Operation);
		public byte ByteCount { get; } = GetAddressingModeByteCount(AddressingMode);

		private static readonly Instruction[] _byOpcode =
		[
			new(Operation.Brk, AddressingMode.Implied), // 0x00
			new(Operation.Ora, AddressingMode.XIndexedIndirect), // 0x01
			new(Operation.Jam, AddressingMode.Implied, true), // 0x02
			new(Operation.Slo, AddressingMode.XIndexedIndirect, true), // 0x03
			new(Operation.Nop, AddressingMode.Zeropage, true), // 0x04
			new(Operation.Ora, AddressingMode.Zeropage), // 0x05
			new(Operation.Asl, AddressingMode.Zeropage), // 0x06
			new(Operation.Slo, AddressingMode.Zeropage, true), // 0x07
			new(Operation.Php, AddressingMode.Implied), // 0x08
			new(Operation.Ora, AddressingMode.Immediate), // 0x09
			new(Operation.Asl, AddressingMode.Accumulator), // 0x0A
			new(Operation.Anc, AddressingMode.Immediate, true), // 0x0B
			new(Operation.Nop, AddressingMode.Absolute, true), // 0x0C
			new(Operation.Ora, AddressingMode.Absolute), // 0x0D
			new(Operation.Asl, AddressingMode.Absolute), // 0x0E
			new(Operation.Slo, AddressingMode.Absolute, true), // 0x0F

			new(Operation.Bpl, AddressingMode.Relative), // 0x10
			new(Operation.Ora, AddressingMode.IndirectYIndexed), // 0x11
			new(Operation.Jam, AddressingMode.Implied, true), // 0x12
			new(Operation.Slo, AddressingMode.IndirectYIndexed, true), // 0x13
			new(Operation.Nop, AddressingMode.ZeropageXIndexed, true), // 0x14
			new(Operation.Ora, AddressingMode.ZeropageXIndexed), // 0x15
			new(Operation.Asl, AddressingMode.ZeropageXIndexed), // 0x16
			new(Operation.Slo, AddressingMode.ZeropageXIndexed, true), // 0x17
			new(Operation.Clc, AddressingMode.Implied), // 0x18
			new(Operation.Ora, AddressingMode.AbsoluteYIndexed), // 0x19
			new(Operation.Nop, AddressingMode.Implied, true), // 0x1A
			new(Operation.Slo, AddressingMode.AbsoluteYIndexed, true), // 0x1B
			new(Operation.Nop, AddressingMode.AbsoluteXIndexed, true), // 0x1C
			new(Operation.Ora, AddressingMode.AbsoluteXIndexed), // 0x1D
			new(Operation.Asl, AddressingMode.AbsoluteXIndexed), // 0x1E
			new(Operation.Slo, AddressingMode.AbsoluteXIndexed, true), // 0x1F

			new(Operation.Jsr, AddressingMode.Absolute), // 0x20
			new(Operation.And, AddressingMode.XIndexedIndirect), // 0x21
			new(Operation.Jam, AddressingMode.Implied, true), // 0x22
			new(Operation.Rla, AddressingMode.XIndexedIndirect, true), // 0x23
			new(Operation.Bit, AddressingMode.Zeropage), // 0x24
			new(Operation.And, AddressingMode.Zeropage), // 0x25
			new(Operation.Rol, AddressingMode.Zeropage), // 0x26
			new(Operation.Rla, AddressingMode.Zeropage, true), // 0x27
			new(Operation.Plp, AddressingMode.Implied), // 0x28
			new(Operation.And, AddressingMode.Immediate), // 0x29
			new(Operation.Rol, AddressingMode.Accumulator), // 0x2A
			new(Operation.Anc, AddressingMode.Immediate, true), // 0x2B
			new(Operation.Bit, AddressingMode.Absolute), // 0x2C
			new(Operation.And, AddressingMode.Absolute), // 0x2D
			new(Operation.Rol, AddressingMode.Absolute), // 0x2E
			new(Operation.Rla, AddressingMode.Absolute, true), // 0x2F

			new(Operation.Bmi, AddressingMode.Relative), // 0x30
			new(Operation.And, AddressingMode.IndirectYIndexed), // 0x31
			new(Operation.Jam, AddressingMode.Implied, true), // 0x32
			new(Operation.Rla, AddressingMode.IndirectYIndexed, true), // 0x33
			new(Operation.Nop, AddressingMode.ZeropageXIndexed, true), // 0x34
			new(Operation.And, AddressingMode.ZeropageXIndexed), // 0x35
			new(Operation.Rol, AddressingMode.ZeropageXIndexed), // 0x36
			new(Operation.Rla, AddressingMode.ZeropageXIndexed, true), // 0x37
			new(Operation.Sec, AddressingMode.Implied), // 0x38
			new(Operation.And, AddressingMode.AbsoluteYIndexed), // 0x39
			new(Operation.Nop, AddressingMode.Implied, true), // 0x3A
			new(Operation.Rla, AddressingMode.AbsoluteYIndexed, true), // 0x3B
			new(Operation.Nop, AddressingMode.AbsoluteXIndexed, true), // 0x3C
			new(Operation.And, AddressingMode.AbsoluteXIndexed), // 0x3D
			new(Operation.Rol, AddressingMode.AbsoluteXIndexed), // 0x3E
			new(Operation.Rla, AddressingMode.AbsoluteXIndexed, true), // 0x3F

			new(Operation.Rti, AddressingMode.Implied), // 0x40
			new(Operation.Eor, AddressingMode.XIndexedIndirect), // 0x41
			new(Operation.Jam, AddressingMode.Implied, true), // 0x42
			new(Operation.Sre, AddressingMode.XIndexedIndirect, true), // 0x43
			new(Operation.Nop, AddressingMode.Zeropage, true), // 0x44
			new(Operation.Eor, AddressingMode.Zeropage), // 0x45
			new(Operation.Lsr, AddressingMode.Zeropage), // 0x46
			new(Operation.Sre, AddressingMode.Zeropage, true), // 0x47
			new(Operation.Pha, AddressingMode.Implied), // 0x48
			new(Operation.Eor, AddressingMode.Immediate), // 0x49
			new(Operation.Lsr, AddressingMode.Accumulator), // 0x4A
			new(Operation.Alr, AddressingMode.Immediate, true), // 0x4B
			new(Operation.Jmp, AddressingMode.Absolute), // 0x4C
			new(Operation.Eor, AddressingMode.Absolute), // 0x4D
			new(Operation.Lsr, AddressingMode.Absolute), // 0x4E
			new(Operation.Sre, AddressingMode.Absolute, true), // 0x4F

			new(Operation.Bvc, AddressingMode.Relative), // 0x50
			new(Operation.Eor, AddressingMode.IndirectYIndexed), // 0x51
			new(Operation.Jam, AddressingMode.Implied, true), // 0x52
			new(Operation.Sre, AddressingMode.IndirectYIndexed, true), // 0x53
			new(Operation.Nop, AddressingMode.ZeropageXIndexed, true), // 0x54
			new(Operation.Eor, AddressingMode.ZeropageXIndexed), // 0x55
			new(Operation.Lsr, AddressingMode.ZeropageXIndexed), // 0x56
			new(Operation.Sre, AddressingMode.ZeropageXIndexed, true), // 0x57
			new(Operation.Cli, AddressingMode.Implied), // 0x58
			new(Operation.Eor, AddressingMode.AbsoluteYIndexed), // 0x59
			new(Operation.Nop, AddressingMode.Implied, true), // 0x5A
			new(Operation.Sre, AddressingMode.AbsoluteYIndexed, true), // 0x5B
			new(Operation.Nop, AddressingMode.AbsoluteXIndexed, true), // 0x5C
			new(Operation.Eor, AddressingMode.AbsoluteXIndexed), // 0x5D
			new(Operation.Lsr, AddressingMode.AbsoluteXIndexed), // 0x5E
			new(Operation.Sre, AddressingMode.AbsoluteXIndexed, true), // 0x5F

			new(Operation.Rts, AddressingMode.Implied), // 0x60
			new(Operation.Adc, AddressingMode.XIndexedIndirect), // 0x61
			new(Operation.Jam, AddressingMode.Implied, true), // 0x62
			new(Operation.Rra, AddressingMode.XIndexedIndirect, true), // 0x63
			new(Operation.Nop, AddressingMode.Zeropage, true), // 0x64
			new(Operation.Adc, AddressingMode.Zeropage), // 0x65
			new(Operation.Ror, AddressingMode.Zeropage), // 0x66
			new(Operation.Rra, AddressingMode.Zeropage, true), // 0x67
			new(Operation.Pla, AddressingMode.Implied), // 0x68
			new(Operation.Adc, AddressingMode.Immediate), // 0x69
			new(Operation.Ror, AddressingMode.Accumulator), // 0x6A
			new(Operation.Arr, AddressingMode.Immediate, true), // 0x6B
			new(Operation.Jmp, AddressingMode.Indirect), // 0x6C
			new(Operation.Adc, AddressingMode.Absolute), // 0x6D
			new(Operation.Ror, AddressingMode.Absolute), // 0x6E
			new(Operation.Rra, AddressingMode.Absolute, true), // 0x6F

			new(Operation.Bvs, AddressingMode.Relative), // 0x70
			new(Operation.Adc, AddressingMode.IndirectYIndexed), // 0x71
			new(Operation.Jam, AddressingMode.Implied, true), // 0x72
			new(Operation.Rra, AddressingMode.IndirectYIndexed, true), // 0x73
			new(Operation.Nop, AddressingMode.ZeropageXIndexed, true), // 0x74
			new(Operation.Adc, AddressingMode.ZeropageXIndexed), // 0x75
			new(Operation.Ror, AddressingMode.ZeropageXIndexed), // 0x76
			new(Operation.Rra, AddressingMode.ZeropageXIndexed, true), // 0x77
			new(Operation.Sei, AddressingMode.Implied), // 0x78
			new(Operation.Adc, AddressingMode.AbsoluteYIndexed), // 0x79
			new(Operation.Nop, AddressingMode.Implied, true), // 0x7A
			new(Operation.Rra, AddressingMode.AbsoluteYIndexed, true), // 0x7B
			new(Operation.Nop, AddressingMode.AbsoluteXIndexed, true), // 0x7C
			new(Operation.Adc, AddressingMode.AbsoluteXIndexed), // 0x7D
			new(Operation.Ror, AddressingMode.AbsoluteXIndexed), // 0x7E
			new(Operation.Rra, AddressingMode.AbsoluteXIndexed, true), // 0x7F

			new(Operation.Nop, AddressingMode.Immediate, true), // 0x80
			new(Operation.Sta, AddressingMode.XIndexedIndirect), // 0x81
			new(Operation.Nop, AddressingMode.Immediate, true), // 0x82
			new(Operation.Sax, AddressingMode.XIndexedIndirect, true), // 0x83
			new(Operation.Sty, AddressingMode.Zeropage), // 0x84
			new(Operation.Sta, AddressingMode.Zeropage), // 0x85
			new(Operation.Stx, AddressingMode.Zeropage), // 0x86
			new(Operation.Sax, AddressingMode.Zeropage, true), // 0x87
			new(Operation.Dey, AddressingMode.Implied), // 0x88
			new(Operation.Nop, AddressingMode.Immediate, true), // 0x89
			new(Operation.Txa, AddressingMode.Implied), // 0x8A
			new(Operation.Ane, AddressingMode.Immediate, true), // 0x8B
			new(Operation.Sty, AddressingMode.Absolute), // 0x8C
			new(Operation.Sta, AddressingMode.Absolute), // 0x8D
			new(Operation.Stx, AddressingMode.Absolute), // 0x8E
			new(Operation.Sax, AddressingMode.Absolute, true), // 0x8F

			new(Operation.Bcc, AddressingMode.Relative), // 0x90
			new(Operation.Sta, AddressingMode.IndirectYIndexed), // 0x91
			new(Operation.Jam, AddressingMode.Implied, true), // 0x92
			new(Operation.Sha, AddressingMode.IndirectYIndexed, true), // 0x93
			new(Operation.Sty, AddressingMode.ZeropageXIndexed), // 0x94
			new(Operation.Sta, AddressingMode.ZeropageXIndexed), // 0x95
			new(Operation.Stx, AddressingMode.ZeropageYIndexed), // 0x96
			new(Operation.Sax, AddressingMode.ZeropageYIndexed, true), // 0x97
			new(Operation.Tya, AddressingMode.Implied), // 0x98
			new(Operation.Sta, AddressingMode.AbsoluteYIndexed), // 0x99
			new(Operation.Txs, AddressingMode.Implied), // 0x9A
			new(Operation.Tas, AddressingMode.AbsoluteYIndexed, true), // 0x9B
			new(Operation.Shy, AddressingMode.AbsoluteXIndexed, true), // 0x9C
			new(Operation.Sta, AddressingMode.AbsoluteXIndexed), // 0x9D
			new(Operation.Shx, AddressingMode.AbsoluteYIndexed, true), // 0x9E
			new(Operation.Sha, AddressingMode.AbsoluteYIndexed, true), // 0x9F

			new(Operation.Ldy, AddressingMode.Immediate), // 0xA0
			new(Operation.Lda, AddressingMode.XIndexedIndirect), // 0xA1
			new(Operation.Ldx, AddressingMode.Immediate), // 0xA2
			new(Operation.Lax, AddressingMode.XIndexedIndirect, true), // 0xA3
			new(Operation.Ldy, AddressingMode.Zeropage), // 0xA4
			new(Operation.Lda, AddressingMode.Zeropage), // 0xA5
			new(Operation.Ldx, AddressingMode.Zeropage), // 0xA6
			new(Operation.Lax, AddressingMode.Zeropage, true), // 0xA7
			new(Operation.Tay, AddressingMode.Implied), // 0xA8
			new(Operation.Lda, AddressingMode.Immediate), // 0xA9
			new(Operation.Tax, AddressingMode.Implied), // 0xAA
			new(Operation.Lxa, AddressingMode.Immediate, true), // 0xAB
			new(Operation.Ldy, AddressingMode.Absolute), // 0xAC
			new(Operation.Lda, AddressingMode.Absolute), // 0xAD
			new(Operation.Ldx, AddressingMode.Absolute), // 0xAE
			new(Operation.Lax, AddressingMode.Absolute, true), // 0xAF

			new(Operation.Bcs, AddressingMode.Relative), // 0xB0
			new(Operation.Lda, AddressingMode.IndirectYIndexed), // 0xB1
			new(Operation.Jam, AddressingMode.Implied, true), // 0xB2
			new(Operation.Lax, AddressingMode.IndirectYIndexed, true), // 0xB3
			new(Operation.Ldy, AddressingMode.ZeropageXIndexed), // 0xB4
			new(Operation.Lda, AddressingMode.ZeropageXIndexed), // 0xB5
			new(Operation.Ldx, AddressingMode.ZeropageYIndexed), // 0xB6
			new(Operation.Lax, AddressingMode.ZeropageYIndexed, true), // 0xB7
			new(Operation.Clv, AddressingMode.Implied), // 0xB8
			new(Operation.Lda, AddressingMode.AbsoluteYIndexed), // 0xB9
			new(Operation.Tsx, AddressingMode.Implied), // 0xBA
			new(Operation.Las, AddressingMode.AbsoluteYIndexed, true), // 0xBB
			new(Operation.Ldy, AddressingMode.AbsoluteXIndexed), // 0xBC
			new(Operation.Lda, AddressingMode.AbsoluteXIndexed), // 0xBD
			new(Operation.Ldx, AddressingMode.AbsoluteYIndexed), // 0xBE
			new(Operation.Lax, AddressingMode.AbsoluteYIndexed, true), // 0xBF

			new(Operation.Cpy, AddressingMode.Immediate), // 0xC0
			new(Operation.Cmp, AddressingMode.XIndexedIndirect), // 0xC1
			new(Operation.Nop, AddressingMode.Immediate, true), // 0xC2
			new(Operation.Dcp, AddressingMode.XIndexedIndirect, true), // 0xC3
			new(Operation.Cpy, AddressingMode.Zeropage), // 0xC4
			new(Operation.Cmp, AddressingMode.Zeropage), // 0xC5
			new(Operation.Dec, AddressingMode.Zeropage), // 0xC6
			new(Operation.Dcp, AddressingMode.Zeropage, true), // 0xC7
			new(Operation.Iny, AddressingMode.Implied), // 0xC8
			new(Operation.Cmp, AddressingMode.Immediate), // 0xC9
			new(Operation.Dex, AddressingMode.Implied), // 0xCA
			new(Operation.Sbx, AddressingMode.Immediate, true), // 0xCB
			new(Operation.Cpy, AddressingMode.Absolute), // 0xCC
			new(Operation.Cmp, AddressingMode.Absolute), // 0xCD
			new(Operation.Dec, AddressingMode.Absolute), // 0xCE
			new(Operation.Dcp, AddressingMode.Absolute, true), // 0xCF

			new(Operation.Bne, AddressingMode.Relative), // 0xD0
			new(Operation.Cmp, AddressingMode.IndirectYIndexed), // 0xD1
			new(Operation.Jam, AddressingMode.Implied, true), // 0xD2
			new(Operation.Dcp, AddressingMode.IndirectYIndexed, true), // 0xD3
			new(Operation.Nop, AddressingMode.ZeropageXIndexed, true), // 0xD4
			new(Operation.Cmp, AddressingMode.ZeropageXIndexed), // 0xD5
			new(Operation.Dec, AddressingMode.ZeropageXIndexed), // 0xD6
			new(Operation.Dcp, AddressingMode.ZeropageXIndexed, true), // 0xD7
			new(Operation.Cld, AddressingMode.Implied), // 0xD8
			new(Operation.Cmp, AddressingMode.AbsoluteYIndexed), // 0xD9
			new(Operation.Nop, AddressingMode.Implied, true), // 0xDA
			new(Operation.Dcp, AddressingMode.AbsoluteYIndexed, true), // 0xDB
			new(Operation.Nop, AddressingMode.AbsoluteXIndexed, true), // 0xDC
			new(Operation.Cmp, AddressingMode.AbsoluteXIndexed), // 0xDD
			new(Operation.Dec, AddressingMode.AbsoluteXIndexed), // 0xDE
			new(Operation.Dcp, AddressingMode.AbsoluteXIndexed, true), // 0xDF

			new(Operation.Cpx, AddressingMode.Immediate), // 0xE0
			new(Operation.Sbc, AddressingMode.XIndexedIndirect), // 0xE1
			new(Operation.Nop, AddressingMode.Immediate, true), // 0xE2
			new(Operation.Isc, AddressingMode.XIndexedIndirect, true), // 0xE3
			new(Operation.Cpx, AddressingMode.Zeropage), // 0xE4
			new(Operation.Sbc, AddressingMode.Zeropage), // 0xE5
			new(Operation.Inc, AddressingMode.Zeropage), // 0xE6
			new(Operation.Isc, AddressingMode.Zeropage, true), // 0xE7
			new(Operation.Inx, AddressingMode.Implied), // 0xE8
			new(Operation.Sbc, AddressingMode.Immediate), // 0xE9
			new(Operation.Nop, AddressingMode.Implied), // 0xEA
			new(Operation.Sbc, AddressingMode.Immediate, true), // 0xEB
			new(Operation.Cpx, AddressingMode.Absolute), // 0xEC
			new(Operation.Sbc, AddressingMode.Absolute), // 0xED
			new(Operation.Inc, AddressingMode.Absolute), // 0xEE
			new(Operation.Isc, AddressingMode.Absolute, true), // 0xEF

			new(Operation.Beq, AddressingMode.Relative), // 0xF0
			new(Operation.Sbc, AddressingMode.IndirectYIndexed), // 0xF1
			new(Operation.Jam, AddressingMode.Implied, true), // 0xF2
			new(Operation.Isc, AddressingMode.IndirectYIndexed, true), // 0xF3
			new(Operation.Nop, AddressingMode.ZeropageXIndexed, true), // 0xF4
			new(Operation.Sbc, AddressingMode.ZeropageXIndexed), // 0xF5
			new(Operation.Inc, AddressingMode.ZeropageXIndexed), // 0xF6
			new(Operation.Isc, AddressingMode.ZeropageXIndexed, true), // 0xF7
			new(Operation.Sed, AddressingMode.Implied), // 0xF8
			new(Operation.Sbc, AddressingMode.AbsoluteYIndexed), // 0xF9
			new(Operation.Nop, AddressingMode.Implied, true), // 0xFA
			new(Operation.Isc, AddressingMode.AbsoluteYIndexed, true), // 0xFB
			new(Operation.Nop, AddressingMode.AbsoluteXIndexed, true), // 0xFC
			new(Operation.Sbc, AddressingMode.AbsoluteXIndexed), // 0xFD
			new(Operation.Inc, AddressingMode.AbsoluteXIndexed), // 0xFE
			new(Operation.Isc, AddressingMode.AbsoluteXIndexed, true), // 0xFF
		];

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Instruction FromOpcode(byte opcode) => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_byOpcode), (nuint)opcode);

		private static OperationMemoryAccess GetOperationMemoryAccess(Operation operation) => operation switch
		{
			// ReSharper disable DuplicatedSwitchExpressionArms
			Operation.Adc => OperationMemoryAccess.Read,
			Operation.And => OperationMemoryAccess.Read,
			Operation.Asl => OperationMemoryAccess.ReadModifyWrite,
			Operation.Bcc => OperationMemoryAccess.Implied,
			Operation.Bcs => OperationMemoryAccess.Implied,
			Operation.Beq => OperationMemoryAccess.Implied,
			Operation.Bit => OperationMemoryAccess.Read,
			Operation.Bmi => OperationMemoryAccess.Implied,
			Operation.Bne => OperationMemoryAccess.Implied,
			Operation.Bpl => OperationMemoryAccess.Implied,
			Operation.Brk => OperationMemoryAccess.Implied,
			Operation.Bvc => OperationMemoryAccess.Implied,
			Operation.Bvs => OperationMemoryAccess.Implied,
			Operation.Clc => OperationMemoryAccess.Implied,
			Operation.Cld => OperationMemoryAccess.Implied,
			Operation.Cli => OperationMemoryAccess.Implied,
			Operation.Clv => OperationMemoryAccess.Implied,
			Operation.Cmp => OperationMemoryAccess.Read,
			Operation.Cpx => OperationMemoryAccess.Read,
			Operation.Cpy => OperationMemoryAccess.Read,
			Operation.Dec => OperationMemoryAccess.ReadModifyWrite,
			Operation.Dex => OperationMemoryAccess.Implied,
			Operation.Dey => OperationMemoryAccess.Implied,
			Operation.Eor => OperationMemoryAccess.Read,
			Operation.Inc => OperationMemoryAccess.ReadModifyWrite,
			Operation.Inx => OperationMemoryAccess.Implied,
			Operation.Iny => OperationMemoryAccess.Implied,
			Operation.Jmp => OperationMemoryAccess.Implied,
			Operation.Jsr => OperationMemoryAccess.Implied,
			Operation.Lda => OperationMemoryAccess.Read,
			Operation.Ldx => OperationMemoryAccess.Read,
			Operation.Ldy => OperationMemoryAccess.Read,
			Operation.Lsr => OperationMemoryAccess.ReadModifyWrite,
			Operation.Nop => OperationMemoryAccess.Read,
			Operation.Ora => OperationMemoryAccess.Read,
			Operation.Pha => OperationMemoryAccess.Implied,
			Operation.Php => OperationMemoryAccess.Implied,
			Operation.Pla => OperationMemoryAccess.Implied,
			Operation.Plp => OperationMemoryAccess.Implied,
			Operation.Rol => OperationMemoryAccess.ReadModifyWrite,
			Operation.Ror => OperationMemoryAccess.ReadModifyWrite,
			Operation.Rti => OperationMemoryAccess.Implied,
			Operation.Rts => OperationMemoryAccess.Implied,
			Operation.Sbc => OperationMemoryAccess.Read,
			Operation.Sec => OperationMemoryAccess.Implied,
			Operation.Sed => OperationMemoryAccess.Implied,
			Operation.Sei => OperationMemoryAccess.Implied,
			Operation.Sta => OperationMemoryAccess.Write,
			Operation.Stx => OperationMemoryAccess.Write,
			Operation.Sty => OperationMemoryAccess.Write,
			Operation.Tax => OperationMemoryAccess.Implied,
			Operation.Tay => OperationMemoryAccess.Implied,
			Operation.Tsx => OperationMemoryAccess.Implied,
			Operation.Txa => OperationMemoryAccess.Implied,
			Operation.Txs => OperationMemoryAccess.Implied,
			Operation.Tya => OperationMemoryAccess.Implied,

			Operation.Alr => OperationMemoryAccess.Read,
			Operation.Anc => OperationMemoryAccess.Read,
			Operation.Ane => OperationMemoryAccess.Read,
			Operation.Arr => OperationMemoryAccess.Read,
			Operation.Dcp => OperationMemoryAccess.ReadModifyWrite,
			Operation.Isc => OperationMemoryAccess.ReadModifyWrite,
			Operation.Jam => OperationMemoryAccess.Implied,
			Operation.Las => OperationMemoryAccess.Read,
			Operation.Lax => OperationMemoryAccess.Read,
			Operation.Lxa => OperationMemoryAccess.Read,
			Operation.Rla => OperationMemoryAccess.ReadModifyWrite,
			Operation.Rra => OperationMemoryAccess.ReadModifyWrite,
			Operation.Sax => OperationMemoryAccess.Write,
			Operation.Sbx => OperationMemoryAccess.Read,
			Operation.Sha => OperationMemoryAccess.Write,
			Operation.Shx => OperationMemoryAccess.Write,
			Operation.Shy => OperationMemoryAccess.Write,
			Operation.Slo => OperationMemoryAccess.ReadModifyWrite,
			Operation.Sre => OperationMemoryAccess.ReadModifyWrite,
			Operation.Tas => OperationMemoryAccess.Write,
			// ReSharper restore DuplicatedSwitchExpressionArms

			_ => throw new InvalidOperationException(),
		};

		private static byte GetAddressingModeByteCount(AddressingMode addressingMode) => addressingMode switch
		{
			AddressingMode.Implied => 1,

			AddressingMode.Accumulator => 1,
			AddressingMode.Immediate => 2,
			AddressingMode.Relative => 2,

			AddressingMode.Absolute => 3,
			AddressingMode.AbsoluteXIndexed => 3,
			AddressingMode.AbsoluteYIndexed => 3,

			AddressingMode.Zeropage => 2,
			AddressingMode.ZeropageXIndexed => 2,
			AddressingMode.ZeropageYIndexed => 2,

			AddressingMode.Indirect => 3,
			AddressingMode.XIndexedIndirect => 2,
			AddressingMode.IndirectYIndexed => 2,

			_ => throw new InvalidOperationException(),
		};
	}
}

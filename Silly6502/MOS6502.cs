using System.Diagnostics;
using System.Runtime.CompilerServices;
using Util.Extensions;

namespace Silly6502;

public sealed partial class MOS6502
{
	public const ushort VectorIrq = 0xFFFE;
	public const ushort VectorNmi = 0xFFFA;
	public const ushort VectorReset = 0xFFFC;

	public event EventHandler? InstructionFinished;

	private byte _regA;

	public byte RegA { get => _regA; set => _regA = value; }
	public byte RegX { get; set; }
	public byte RegY { get; set; }
	public byte RegSPLow { get; set; }
	public ushort RegPC { get; set; }

	public ushort RegSP => (ushort)(RegSPLow | 0x0100);
	public byte RegStatus { get; set; }

	public bool FlagCarry { get => RegStatus.GetBit(0); set => RegStatus = RegStatus.SetBit(0, value); }
	public bool FlagZero { get => RegStatus.GetBit(1); set => RegStatus = RegStatus.SetBit(1, value); }
	public bool FlagInterruptDisable { get => RegStatus.GetBit(2); set => RegStatus = RegStatus.SetBit(2, value); }
	public bool FlagDecimal { get => RegStatus.GetBit(3); set => RegStatus = RegStatus.SetBit(3, value); }
	public bool FlagOverflow { get => RegStatus.GetBit(6); set => RegStatus = RegStatus.SetBit(6, value); }
	public bool FlagNegative { get => RegStatus.GetBit(7); set => RegStatus = RegStatus.SetBit(7, value); }

	public bool BusRead { get; private set; }
	public ushort BusAddress { get; private set; }
	public byte BusData { get; private set; }

	private readonly IAddressBus _bus;

	private bool _latchNmi = false;
	private bool _latchReset = false;

	private bool _brkIrq = false;
	private bool _brkOp = false;
	private ushort _brkVec;

	private Instruction _instruction;
	private byte _step = 1;

	private byte _tempPointer;
	private ushort _tempAddress;
	private byte _tempValue;
	private bool _pageBoundaryCrossed;

	public MOS6502(IAddressBus bus)
	{
		_bus = bus;
		Reset();
	}

	// The IRQ line must be continuously asserted every tick until the interrupt has been acknowledged, it is reset after every tick.
	private bool _requestIrq, _nextRequestIrq;
	private bool _requestNmi, _nextRequestNmi;
	private bool _ready, _nextReady;
	private bool _addressEnable, _nextAddressEnable;

	public bool RequestIrq { get => _requestIrq; set => _nextRequestIrq = value; }
	public bool RequestNmi { get => _requestNmi; set => _nextRequestNmi = value; }
	public bool Ready { get => _ready; set => _nextReady = value; }
	public bool Sync { get; private set; }
	public bool AddressEnable { get => _addressEnable; set => _nextAddressEnable = value; }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Reset()
	{
		_requestIrq = _nextRequestIrq = false;
		_requestNmi = _nextRequestNmi = false;
		_ready = _nextReady = true;
		_addressEnable = _nextAddressEnable = true;

		_latchReset = true;
		_latchNmi = false;
		_brkIrq = true;
		_step = 1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private byte Read(ushort address)
	{
		if (!AddressEnable)
		{
			BusRead = false;
			BusAddress = 0xFFFF;
			return 0xFF;
		}

		BusRead = true;
		BusAddress = address;
		return BusData = _bus.Read(address);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Write(ushort address, byte data)
	{
		if (!AddressEnable)
		{
			BusRead = false;
			BusAddress = 0xFFFF;
			BusData = data;
		}

		BusRead = false;
		BusAddress = address;
		BusData = data;
		_bus.Write(address, data);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Step() => _step++;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void End()
	{
		_step = 1;

		// Determine if an interrupt must be serviced
		_brkIrq = _latchReset || _latchNmi || (RequestIrq && !FlagInterruptDisable);

		Sync = true;
		InstructionFinished?.Invoke(this, EventArgs.Empty);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SetResultFlags(byte result)
	{
		FlagNegative = result.GetBit(7);
		FlagZero = result == 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SetOverflowFlag(byte a, byte b, int result) => FlagOverflow = (result & 0x80) != (a & 0x80) && (result & 0x80) != (b & 0x80);

	public void Tick()
	{
		// A low-to-high transition of the NMI line will set the NMI latch
		if (!_requestNmi && _nextRequestNmi)
			_latchNmi = true;

		_requestIrq = _nextRequestIrq;
		_requestNmi = _nextRequestNmi;
		_ready = _nextReady;
		_addressEnable = _nextAddressEnable;
		_nextRequestIrq = false;
		_nextRequestNmi = false;
		_nextReady = true; // Internal pull-up
		_nextAddressEnable = true;

		if (_step == 1)
		{
			// The next opcode is fetched, PC is incremented immediately
			var opcode = Read(RegPC);
			if (!_ready)
				return;

			_brkOp = opcode == 0x00;

			RegPC++;

			// If we should be servicing an interrupt, the operation is forced to BRK and the PC increment is suppressed
			if (_brkIrq)
			{
				opcode = 0x00;

				if (!_brkOp)
					RegPC--;
			}

			_instruction = Instruction.FromOpcode(opcode);

			_pageBoundaryCrossed = false;

			Sync = false;
		}

		TickInstruction();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstruction()
	{
		switch (_instruction.AddressingMode)
		{
			case AddressingMode.Implied: TickInstructionImplied(); break;
			case AddressingMode.Accumulator: TickInstructionAccumulator(); break;
			case AddressingMode.Immediate: TickInstructionImmediate(); break;
			case AddressingMode.Relative: TickInstructionRelative(); break;
			case AddressingMode.Absolute: TickInstructionAbsolute(); break;
			case AddressingMode.AbsoluteXIndexed: TickInstructionAbsoluteIndexed(RegX); break;
			case AddressingMode.AbsoluteYIndexed: TickInstructionAbsoluteIndexed(RegY); break;
			case AddressingMode.Zeropage: TickInstructionZeropage(); break;
			case AddressingMode.ZeropageXIndexed: TickInstructionZeropageIndexed(RegX); break;
			case AddressingMode.ZeropageYIndexed: TickInstructionZeropageIndexed(RegY); break;
			case AddressingMode.Indirect: TickInstructionIndirect(); break;
			case AddressingMode.XIndexedIndirect: TickInstructionXIndexedIndirect(); break;
			case AddressingMode.IndirectYIndexed: TickInstructionIndirectYIndexed(); break;
			default: throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionImplied()
	{
		switch (_instruction.Operation)
		{
			case Operation.Brk:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);

						if (!_ready)
							break;

						if (_brkOp)
							RegPC++;

						Step();
						break;
					case 3:
						if (!_latchReset)
						{
							Write(RegSP, (byte)RegPC.GetBits(8, 0xFF));
						}
						else
						{
							Read(RegSP);
							if (!_ready)
								break;
						}

						RegSPLow--;
						Step();
						break;
					case 4:
						if (!_latchReset)
						{
							Write(RegSP, (byte)RegPC.GetBits(0, 0xFF));
						}
						else
						{
							Read(RegSP);
							if (!_ready)
								break;
						}

						RegSPLow--;
						Step();
						break;
					case 5:
					{
						if (!_latchReset)
						{
							Write(RegSP, RegStatus.SetBit(4, !_brkIrq));
						}
						else
						{
							Read(RegSP);
							if (!_ready)
								break;
						}

						RegSPLow--;

						if (_latchReset)
						{
							_brkVec = VectorReset;
							_latchReset = false;
						}
						else if (_latchNmi)
						{
							_brkVec = VectorNmi;
							_latchNmi = false;
						}
						else
						{
							_brkVec = VectorIrq;
						}

						FlagInterruptDisable = true;
						Step();
						break;
					}
					case 6:
					{
						var value = Read(_brkVec);
						if (!_ready)
							break;

						_brkVec++;
						RegPC = RegPC.SetBits(0, 0xFF, value);
						Step();
						break;
					}
					case 7:
					{
						var value = Read(_brkVec);
						if (!_ready)
							break;


						_brkVec++;
						RegPC = RegPC.SetBits(8, 0xFF, value);
						_brkIrq = false;
						End();
						break;
					}
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Clc:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						FlagCarry = false;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Cld:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						FlagDecimal = false;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Cli:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						FlagInterruptDisable = false;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Clv:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						FlagOverflow = false;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Dex:
			{
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegX--;
						SetResultFlags(RegX);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			}
			case Operation.Dey:
			{
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegY--;
						SetResultFlags(RegY);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			}
			case Operation.Inx:
			{
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegX++;
						SetResultFlags(RegX);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			}
			case Operation.Iny:
			{
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegY++;
						SetResultFlags(RegY);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			}
			case Operation.Nop:
			{
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			}
			case Operation.Pha:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						Step();
						break;
					case 3:
						Write(RegSP, RegA);
						RegSPLow--;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Php:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						Step();
						break;
					case 3:
						Write(RegSP, RegStatus.SetBit(4, true).SetBit(5, true));
						RegSPLow--;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Pla:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						Step();
						break;
					case 3:
						Read(RegSP);
						if (!_ready)
							break;

						RegSPLow++;
						Step();
						break;
					case 4:
					{
						var value = Read(RegSP);
						if (!_ready)
							break;

						RegA = value;
						SetResultFlags(RegA);
						End();
						break;
					}
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Plp:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						Step();
						break;
					case 3:
						Read(RegSP);
						if (!_ready)
							break;

						RegSPLow++;
						Step();
						break;
					case 4:
					{
						var value = Read(RegSP).SetBit(4, false).SetBit(5, true);
						if (!_ready)
							break;

						RegStatus = value;
						End();
						break;
					}
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Rti:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						Step();
						break;
					case 3:
						Read(RegSP);
						if (!_ready)
							break;

						RegSPLow++;
						Step();
						break;
					case 4:
					{
						var value = Read(RegSP).SetBit(4, false).SetBit(5, true);
						if (!_ready)
							break;

						RegStatus = value;
						RegSPLow++;
						Step();
						break;
					}
					case 5:
					{
						var value = Read(RegSP);
						if (!_ready)
							break;

						RegPC = value;
						RegSPLow++;
						Step();
						break;
					}
					case 6:
					{
						var value = Read(RegSP);
						if (!_ready)
							break;

						RegPC = RegPC.SetBits(8, 0xFF, value);
						End();
						break;
					}
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Rts:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						Step();
						break;
					case 3:
						Read(RegSP);
						if (!_ready)
							break;

						RegSPLow++;
						Step();
						break;
					case 4:
						RegPC = Read(RegSP);
						if (!_ready)
							break;

						RegSPLow++;
						Step();
						break;
					case 5:
					{
						var value = Read(RegSP);
						if (!_ready)
							break;

						RegPC = RegPC.SetBits(8, 0xFF, value);
						Step();
						break;
					}
					case 6:
						Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Sec:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						FlagCarry = true;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Sed:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						FlagDecimal = true;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Sei:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						FlagInterruptDisable = true;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Tax:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegX = RegA;
						SetResultFlags(RegX);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Tay:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegY = RegA;
						SetResultFlags(RegY);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Tsx:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegX = RegSPLow;
						SetResultFlags(RegX);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Txa:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegA = RegX;
						SetResultFlags(RegA);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Txs:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegSPLow = RegX;
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Tya:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						RegA = RegY;
						SetResultFlags(RegA);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;

			// Illegal
			case Operation.Jam:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						Read(RegPC);
						if (!_ready)
							break;

						Step();
						break;
					case 3:
						Read(0xFFFF);
						if (!_ready)
							break;

						Step();
						break;
					case 4:
					case 5:
						Read(0xFFFE);
						if (!_ready)
							break;

						Step();
						break;
					case 6:
						Read(0xFFFF);
						break;
					default:
						throw new UnreachableException();
				}

				break;
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionAccumulator()
	{
		switch (_step)
		{
			case 1:
				Step();
				break;
			case 2:
				Read(RegPC);
				if (!_ready)
					break;

				_tempValue = RegA;
				DoOp();
				RegA = _tempValue;
				End();
				break;
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionImmediate()
	{
		switch (_step)
		{
			case 1:
				Step();
				break;
			case 2:
				_tempValue = Read(RegPC);
				if (!_ready)
					break;

				RegPC++;
				DoOp();
				End();
				break;
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionRelative()
	{
		switch (_step)
		{
			case 1:
				Step();
				break;
			case 2:
			{
				_tempValue = Read(RegPC);
				if (!_ready)
					break;

				RegPC++;

				var condition = _instruction.Operation switch
				{
					Operation.Bcc => !FlagCarry,
					Operation.Bcs => FlagCarry,
					Operation.Beq => FlagZero,
					Operation.Bmi => FlagNegative,
					Operation.Bne => !FlagZero,
					Operation.Bpl => !FlagNegative,
					Operation.Bvc => !FlagOverflow,
					Operation.Bvs => FlagOverflow,
					_ => throw new UnreachableException()
				};

				Step();

				if (!condition)
					End();

				break;
			}
			case 3:
				Read(RegPC);
				if (!_ready)
					break;

				_tempAddress = (ushort)(RegPC + (sbyte)_tempValue);
				RegPC = RegPC.SetBits(0, 0xFF, _tempAddress.GetBits(0, 0xFF));

				Step();
				if (RegPC == _tempAddress)
					End();

				break;
			case 4:
				_pageBoundaryCrossed = true;
				Read(RegPC);
				if (!_ready)
					break;

				RegPC = RegPC.SetBits(8, 0xFF, _tempAddress.GetBits(8, 0xFF));
				End();
				break;
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionAbsolute()
	{
		switch (_instruction.Operation)
		{
			case Operation.Jmp:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC = _tempAddress.SetBits(8, 0xFF, value);
						End();
						break;
					}
					default:
						throw new UnreachableException();
				}

				break;
			case Operation.Jsr:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
						Read(RegSP);
						if (!_ready)
							break;

						Step();
						break;
					case 4:
						Write(RegSP, (byte)RegPC.GetBits(8, 0xFF));
						RegSPLow--;
						Step();
						break;
					case 5:
						Write(RegSP, (byte)RegPC.GetBits(0, 0xFF));
						RegSPLow--;
						Step();
						break;
					case 6:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC = _tempAddress.SetBits(8, 0xFF, value);
						End();
						break;
					}
					default:
						throw new UnreachableException();
				}

				break;
			default:
				switch (_instruction.MemoryAccess)
				{
					case OperationMemoryAccess.Read:
						switch (_step)
						{
							case 1:
								Step();
								break;
							case 2:
							{
								var value = Read(RegPC);
								if (!_ready)
									break;

								RegPC++;
								_tempAddress = value;
								Step();
								break;
							}
							case 3:
							{
								var value = Read(RegPC);
								if (!_ready)
									break;

								RegPC++;
								_tempAddress = _tempAddress.SetBits(8, 0xFF, value);
								Step();
								break;
							}
							case 4:
								_tempValue = Read(_tempAddress);
								if (!_ready)
									break;

								DoOp();
								End();
								break;
							default:
								throw new UnreachableException();
						}

						break;
					case OperationMemoryAccess.ReadModifyWrite:
						switch (_step)
						{
							case 1:
								Step();
								break;
							case 2:
							{
								var value = Read(RegPC);
								if (!_ready)
									break;

								RegPC++;
								_tempAddress = value;
								Step();
								break;
							}
							case 3:
							{
								var value = Read(RegPC);
								if (!_ready)
									break;

								RegPC++;
								_tempAddress = _tempAddress.SetBits(8, 0xFF, value);
								Step();
								break;
							}
							case 4:
							{
								var value = Read(_tempAddress);
								if (!_ready)
									break;

								_tempValue = value;
								Step();
								break;
							}
							case 5:
								Write(_tempAddress, _tempValue);
								DoOp();
								Step();
								break;
							case 6:
								Write(_tempAddress, _tempValue);
								End();
								break;
							default:
								throw new UnreachableException();
						}

						break;
					case OperationMemoryAccess.Write:
						switch (_step)
						{
							case 1:
								Step();
								break;
							case 2:
							{
								var value = Read(RegPC);
								if (!_ready)
									break;

								RegPC++;
								_tempAddress = value;
								Step();
								break;
							}
							case 3:
							{
								var value = Read(RegPC);
								if (!_ready)
									break;

								RegPC++;
								_tempAddress = _tempAddress.SetBits(8, 0xFF, value);
								Step();
								break;
							}
							case 4:
								DoOp();
								Write(_tempAddress, _tempValue);
								End();
								break;
							default:
								throw new UnreachableException();
						}

						break;
					default:
						throw new UnreachableException();
				}

				break;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionAbsoluteIndexed(byte x)
	{
		switch (_instruction.MemoryAccess)
		{
			case OperationMemoryAccess.Read:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempValue = value;
						_tempAddress += x;

						var hi = (byte)_tempAddress.GetBits(8, 0xFF);
						_tempAddress = _tempAddress.SetBits(8, 0xFF, _tempValue);

						if (hi == 0)
							Step();

						Step();
						break;
					}
					case 4:
						_pageBoundaryCrossed = true;
						Read(_tempAddress);
						if (!_ready)
							break;

						_tempAddress += 0x100;
						Step();
						break;
					case 5:
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						DoOp();
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.ReadModifyWrite:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
					{
						_tempValue = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress += x;

						var hi = (byte)_tempAddress.GetBits(8, 0xFF);
						_tempAddress = _tempAddress.SetBits(8, 0xFF, _tempValue);
						_tempValue = hi;
						Step();
						break;
					}
					case 4:
					{
						Read(_tempAddress);
						if (!_ready)
							break;

						if (_tempValue != 0)
							_pageBoundaryCrossed = true;

						_tempAddress += (ushort)(_tempValue << 8);
						Step();
						break;
					}
					case 5:
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						Step();
						break;
					case 6:
						Write(_tempAddress, _tempValue);
						DoOp();
						Step();
						break;
					case 7:
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.Write:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempValue = value;
						_tempAddress += x;

						var hi = (byte)_tempAddress.GetBits(8, 0xFF);
						_tempAddress = _tempAddress.SetBits(8, 0xFF, _tempValue);
						_tempValue = hi;
						Step();
						break;
					}
					case 4:
						Read(_tempAddress);
						if (!_ready)
							break;

						if (_tempValue != 0)
							_pageBoundaryCrossed = true;

						_tempAddress += (ushort)(_tempValue << 8);
						Step();
						break;
					case 5:
						DoOp();
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionZeropage()
	{
		switch (_instruction.MemoryAccess)
		{
			case OperationMemoryAccess.Read:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
					{
						var value = Read(_tempAddress);
						if (!_ready)
							break;

						_tempValue = value;
						DoOp();
						End();
						break;
					}
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.ReadModifyWrite:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						Step();
						break;
					case 4:
						Write(_tempAddress, _tempValue);
						DoOp();
						Step();
						break;
					case 5:
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.Write:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
						DoOp();
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionZeropageIndexed(byte x)
	{
		switch (_instruction.MemoryAccess)
		{
			case OperationMemoryAccess.Read:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					}
					case 3:
					{
						Read(_tempAddress);
						if (!_ready)
							break;

						_tempAddress += x;
						_tempAddress &= 0xFF;
						Step();
						break;
					}
					case 4:
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						DoOp();
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.ReadModifyWrite:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					case 3:
						Read(_tempAddress);
						if (!_ready)
							break;

						_tempAddress += x;
						_tempAddress &= 0xFF;
						Step();
						break;
					case 4:
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						Step();
						break;
					case 5:
						Write(_tempAddress, _tempValue);
						DoOp();
						Step();
						break;
					case 6:
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.Write:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempAddress = value;
						Step();
						break;
					case 3:
						Read(_tempAddress);
						if (!_ready)
							break;

						_tempAddress += x;
						_tempAddress &= 0xFF;
						Step();
						break;
					case 4:
						DoOp();
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionIndirect()
	{
		Debug.Assert(_instruction.Operation == Operation.Jmp);

		switch (_step)
		{
			case 1:
				Step();
				break;
			case 2:
			{
				var value = Read(RegPC);
				if (!_ready)
					break;

				RegPC++;
				_tempAddress = value;
				Step();
				break;
			}
			case 3:
			{
				var value = Read(RegPC);
				if (!_ready)
					break;

				_tempAddress = _tempAddress.SetBits(8, 0xFF, value);
				Step();
				break;
			}
			case 4:
			{
				var value = Read(_tempAddress);
				if (!_ready)
					break;

				_tempAddress++;
				RegPC = value;
				if (_tempAddress.GetBits(0, 0xFF) == 0)
					_tempAddress -= 0x100;

				Step();
				break;
			}
			case 5:
			{
				var value = Read(_tempAddress);
				if (!_ready)
					break;

				RegPC = RegPC.SetBits(8, 0xFF, value);
				End();
				break;
			}
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionXIndexedIndirect()
	{
		switch (_instruction.MemoryAccess)
		{
			case OperationMemoryAccess.Read:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempPointer = value;
						Step();
						break;
					}
					case 3:
						Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer += RegX;
						Step();
						break;
					case 4:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = value;
						Step();
						break;
					}
					case 5:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = _tempAddress.SetBits(8, 0xFF, value);
						Step();
						break;
					}
					case 6:
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						DoOp();
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.ReadModifyWrite:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempPointer = value;
						Step();
						break;
					}
					case 3:
						Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer += RegX;
						Step();
						break;
					case 4:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = _tempAddress.SetBits(0, 0xFF, value);
						Step();
						break;
					}
					case 5:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = _tempAddress.SetBits(8, 0xFF, value);
						Step();
						break;
					}
					case 6:
					{
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						Step();
						break;
					}
					case 7:
						Write(_tempAddress, _tempValue);
						DoOp();
						Step();
						break;
					case 8:
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.Write:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;

						_tempPointer = value;
						Step();
						break;
					}
					case 3:
						Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer += RegX;
						Step();
						break;
					case 4:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = _tempAddress.SetBits(0, 0xFF, value);
						Step();
						break;
					}
					case 5:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = _tempAddress.SetBits(8, 0xFF, value);
						Step();
						break;
					}
					case 6:
						DoOp();
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TickInstructionIndirectYIndexed()
	{
		switch (_instruction.MemoryAccess)
		{
			case OperationMemoryAccess.Read:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempPointer = value;
						Step();
						break;
					}
					case 3:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = value;
						Step();
						break;
					}
					case 4:
					{
						_tempValue = Read(_tempPointer);
						if (!_ready)
							break;

						_tempAddress += RegY;

						var hi = (byte)_tempAddress.GetBits(8, 0xFF);
						_tempAddress = _tempAddress.SetBits(8, 0xFF, _tempValue);

						if (hi == 0)
							Step();

						Step();
						break;
					}
					case 5:
					{
						Read(_tempAddress);
						if (!_ready)
							break;

						_pageBoundaryCrossed = true;
						_tempAddress += 0x100;
						Step();
						break;
					}
					case 6:
					{
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						DoOp();
						End();
						break;
					}
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.ReadModifyWrite:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempPointer = value;
						Step();
						break;
					}
					case 3:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = value;
						Step();
						break;
					}
					case 4:
					{
						_tempValue = Read(_tempPointer);
						if (!_ready)
							break;

						_tempAddress += RegY;
						var hi = (byte)_tempAddress.GetBits(8, 0xFF);
						_tempAddress = _tempAddress.SetBits(8, 0xFF, _tempValue);
						_tempValue = hi;
						Step();
						break;
					}
					case 5:
						Read(_tempAddress);
						if (!_ready)
							break;

						if (_tempValue != 0)
							_pageBoundaryCrossed = true;

						_tempAddress += (ushort)(_tempValue << 8);
						Step();
						break;
					case 6:
						_tempValue = Read(_tempAddress);
						if (!_ready)
							break;

						Step();
						break;
					case 7:
						Write(_tempAddress, _tempValue);
						DoOp();
						Step();
						break;
					case 8:
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			case OperationMemoryAccess.Write:
				switch (_step)
				{
					case 1:
						Step();
						break;
					case 2:
					{
						var value = Read(RegPC);
						if (!_ready)
							break;

						RegPC++;
						_tempPointer = value;
						Step();
						break;
					}
					case 3:
					{
						var value = Read(_tempPointer);
						if (!_ready)
							break;

						_tempPointer++;
						_tempAddress = value;
						Step();
						break;
					}
					case 4:
					{
						_tempValue = Read(_tempPointer);
						if (!_ready)
							break;

						_tempAddress += RegY;
						var hi = (byte)_tempAddress.GetBits(8, 0xFF);
						_tempAddress = _tempAddress.SetBits(8, 0xFF, _tempValue);
						_tempValue = hi;
						Step();
						break;
					}
					case 5:
						if (_tempValue != 0)
							_pageBoundaryCrossed = true;

						Read(_tempAddress);
						if (!_ready)
							break;

						_tempAddress += (ushort)(_tempValue << 8);
						Step();
						break;
					case 6:
						DoOp();
						Write(_tempAddress, _tempValue);
						End();
						break;
					default:
						throw new UnreachableException();
				}

				break;
			default:
				throw new UnreachableException();
		}
	}

	private void DoOp()
	{
		switch (_instruction.Operation)
		{
			case Operation.Adc:
				DoOpAdc();
				break;
			case Operation.And:
				DoOpAnd();
				break;
			case Operation.Asl:
				DoOpAsl();
				break;
			case Operation.Bit:
				FlagZero = (RegA & _tempValue) == 0;
				FlagNegative = _tempValue.GetBit(7);
				FlagOverflow = _tempValue.GetBit(6);
				break;
			case Operation.Cmp:
				DoOpCmp(RegA);
				break;
			case Operation.Cpx:
				DoOpCmp(RegX);
				break;
			case Operation.Cpy:
				DoOpCmp(RegY);
				break;
			case Operation.Dec:
				_tempValue--;
				SetResultFlags(_tempValue);
				break;
			case Operation.Eor:
				DoOpEor();
				break;
			case Operation.Inc:
				_tempValue++;
				SetResultFlags(_tempValue);
				break;
			case Operation.Lda:
				RegA = _tempValue;
				SetResultFlags(_tempValue);
				break;
			case Operation.Ldx:
				RegX = _tempValue;
				SetResultFlags(_tempValue);
				break;
			case Operation.Ldy:
				RegY = _tempValue;
				SetResultFlags(_tempValue);
				break;
			case Operation.Lsr:
				DoOpLsr(ref _tempValue);
				break;
			case Operation.Nop:
				break;
			case Operation.Ora:
				DoOpOra();
				break;
			case Operation.Rol:
				DoOpRol(ref _tempValue);
				break;
			case Operation.Ror:
				DoOpRor(ref _tempValue);
				break;
			case Operation.Sbc:
				DoOpSbc();
				break;
			case Operation.Sta:
				_tempValue = RegA;
				break;
			case Operation.Stx:
				_tempValue = RegX;
				break;
			case Operation.Sty:
				_tempValue = RegY;
				break;

			// Illegal instructions
			case Operation.Alr:
				DoOpAnd();
				DoOpLsr(ref _regA);
				SetResultFlags(RegA);
				break;
			case Operation.Anc:
				DoOpAnd();
				FlagCarry = RegA.GetBit(7);
				break;
			case Operation.Ane: // (A OR CONST) AND X AND oper -> A
			{
				const byte constant = 0xEE;
				RegA = (byte)((RegA | constant) & RegX & _tempValue);
				DoOpAnd();
				break;
			}
			case Operation.Arr:
			{
				DoOpAnd();
				var andResult = RegA;
				DoOpRor(ref _regA);
				FlagOverflow = RegA.GetBit(6) ^ RegA.GetBit(5);

				if (FlagDecimal)
				{
					var low = RegA & 0x0F;
					var high = RegA & 0xF0;

					if ((andResult & 0x0F) + (andResult & 1) > 0x05)
						low += 0x06;

					FlagCarry = false;

					if ((andResult & 0xF0) + (andResult & 0x10) > 0x50)
					{
						high += 0x60;
						FlagCarry = true;
					}

					RegA = (byte)((high & 0xF0) | (low & 0x0F));
				}
				else
				{
					FlagCarry = RegA.GetBit(6);
				}

				break;
			}
			case Operation.Dcp:
				_tempValue--;
				DoOpCmp(RegA);
				break;
			case Operation.Isc:
				_tempValue++;
				DoOpSbc();
				break;
			case Operation.Las:
				RegA = RegX = RegSPLow = (byte)(_tempValue & RegSPLow);
				SetResultFlags(RegA);
				break;
			case Operation.Lax:
				RegA = RegX = _tempValue;
				SetResultFlags(RegA);
				break;
			case Operation.Lxa: // (A OR CONST) AND oper -> A -> X
			{
				const byte constant = 0xEE;
				RegA = RegX = RegA = (byte)((RegA | constant) & _tempValue);
				SetResultFlags(RegA);
				break;
			}
			case Operation.Rla:
				DoOpRol(ref _tempValue);
				DoOpAnd();
				break;
			case Operation.Rra:
				DoOpRor(ref _tempValue);
				DoOpAdc();
				break;
			case Operation.Sax:
				_tempValue = (byte)(RegA & RegX);
				break;
			case Operation.Sbx:
				RegX &= RegA;
				DoOpCmp(RegX);
				RegX -= _tempValue;
				break;
			case Operation.Sha:
			{
				var and = _tempAddress.GetBits(8, 0xFF);

				if (_pageBoundaryCrossed)
				{
					_tempAddress = _tempAddress.SetBits(8, 0xFF, (byte)(_tempAddress.GetBits(8, 0xFF) & RegA & RegX));
				}
				else
					and++;

				_tempValue = (byte)(RegA & RegX & and);
				break;
			}
			case Operation.Shx:
			{
				var and = _tempAddress.GetBits(8, 0xFF);

				if (_pageBoundaryCrossed)
				{
					_tempAddress = _tempAddress.SetBits(8, 0xFF, (byte)(_tempAddress.GetBits(8, 0xFF) & RegX));
				}
				else
					and++;

				_tempValue = (byte)(RegX & and);
				break;
			}
			case Operation.Shy:
			{
				var and = _tempAddress.GetBits(8, 0xFF);

				if (_pageBoundaryCrossed)
				{
					_tempAddress = _tempAddress.SetBits(8, 0xFF, (byte)(_tempAddress.GetBits(8, 0xFF) & RegY));
				}
				else
					and++;

				_tempValue = (byte)(RegY & and);
				break;
			}
			case Operation.Slo:
				DoOpAsl();
				DoOpOra();
				break;
			case Operation.Sre:
				DoOpLsr(ref _tempValue);
				DoOpEor();
				break;
			case Operation.Tas:
			{
				var and = _tempAddress.GetBits(8, 0xFF);

				if (_pageBoundaryCrossed)
				{
					_tempAddress = _tempAddress.SetBits(8, 0xFF, (byte)(_tempAddress.GetBits(8, 0xFF) & RegA & RegX));
				}
				else
					and++;

				RegSPLow = (byte)(RegA & RegX);
				_tempValue = (byte)(RegA & RegX & and);
				break;
			}
			default:
				throw new UnreachableException();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpAdc()
	{
		var carry = (FlagCarry ? 1 : 0);
		var result = RegA + _tempValue + carry;

		if (FlagDecimal)
		{
			var low = (RegA & 0x0F) + (_tempValue & 0x0F) + carry;
			var high = (RegA & 0xF0) + (_tempValue & 0xF0) + (low > 0x09 ? 0x10 : 0);

			FlagZero = (byte)result == 0;
			FlagNegative = (high & 0x80) != 0;
			SetOverflowFlag(RegA, _tempValue, high);

			if (low > 0x09)
				low = (low + 0x06) & 0x0F;

			FlagCarry = high > 0x90;

			if (high > 0x90)
				high += 0x60;

			RegA = (byte)((high & 0xF0) | (low & 0x0F));
		}
		else
		{
			SetOverflowFlag(RegA, _tempValue, result);
			FlagCarry = result > byte.MaxValue;
			RegA = (byte)result;
			SetResultFlags(RegA);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpAnd()
	{
		RegA &= _tempValue;
		SetResultFlags(RegA);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpAsl()
	{
		FlagCarry = _tempValue.GetBit(7);
		_tempValue <<= 1;
		SetResultFlags(_tempValue);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpCmp(byte lhs)
	{
		var result = lhs - _tempValue;
		FlagCarry = result >= 0;
		SetResultFlags((byte)result);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpEor()
	{
		RegA ^= _tempValue;
		SetResultFlags(RegA);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpLsr(ref byte value)
	{
		FlagCarry = value.GetBit(0);
		value >>>= 1;
		SetResultFlags(value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpOra()
	{
		RegA |= _tempValue;
		SetResultFlags(RegA);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpRol(ref byte value)
	{
		var bit0 = FlagCarry;
		FlagCarry = value.GetBit(7);
		value <<= 1;
		value = value.SetBit(0, bit0);
		SetResultFlags(value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpRor(ref byte value)
	{
		var bit7 = FlagCarry;
		FlagCarry = value.GetBit(0);
		value >>= 1;
		value = value.SetBit(7, bit7);
		SetResultFlags(value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DoOpSbc()
	{
		var borrow = (FlagCarry ? 0 : 1);
		var result = RegA - _tempValue - borrow;

		SetOverflowFlag(RegA, (byte)~_tempValue, result);
		FlagCarry = result >= 0;

		if (FlagDecimal)
		{
			var low = (RegA & 0x0F) - (_tempValue & 0x0F) - borrow;
			var high = (RegA & 0xF0) - (_tempValue & 0xF0) - (low < 0 ? 0x10 : 0);

			FlagZero = (byte)result == 0;
			FlagNegative = (high & 0x80) != 0;

			if (low < 0)
				low = (low - 0x06) & 0x0F;

			if (high < 0)
				high -= 0x60;

			RegA = (byte)((high & 0xF0) | (low & 0x0F));
		}
		else
		{
			RegA = (byte)result;
			SetResultFlags(RegA);
		}
	}
}

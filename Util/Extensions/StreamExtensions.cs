using System.Buffers.Binary;

namespace Util.Extensions;


public static class StreamExtensions
{
	extension(Stream self)
	{
		public byte ReadUInt8()
		{
			Span<byte> buf = stackalloc byte[1];
			self.ReadExactly(buf);
			return buf[0];
		}

		public sbyte ReadInt8() => (sbyte)self.ReadUInt8();

		public ushort ReadUInt16LittleEndian()
		{
			Span<byte> buf = stackalloc byte[2];
			self.ReadExactly(buf);
			return BinaryPrimitives.ReadUInt16LittleEndian(buf);
		}

		public uint ReadUInt32LittleEndian()
		{
			Span<byte> buf = stackalloc byte[4];
			self.ReadExactly(buf);
			return BinaryPrimitives.ReadUInt32LittleEndian(buf);
		}

		public ulong ReadUInt64LittleEndian()
		{
			Span<byte> buf = stackalloc byte[8];
			self.ReadExactly(buf);
			return BinaryPrimitives.ReadUInt64LittleEndian(buf);
		}
		
		public short ReadInt16LittleEndian() => (short)self.ReadUInt16LittleEndian();
		public int ReadInt32LittleEndian() => (int)self.ReadUInt32LittleEndian();
		public long ReadInt64LittleEndian() => (long)self.ReadUInt64LittleEndian();
		
		public ushort ReadUInt16BigEndian()
		{
			Span<byte> buf = stackalloc byte[2];
			self.ReadExactly(buf);
			return BinaryPrimitives.ReadUInt16BigEndian(buf);
		}

		public uint ReadUInt32BigEndian()
		{
			Span<byte> buf = stackalloc byte[4];
			self.ReadExactly(buf);
			return BinaryPrimitives.ReadUInt32BigEndian(buf);
		}

		public ulong ReadUInt64BigEndian()
		{
			Span<byte> buf = stackalloc byte[8];
			self.ReadExactly(buf);
			return BinaryPrimitives.ReadUInt64BigEndian(buf);
		}
		
		public short ReadInt16BigEndian() => (short)self.ReadUInt16BigEndian();
		public int ReadInt32BigEndian() => (int)self.ReadUInt32BigEndian();
		public long ReadInt64BigEndian() => (long)self.ReadUInt64BigEndian();
	}
}

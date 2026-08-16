using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Util.Extensions;

public static class ValueTypeExtensions
{
	extension<T>(ref T self) where T : unmanaged
	{
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> AsBytes() => MemoryMarshal.AsBytes(new Span<T>(ref self));
	}
}

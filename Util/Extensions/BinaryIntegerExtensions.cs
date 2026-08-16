using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Util.Extensions;

public static class BinaryIntegerExtensions
{
	extension<T>(T self) where T : struct, IBinaryInteger<T>
	{
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool GetBit(int bit) => ((self >>> bit) & T.One) != T.Zero;

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T SetBit(int bit, bool value) => (self & ~(T.One << bit)) | ((value ? T.One : T.Zero) << bit);

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T GetBits(int shift, T mask) => (self >>> shift) & mask;

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T GetBits(int shift, uint mask) => (self >>> shift) & T.CreateTruncating(mask);

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T SetBits(int shift, T mask, T value) => (self & ~(mask << shift)) | ((value & mask) << shift);

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T SetBits(int shift, uint mask, uint value) => self.SetBits(shift, T.CreateTruncating(mask), T.CreateTruncating(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T SetBits(int shift, T mask, uint value) => self.SetBits(shift, mask, T.CreateTruncating(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T SetBits(int shift, uint mask, T value) => self.SetBits(shift, T.CreateTruncating(mask), value);
	}
}

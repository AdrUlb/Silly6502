namespace Util;

public sealed class PositionTrackingStream : Stream
{
	public override long Length => throw new NotSupportedException();
	public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

	public override bool CanRead => _baseStream.CanRead;
	public override bool CanWrite => false;
	public override bool CanSeek => false;

	public bool EndReached => _peek == -1 && (_peek = _baseStream.ReadByte()) == -1;

	private long _position;
	private readonly Stream _baseStream;
	private readonly bool _leaveOpen;

	private int _peek = -1;

	public PositionTrackingStream(Stream baseStream, bool leaveOpen = false)
	{
		_baseStream = baseStream;
		_leaveOpen = leaveOpen;

		try
		{
			_position = _baseStream.Position;
		}
		catch
		{
			_position = 0;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (!_leaveOpen)
			_baseStream.Dispose();

		base.Dispose(disposing);
	}

	public override void SetLength(long value) => throw new NotSupportedException();

	public override long Seek(long offset, SeekOrigin origin)
	{
		var newPosition = origin switch
		{
			SeekOrigin.Begin => offset >= _position ? offset : throw new NotSupportedException(),
			SeekOrigin.Current => _position + offset,
			SeekOrigin.End => throw new NotSupportedException(),
			_ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
		};

		while (_position < newPosition)
		{
			if (_peek != -1)
			{
				_peek = -1;
				_position++;
				continue;
			}

			if (_baseStream.ReadByte() == -1)
				break;

			_position++;
		}

		return _position;
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		if (count == 0)
			return 0;

		var readCount = 0;

		if (_peek != -1)
		{
			buffer[offset++] = (byte)_peek;
			_peek = -1;

			readCount++;
			count--;
		}

		if (count > 0)
			readCount += _baseStream.Read(buffer, offset, count);

		_position += readCount;
		return readCount;
	}

	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	public override void Flush() {}
}

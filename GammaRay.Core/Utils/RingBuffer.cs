using System.Collections;

namespace GammaRay.Core.Utils;

public class RingBuffer<T>(int _size) : IEnumerable<T> where T : struct
{
	private readonly T[] _buffer = new T[_size];
	private int _currentPosition = 0;
	private bool _full = false;


	public int Size => _size;

	public bool IsFull => _full;

	public int Used => _full ? _size : _currentPosition;

	public int CurrentPosition => _currentPosition;

	public T[] InternalBuffer => _buffer;

	public Span<T> FirstUsedPart => InternalBuffer.AsSpan(.._currentPosition);

	public Span<T> SecondUsedPart => IsFull ? InternalBuffer.AsSpan(_currentPosition..) : Span<T>.Empty;

	//
	// Index order:
	// [oldest] used-1, used-2, ... , 2, 1, [newest] 0,
	// (Negative indexes valid only if buffer is full)
	// [_currentPosition aka NextDisplacementCandidate aka oldest if full] -1, -2, -3 ... , [lastIndexable] -size+1, [newest] -size = 0
	//
	// In internal buffer:
	//
	// [oldest] _cp-used, ..[possible boundary].. , [newest, zeroIndex] _cp-1 (0), [displacementCandidate] _cp+0 (-1), ..[possible boundary].. , [newest] _cp-1+size = _cp-1
	//
	public T this[int index]
	{
		get
		{
			if (index >= _size || index < -_size + 1)
				throw new IndexOutOfRangeException("Too big");
			if (IsFull == false && (index < 0 || index >= Used))
				throw new IndexOutOfRangeException("Element is not reachable");

			var zeroIndex = _currentPosition - 1;
			var indexInInternalArray = zeroIndex - index;

			indexInInternalArray %= _size;
			if (indexInInternalArray < 0)
				indexInInternalArray = -indexInInternalArray;

			return _buffer[indexInInternalArray];
		}
	}


	public T? Push(T value)
	{
		T? displacedElement = _full ? _buffer[_currentPosition] : null;
		_buffer[_currentPosition] = value;
		_currentPosition++;

		if (_currentPosition == Size)
		{
			_full = true;
			_currentPosition = 0;
		}

		return displacedElement;
	}

	public T? GetNextDisplacementCandidate() => IsFull ? _buffer[_currentPosition] : null;

	public RingBuffer<T> Resize(int size)
	{
		if (size <= _size)
			throw new ArgumentOutOfRangeException(nameof(size), size, "Less or equal then current size of RingBuffer");

		var newBuffer = new RingBuffer<T>(size);

		Array.Copy(_buffer, _currentPosition, newBuffer._buffer, 0, _size - _currentPosition);
		Array.Copy(_buffer, 0, newBuffer._buffer, _size - _currentPosition, _currentPosition);

		return newBuffer;
	}

	public Enumerator GetEnumerator() => new(this);

	IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


	public struct Enumerator(RingBuffer<T> ringBuffer) : IEnumerator<T>
	{
		private readonly RingBuffer<T> _ringBuffer = ringBuffer;
		private int _index = -1;


		public bool MoveNext()
		{
			if (_index < _ringBuffer.Used - 1)
			{
				_index++;
				return true;
			}
			return false;
		}

		public readonly void Reset() => throw new NotSupportedException();

		public readonly void Dispose() { }


		public readonly T Current => _ringBuffer[_index];

		readonly object IEnumerator.Current => Current;
	}
}

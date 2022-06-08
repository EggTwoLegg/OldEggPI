using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using EggPI.Nav;


//====
namespace EggPI
{
//====
	

public unsafe partial struct NativeDataWriter : IDisposable
{
	[NativeDisableUnsafePtrRestriction] 
	internal GrowableBuffer data;
	
	public byte*	 buffer	   	 => data.buffer;
	public int 		 length    	 => data.length;
	public int		 bitpack_idx => data.bitpack_index;
	public int		 bitpack_pos => data.bitpack_pos;
	public int 		 capacity  	 => data.capacity;
	public Allocator allocator 	 => data.allocator;
	
	private bool auto_resize, is_deferred;
	private int  mask_pos;

	public NativeDataWriter(bool auto_resize, Allocator allocator)
	{
		data = new GrowableBuffer(UnsafeUtility.SizeOf<byte>(), 64, UnsafeUtility.AlignOf<byte>(), allocator);

		this.auto_resize = auto_resize;
		mask_pos    	 = -1;
		is_deferred 	 = false;
	}
	
	public NativeDataWriter(int initial_capacity, bool auto_resize, Allocator allocator)
	{
		initial_capacity = math.max(1, initial_capacity);
		
		data = new GrowableBuffer(UnsafeUtility.SizeOf<byte>(), initial_capacity, UnsafeUtility.AlignOf<byte>(), allocator);

		this.auto_resize = auto_resize;
		mask_pos    	 = -1;
		is_deferred 	 = false;
	}

	public NativeDataWriter(GrowableBuffer buffer, bool auto_resize)
	{
		data = buffer;
		
		this.auto_resize = auto_resize;
		mask_pos 		 = -1;
		is_deferred 	 = true;
	}
	
	public NativeDataWriter(byte* buffer, int length, bool auto_resize, Allocator allocator)
	{
		data = new GrowableBuffer(buffer, length, allocator);
		
		this.auto_resize = auto_resize;
		mask_pos    	 = -1;
		is_deferred 	 = true;
	}
	
	public static void*
	GetCombinedBuffers(NativeDataWriter writer0, NativeDataWriter writer1, Allocator alloc, out int length)
	{
		byte* buf = (byte*)UnsafeUtility.Malloc(writer0.length + writer1.length, UnsafeUtility.AlignOf<byte>(), alloc);
		
		UnsafeUtility.MemCpy(buf, writer0.buffer, writer0.length);
		UnsafeUtility.MemCpy(buf + writer0.length, writer1.buffer, writer1.length);

		length = writer0.length + writer1.length;

		return buf;
	}
	
	public static void*
	GetCombinedBuffers(NativeDataWriter writer0, NativeDataWriter writer1, NativeDataWriter writer2, Allocator alloc, out int length)
	{
		byte* buf = (byte*)UnsafeUtility.Malloc(writer0.length + writer1.length + writer2.length, UnsafeUtility.AlignOf<byte>(), alloc);
		
		UnsafeUtility.MemCpy(buf, writer0.buffer, writer0.length);
		UnsafeUtility.MemCpy(buf + writer0.length, writer1.buffer, writer1.length);
		UnsafeUtility.MemCpy(buf + writer0.length + writer1.length, writer2.buffer, writer2.length);
		
		length = writer0.length + writer1.length + writer2.length;

		return buf;
	}

	public void 
	ResizeIfNeed(int newsize)
	{
		if(data.capacity < newsize)
		{
			data.SetCapacity<byte>(newsize);
		}
	}

	public void 
	Reset(int size)
	{
		ResizeIfNeed(size);
		data.length = 0;
	}

	public void 
	Reset()
	{
		data.length = 0;
	}

	public void 
	Put(float value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 4);
		
		*(float*)(data.buffer + data.length) = value;
		data.length += sizeof(float);
	}
	
	public void
	Put(float2 value)
	{
		if(auto_resize) { ResizeIfNeed(data.length + 8); }
		
		*(float2*)(data.buffer + data.length) = value;
		data.length += 8;
	}
	
	public void 
	Put(float3 value)
	{	
		if(auto_resize) { ResizeIfNeed(data.length + 12); }

		*(float3*)(data.buffer + data.length) = value;
		data.length += 12;
	}
	
	public void 
	Put(float4 value)
	{	
		if(auto_resize) { ResizeIfNeed(data.length + 16); }

		*(float4*)(data.buffer + data.length) = value;
		data.length += 16;
	}

	public void 
	Put(double value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 8);
		
		*(double*)(data.buffer + data.length) = value;
		data.length += 8;
	}

	public void 
	Put(long value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 8);
		
		*(long*)(data.buffer + data.length) = value;
		data.length += 8;
	}

	public void 
	Put(ulong value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 8);
		
		*(ulong*)(data.buffer + data.length) = value;
		data.length += 8;
	}

	public void 
	Put(int value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 4);
		
		*(int*)(data.buffer + data.length) = value;
		data.length += 4;
	}
	
	// http://sqlite.org/src4/doc/trunk/www/varint.wiki
	public void 
	PutPackedUInt32(uint value)
	{
		if (value <= 240)
		{
			Put((byte)value);
			return;
		}
		if (value <= 2287)
		{
			Put((byte)((value - 240) / 256 + 241));
			Put((byte)((value - 240) % 256));
			return;
		}
		if (value <= 67823)
		{
			Put((byte)249);
			Put((byte)((value - 2288) / 256));
			Put((byte)((value - 2288) % 256));
			return;
		}
		if (value <= 16777215)
		{
			Put((byte)250);
			Put((byte)(value & 0xFF));
			Put((byte)((value >> 8) & 0xFF));
			Put((byte)((value >> 16) & 0xFF));
			return;
		}

		// all other values of uint
		Put((byte)251);
		Put((byte)(value & 0xFF));
		Put((byte)((value >> 8) & 0xFF));
		Put((byte)((value >> 16) & 0xFF));
		Put((byte)((value >> 24) & 0xFF));
	}

	public void 
	Put(uint value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 4);
		
		*(uint*)(data.buffer + data.length) = value;
		data.length += 4;
	}
	
	public NativeDataWriter
	_(ushort chr)
	{
		Put(chr);

		return this;
	}
	
	public void
	Put(ushort* chars, int num_chars)
	{
		if(auto_resize) { ResizeIfNeed(data.length + UnsafeUtility.SizeOf<ushort>() * num_chars); }
		
		UnsafeUtility.MemCpy(data.buffer + data.length, chars, UnsafeUtility.SizeOf<ushort>() * num_chars);

		data.length += UnsafeUtility.SizeOf<ushort>() * num_chars;
	}

	public void 
	Put(ushort value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 2);
		
		*(ushort*)(data.buffer + data.length) = value;
		data.length += 2;
	}

	public void 
	Put(short value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 2);
		
		*(short*)(data.buffer + data.length) = value;
		data.length += 2;
	}

	public void 
	Put(sbyte value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 1);
		
		*(sbyte*)(data.buffer + data.length) = value;
		data.length = data.length + 1;
	}

	public void 
	Put(byte value)
	{
		if (auto_resize)
			ResizeIfNeed(data.length + 1);
		
		*(byte*)(data.buffer + data.length) = value;
		data.length = data.length + 1;
	}

	public void 
	Put(bool value)
	{
		if(bitpack_pos == 0 && auto_resize)
		{
			ResizeIfNeed(data.length + 1);
			data.bitpack_index = data.length;
			data.length 	   = data.length + 1;
		}

		var val = value ? (byte)1 : (byte)0;
		
		*(byte*)(data.buffer + data.bitpack_index) |= (byte)(val << bitpack_pos);
		data.bitpack_pos = (data.bitpack_pos + 1) % 8;
	}
	
	public void
	PutBits(byte val)
	{
		var num_bits = 1;
		while((val >>= 1) > 0)
		{
			num_bits++;
		}
		
		PutBits(val, num_bits);
	}
	
	public void
	PutBits(byte val, int num_bits)
	{	
		// Just put the value if we use all 8 bits.
		if(num_bits == 8)
		{
			Put(val);
			return;
		}
		
		// Need to allocate a bitpack index and (potentially) grow our buffer.
		if(bitpack_pos == 0 && auto_resize)
		{
			ResizeIfNeed(data.length + 1);
			data.bitpack_index = data.length;
			data.length 	   = data.length + 1;
		}
		
		// We might be split between 2 bitpack bytes when writing.
		if(num_bits + bitpack_pos > 8)
		{			
			// Write the data to the upper area of the byte.
			*(byte*)(data.buffer + data.bitpack_index) |= (byte)(val << bitpack_pos);

			// How many bits were written to the remainder of the byte?
			var num_leftover_bits = 8 - bitpack_pos;
			val >>= num_leftover_bits;
			
			// Set bitpack index to the next writable byte and resize our internal capacity (if needed).
			ResizeIfNeed(data.length + 1);
			data.bitpack_index = data.length;
			data.length 	   = data.length + 1;

			data.bitpack_pos = 0;
		}
		
		*(byte*)(data.buffer + data.bitpack_index) |= (byte)(val << bitpack_pos);

		data.bitpack_pos += num_bits % 8;
	}
	
	public void 
	Put(NavmeshNode node)
	{
		int sz = UnsafeUtility.SizeOf<NavmeshNode>();
		if(auto_resize) { ResizeIfNeed(data.length + sz); }
		
		*(NavmeshNode*)(data.buffer + data.length) = node;

		data.length += sz;
	}
	
	public void
	PutBytes<T>(void* srcbuf, int len) where T : struct
	{
		var sz = UnsafeUtility.SizeOf<T>() * len;
		
		if (auto_resize) { ResizeIfNeed(data.length + sz); }
		
		UnsafeUtility.MemCpy((void*)(data.buffer + data.length), srcbuf, sz);

		data.length += sz;
	}
	
	public void 
	StartMask()
	{
		mask_pos = data.length;

		data.length += 4;
	}
	
	public void
	EndMask()
	{
		mask_pos = -1;
	}
	
	public void
	AppendToMask(int value)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(mask_pos < 0)
			{
				throw new IndexOutOfRangeException("Mask hasn't been set for writing. Please call 'StartMask,' first.");
			}
		#endif
		
		*(int*)(data.buffer + mask_pos) |= value;
	}
	
	public void
	ToggleMaskBits(int bits)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(mask_pos < 0)
			{
				throw new IndexOutOfRangeException("Mask hasn't been set for writing. Please call 'StartMask,' first.");
			}
		#endif
		
		*(int*)(data.buffer + mask_pos) ^= bits;
	}

	public void 
	Dispose()
	{
		if(is_deferred)
		{
			throw new InvalidOperationException("You can't dispose this Writer's data, as it was deferred (allocated somewhere else).");
		}
		
		data.Dispose();
	}
}
	
[NativeContainer]
public unsafe struct NativeConcurrentDataWriter
{
	internal NativeDataWriter writer;

	public NativeDataWriter Do => writer;

	public NativeConcurrentDataWriter(NativeDataWriter writer)
	{
		this.writer = writer;
	}
	
	public NativeConcurrentDataWriter(byte* buffer, int length, bool auto_resize, Allocator allocator)
	{
		writer = new NativeDataWriter(buffer, length, auto_resize, allocator);
	}
	
	public NativeConcurrentDataWriter(GrowableBuffer buffer, bool auto_resize)
	{
		writer = new NativeDataWriter(buffer, auto_resize);
	}
	
	public NativeDataWriter
	BeginWrite()
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			const int LOCK_WAIT_TIMEOUT = 100_000_000;
			int num_cycles_waited = 0;
		#endif
		
		while (Interlocked.CompareExchange(ref *writer.data.threadlock, 1, 0) != 0)
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(num_cycles_waited >= LOCK_WAIT_TIMEOUT)
				{
					throw new ThreadStateException("Other threads have taken too long to unlock this Writer's data. " +
												   "Make sure you call NativeConcurrentDataWriter.EndWrite() when you are finished writing.");
				}
				num_cycles_waited++;
			#endif
		} 

		return writer;
	}
	
	public void
	EndWrite()
	{
		Interlocked.Exchange(ref *writer.data.threadlock, 0); // Free thread locked writes so that other threads may use the writer.
	}
}
	
	
//====
}
//====

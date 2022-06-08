using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI
{
//====


public unsafe struct GrowableBuffer : IDisposable
{	
	public const int HEADER_SIZE = 24;
	
	[NativeDisableUnsafePtrRestriction]
	internal byte* raw;

	public byte* buffer => (raw + HEADER_SIZE);
	
	public int length
	{
		get => *(int*)(raw + 0);
		set => *(int*)(raw + 0) = value;
	}
	
	public int capacity
	{
		get => *(int*)(raw + 4);
		set => *(int*)(raw + 4) = value;
	}
	
	public Allocator allocator
	{
		get => *(Allocator*)(raw + 8);
		set => *(Allocator*)(raw + 8) = value;
	}
	
	public int bitpack_index
	{
		get => *(int*)(raw + 12);
		set => *(int*)(raw + 12) = value;
	}
	
	public int bitpack_pos
	{
		get => *(int*)(raw + 16);
		set => *(int*)(raw + 16) = value;
	}

	[NativeDisableUnsafePtrRestriction]
	public int* threadlock;

	public bool IsCreated => (IntPtr)raw != IntPtr.Zero;
	
	public GrowableBuffer(int per_size, int capacity, int align, Allocator allocator)
	{
		raw    	    = (byte*)UnsafeUtility.Malloc(HEADER_SIZE + (long)per_size * capacity, align, allocator);
		threadlock  = (int*)(raw + 20);
		*threadlock = 0;
		length 	    = 0;
		
		this.allocator = allocator;
		this.capacity  = math.max(0, capacity);

		bitpack_pos = 0;
	}
	
	public GrowableBuffer(byte* buffer, int len, Allocator allocator)
	{
		if(len < 1) { throw new InvalidOperationException("Length must be at least 1"); }
		
		raw = (byte*)UnsafeUtility.Malloc(HEADER_SIZE + len, UnsafeUtility.AlignOf<byte>(), allocator);

		var buf = raw + HEADER_SIZE;
		buf 	= buffer;

		threadlock     = (int*)(raw + 20);
		*threadlock    = 0;
		length   	   = len;
		capacity 	   = len;
		this.allocator = allocator;
	}
	
	public static GrowableBuffer*
	AllocWithPointer<T>(int initial_capacity, Allocator allocator) where T : struct
	{
		var buf = UnsafeUtility.Malloc(UnsafeUtility.SizeOf<GrowableBuffer>() * initial_capacity, UnsafeUtility.AlignOf<GrowableBuffer>(), 
			allocator);

		return (GrowableBuffer*)buf;
	}
	
	public void
	Dispose()
	{
		UnsafeUtility.Free(raw, allocator);
	}
	
	public int
	GetLength<T>() where T : struct
	{
		var sz = UnsafeUtility.SizeOf<T>();
		return length / sz;
	}
	
	public T
	Get<T>(int i_get) where T : struct
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(IsCreated) { throw new NullReferenceException("Buffer of InnerArrayData has not been allocated!"); }
				if(i_get < 0 || i_get >= length / UnsafeUtility.SizeOf<T>()) { throw new IndexOutOfRangeException(); }
		#endif
		
		return UnsafeUtility.ReadArrayElement<T>(buffer, i_get);
	}
	
	public void
	Add<T>(T value) where T : struct
	{
		// Expand buffer's allocation, if necessary.
		if(length >= capacity)
		{
			int new_cap = math.ceilpow2(capacity + 1);
			
			if(new_cap == capacity)
			{
				throw new OutOfMemoryException($"GrowableBuffer has reached max capacity of {int.MaxValue}.");
			}
			
			SetCapacity<T>(new_cap);
		}
		
		UnsafeUtility.WriteArrayElement(buffer, length, value);

		length += UnsafeUtility.SizeOf<T>();
	}
	
	public void
	InsertAt<T>(int idx, T value) where T : struct
	{
		if(idx == length)
		{
			Add(value);
			return;
		}
		
		// Expand buffer's allocation, if necessary.
		if(idx >= capacity)
		{
			int new_cap = math.ceilpow2(capacity + 1);
			
			if(new_cap == capacity)
			{
				throw new OutOfMemoryException($"GrowableBuffer has reached max capacity of {int.MaxValue}.");
			}
			
			SetCapacity<T>(new_cap);
		}
		
		var sz = UnsafeUtility.SizeOf<T>();
		
		// Shift everything starting at the specified index over by one.
		UnsafeUtility.MemMove(buffer + (idx * sz + sz), buffer + idx * sz, (length - idx) * sz);
		
		UnsafeUtility.WriteArrayElement(buffer, idx, value);
	}
	
	public void
	Set<T>(int i_set, T value) where T : struct
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(IsCreated) { throw new NullReferenceException("GrowableBuffer has not been allocated!"); }
				if(i_set < 0 || i_set >= length / UnsafeUtility.SizeOf<T>()) { throw new IndexOutOfRangeException(); }
		#endif
		
		UnsafeUtility.WriteArrayElement(buffer, i_set, value);
	}
	
	public void
	RemoveAtSwapBack<T>(int i_rem) where T : struct
	{
		var sz = UnsafeUtility.SizeOf<T>();
		
		var new_len = length - sz;
		UnsafeUtility.WriteArrayElement(buffer, i_rem, Get<T>(new_len / sz));
		
		length = new_len;
	}

	public void
	SetCapacity<T>(int new_cap) where T : struct
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(new_cap < 0) { throw new IndexOutOfRangeException(); }
		#endif
		
		var t_sz   = UnsafeUtility.SizeOf<T>();
		var t_al   = UnsafeUtility.AlignOf<T>();
		var tot_sz = HEADER_SIZE + t_sz * new_cap;
		
		var new_buf = UnsafeUtility.Malloc(tot_sz, t_al, allocator);
		
		UnsafeUtility.MemCpy(new_buf, raw, tot_sz);
		UnsafeUtility.Free(raw, allocator);

		raw   	 = (byte*)new_buf;
		capacity = new_cap;
	}
	
	public void
	Clear()
	{
		length = 0;
	}
	
	public NativeArray<T>
	ToNativeArray<T>() where T : struct
	{
		return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(buffer, GetLength<T>(), allocator);
	}
	
	public static GrowableBuffer
	FromRawPtr(byte* raw)
	{
		var buf = new GrowableBuffer()
		{
			raw = raw
		};

		return buf;
	}
}


//====
}
//====
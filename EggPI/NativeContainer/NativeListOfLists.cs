using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI
{
//====

	
public unsafe struct NativeListOfListsData : IDisposable
{	
	public const int HEADER_SIZE = 12;
	
	internal byte* raw;

	public GrowableBuffer* buffer => (GrowableBuffer*)(raw + HEADER_SIZE);
	
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

	public bool IsCreated => (IntPtr)raw != IntPtr.Zero;
	
	public NativeListOfListsData(int capacity, Allocator allocator)
	{
		raw = (byte*)UnsafeUtility.Malloc(HEADER_SIZE + (long)UnsafeUtility.SizeOf<GrowableBuffer>() * capacity, 
			UnsafeUtility.AlignOf<GrowableBuffer>(), allocator);
		length = 0;
		this.capacity  = math.max(0, capacity);
		this.allocator = allocator;
	}
	
	public void
	Dispose()
	{
		UnsafeUtility.Free(raw, allocator);
	}

	public void
	SetCapacity(int new_cap)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(new_cap < 0) { throw new IndexOutOfRangeException(); }
		#endif
		
		var t_sz   = UnsafeUtility.SizeOf<GrowableBuffer>();
		var t_al   = UnsafeUtility.AlignOf<GrowableBuffer>();
		var tot_sz = HEADER_SIZE + t_sz * new_cap;
		
		var new_buf = UnsafeUtility.Malloc(tot_sz, t_al, allocator);
		
		UnsafeUtility.MemCpy(new_buf, raw, HEADER_SIZE + t_sz * capacity);
		UnsafeUtility.Free(raw, allocator);

		raw   	 = (byte*)new_buf;
		capacity = new_cap;
	}
}
	
[NativeContainer]
[NativeContainerSupportsDeallocateOnJobCompletion]
public unsafe struct NativeListOfLists<T> : IDisposable, IEnumerable<T>, IEquatable<NativeListOfLists<T>> where T : struct
{
	[NativeDisableUnsafePtrRestriction] 
	internal NativeListOfListsData* data;

	#if ENABLE_UNITY_COLLECTIONS_CHECKS		
		[NativeSetClassTypeToNullOnSchedule]
		private DisposeSentinel    m_DisposeSentinel;
	
		private AtomicSafetyHandle m_Safety;
	#endif
	
	public int 		 length    => IsCreated ? data->length 	  : 0;
	public int		 capacity  => IsCreated ? data->capacity  : 0;
	public Allocator allocator => IsCreated ? data->allocator : Allocator.None;

	public bool IsCreated => (IntPtr)data != IntPtr.Zero && data->IsCreated;
	
	public T this[int x, int y]
	{
		get => UnsafeUtility.ReadArrayElement<GrowableBuffer>(data->buffer, x).Get<T>(y);
		set => UnsafeUtility.ReadArrayElement<GrowableBuffer>(data->buffer, x).Set<T>(y, value);
	}
	
	private GrowableBuffer this[int x]
	{
		get	=> UnsafeUtility.ReadArrayElement<GrowableBuffer>(data->buffer, x);
		set => *(data->buffer + x) = value;
	}

	public NativeListOfLists(int initial_length, Allocator allocator)
	{
		data = (NativeListOfListsData*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<NativeListOfListsData>(),
			UnsafeUtility.AlignOf<NativeListOfListsData>(), allocator);
		*data = new NativeListOfListsData(initial_length, allocator);
		
		if(allocator <= Allocator.None)
		{
			throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof (allocator));
		}

		initial_length = math.max(1, initial_length);
		long size = UnsafeUtility.SizeOf<GrowableBuffer>() * (long)initial_length;
		
		if(size > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException(nameof(length), $"Length * sizeof(T) cannot exceed {int.MaxValue} bytes");
		}
		
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 1, allocator);
		#endif
		
		int per_size = UnsafeUtility.SizeOf<T>();
	}
	
	public NativeListOfLists(int initial_length, int per_inner_cap, Allocator allocator)
	{
		data = (NativeListOfListsData*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<NativeListOfListsData>(),
			UnsafeUtility.AlignOf<NativeListOfListsData>(), allocator);
		*data = new NativeListOfListsData(initial_length, allocator);
		
		if(allocator <= Allocator.None)
		{
			throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof (allocator));
		}

		initial_length = math.max(1, initial_length);
		long size = UnsafeUtility.SizeOf<GrowableBuffer>() * (long)initial_length;
		
		if(size > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException(nameof(length), $"Length * sizeof(T) cannot exceed {int.MaxValue} bytes");
		}
		
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 1, allocator);
		#endif
		
		int per_size = UnsafeUtility.SizeOf<T>();
		
		// Pre-allocate inner lists.
		for(int i_inner = 0; i_inner < initial_length; i_inner++)
		{
			this[i_inner] = new GrowableBuffer(per_size, per_inner_cap, UnsafeUtility.AlignOf<T>(), allocator);
		}
	}
	
	[WriteAccessRequired]
	public void 
	Dispose()
	{		
		// Go through each allocated list in our buffer and dispose their contents.
		var len = data->length;
		for(int i_list = 0; i_list < len; i_list++)
		{
			var list = this[i_list];
			
			// Uninitialized data.
			if(!list.IsCreated) { continue; }
			
			this[i_list].Dispose();
		}
		
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
		#endif

		data->Dispose();
	}
	
	[WriteAccessRequired]
	public void
	Add(int i_arr, T value)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_arr < 0 || i_arr >= data->length) { throw new IndexOutOfRangeException(); }
		#endif
		
		this[i_arr].Add(value);
	}
	
	public void
	AddInnerList(int initial_capacity = 1)
	{
		// Expand buffer's allocation, if necessary.
		if(data->length >= data->capacity)
		{
			int new_cap = math.ceilpow2(data->capacity + 1);
			
			if(new_cap == data->capacity)
			{
				throw new OutOfMemoryException($"GrowableBuffer has reached max capacity of {int.MaxValue}.");
			}

			data->SetCapacity(new_cap);
		}

		this[data->length] = new GrowableBuffer(UnsafeUtility.SizeOf<T>(), initial_capacity, UnsafeUtility.AlignOf<T>(), data->allocator);
	}

	public void
	RemoveListAtSwapBack(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= data->length) { throw new IndexOutOfRangeException(); }
		#endif

		var newlen = data->length - 1;
		GrowableBuffer last = this[newlen];
		this[i_list] = last;
	}
	
	[WriteAccessRequired]
	public void
	RemoveAtSwapBack(int x, int y)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(x < 0 || x >= data->length) { throw new IndexOutOfRangeException(); }
		#endif
			
		this[x].RemoveAtSwapBack<T>(y);
	}
	
	public void
	Clear()
	{
		for(int i_inner = 0; i_inner < data->length; i_inner++)
		{
			var inner = this[i_inner];
			inner.length = 0;
		}
	}
	
	public void
	Clear(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= data->length) { throw new IndexOutOfRangeException(); }
		#endif
		
		var list = this[i_list];
		list.length = 0;
	}
	
	[WriteAccessRequired]
	public int
	GetListLength(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= data->length) { throw new IndexOutOfRangeException(); }
		#endif

		return this[i_list].length;
	}
	
	public GrowableBuffer
	GetListBuffer(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= length) { throw new IndexOutOfRangeException(); }
		#endif

		return this[i_list];
	}
	
	public bool
	IsListCreated(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= data->length) { throw new IndexOutOfRangeException(); }
		#endif
		
		return this[i_list].IsCreated;
	}

	public IEnumerator<T> 
	GetEnumerator()
	{
		throw new NotImplementedException();
	}

	IEnumerator 
	IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public bool 
	Equals(NativeListOfLists<T> other)
	{
		return data == other.data;
	}

	public NativeArrayOfLists<T>
	ToArrayOfLists()
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			return NativeArrayOfLists<T>.FromExistingData(data->buffer, data->length, data->allocator, m_Safety);
		#else
			return NativeArrayOfLists<T>.FromExistingData(data->buffer, data->length, data->allocator);
		#endif
	}
}


//====
}
//====
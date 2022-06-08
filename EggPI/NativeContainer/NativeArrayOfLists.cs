using System.Runtime.InteropServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI
{
//====


[NativeContainer]
[NativeContainerSupportsDeallocateOnJobCompletion]
[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeArrayOfLists<T> : IDisposable, IEnumerable<T>, IEquatable<NativeArrayOfLists<T>> where T : struct
{ 
	[NativeDisableUnsafePtrRestriction]
	internal GrowableBuffer* m_Buffer;
	
	internal int m_Length; // Must be named as such to work with unity's safety systems and ParallelFor jobs.

	#if ENABLE_UNITY_COLLECTIONS_CHECKS		
		private AtomicSafetyHandle m_Safety;

		[NativeSetClassTypeToNullOnSchedule]
		private DisposeSentinel m_DisposeSentinel;
	#endif
	
	public int Length => m_Length;
	
	internal Allocator m_AllocatorLabel;
	public   Allocator Allocator => m_AllocatorLabel;
	
	public bool IsCreated => (IntPtr)m_Buffer != IntPtr.Zero;
	
	public T this[int x, int y]
	{
		get => UnsafeUtility.ReadArrayElement<GrowableBuffer>(m_Buffer, x).Get<T>(y);
		set => UnsafeUtility.ReadArrayElement<GrowableBuffer>(m_Buffer, x).Set<T>(y, value);
	}
	
	private GrowableBuffer this[int x]
	{
		get	=> UnsafeUtility.ReadArrayElement<GrowableBuffer>(m_Buffer, x);
		set => *(m_Buffer + x) = value;
	}

	public NativeArrayOfLists(int length, Allocator allocator)
	{
		m_Length = length;
		
		long size = UnsafeUtility.SizeOf<GrowableBuffer>() * (long)length;
		
		if(allocator <= Allocator.None)
		{
			throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof (allocator));
		}
		
		if(length < 1)
		{
			throw new ArgumentOutOfRangeException(nameof (length), "Length must be >= 1");
		}
		
		if(size > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException(nameof(length), $"Length * sizeof(T) cannot exceed {int.MaxValue} bytes");
		}
		
		m_Buffer = (GrowableBuffer*)UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<GrowableBuffer>(), allocator);
		
		m_AllocatorLabel = allocator;
		
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 1, allocator);
		#endif
		
		int per_size = UnsafeUtility.SizeOf<T>();
		
		// Create inner arrays.
		for(int i_inner = 0; i_inner < length; i_inner++)
		{
			this[i_inner] = new GrowableBuffer(per_size, 1, UnsafeUtility.AlignOf<T>(), allocator);
		}
	}
	
	public NativeArrayOfLists(int length, int per_inner_cap, Allocator allocator)
	{
		m_Length = length;
		
		long size = UnsafeUtility.SizeOf<GrowableBuffer>() * (long)length;
		
		if(allocator <= Allocator.None)
		{
			throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof (allocator));
		}
		
		if(length < 1)
		{
			throw new ArgumentOutOfRangeException(nameof (length), "Length must be >= 1");
		}
		
		if(size > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException(nameof(length), $"Length * sizeof(T) cannot exceed {int.MaxValue} bytes");
		}
		
		m_Buffer = (GrowableBuffer*)UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<GrowableBuffer>(), allocator);
		
		m_AllocatorLabel = allocator;
		
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 1, allocator);
#endif
		
		int per_size = UnsafeUtility.SizeOf<T>();
		
		// Create inner arrays.
		for(int i_inner = 0; i_inner < length; i_inner++)
		{
			this[i_inner] = new GrowableBuffer(per_size, per_inner_cap, UnsafeUtility.AlignOf<T>(), allocator);
		}
	}
	
	[WriteAccessRequired]
	public void 
	Dispose()
	{
		if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
		{
			throw new InvalidOperationException("The NativeArrayOfLists can't be disposed because it wasn't allocated with a valid allocator.");
		}
		
		// Go through each allocated list in our buffer and dispose their contents.
		for(int i_list = 0; i_list < m_Length; i_list++)
		{
			var data = this[i_list];
			
			// Uninitialized data.
			if(!data.IsCreated) { continue; }
			
			data.Dispose();
		}
		
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
		#endif
		
		UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);

		m_Length = 0;
		m_Buffer = null;
	}
	
	[WriteAccessRequired]
	public void
	Add(int i_arr, T value)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_arr < 0 || i_arr >= m_Length) { throw new IndexOutOfRangeException(); }
		#endif
		
		this[i_arr].Add(value);
	}

	[WriteAccessRequired]
	public void
	RemoveAtSwapBack(int x, int y)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(x < 0 || x >= m_Length) { throw new IndexOutOfRangeException(); }
		#endif
			
		this[x].RemoveAtSwapBack<T>(y);
	}
	
	public void
	Clear()
	{
		for(int i_inner = 0; i_inner < m_Length; i_inner++)
		{
			var inner = this[i_inner];
			inner.length = 0;
		}
	}
	
	public void
	Clear(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException(); }
		#endif
		
		var list = this[i_list];
		list.length = 0;
	}
	
	[WriteAccessRequired]
	public int
	GetListLength(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException(); }
		#endif

		return this[i_list].length;
	}
	
	public GrowableBuffer
	GetListBuffer(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException(); }
		#endif

		return this[i_list];
	}
	
	public bool
	IsListCreated(int i_list)
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException(); }
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
	Equals(NativeArrayOfLists<T> other)
	{
		return m_Buffer == other.m_Buffer;
	}
	
	public NativeArrayOfLists<T>.Concurrent
	ToConcurrent()
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			return new Concurrent(m_Buffer, m_Length, m_DisposeSentinel, m_Safety);
		#else
			return new Concurrent(buffer, m_Length);
		#endif
	}
	
	public static NativeArrayOfLists<T>
	FromExistingData(GrowableBuffer* buffer, int length, Allocator allocator, AtomicSafetyHandle safety)
	{
		var arr = new NativeArrayOfLists<T>()
		{
			m_Buffer = buffer,
			m_Length = length,
			m_AllocatorLabel = allocator,
			m_Safety = safety
		};

		return arr;
	}
	
	public static NativeArrayOfLists<T>
	FromExistingData(GrowableBuffer* buffer, int length, Allocator allocator)
	{
		var arr = new NativeArrayOfLists<T>()
		{
			m_Buffer = buffer,
			m_Length = length,
			m_AllocatorLabel = allocator,
		};

		return arr;
	}

	[NativeContainer]
	[NativeContainerIsAtomicWriteOnly]
	public struct Concurrent
	{
		[NativeDisableUnsafePtrRestriction] 
		internal GrowableBuffer* m_Buffer;

		[NativeSetThreadIndex]
		internal int m_ThreadIndex;
		
		internal int m_Length; // Must be named as such to work with unity's safety systems and ParallelFor jobs.

		#if ENABLE_UNITY_COLLECTIONS_CHECKS			
			[NativeSetClassTypeToNullOnSchedule]
			private DisposeSentinel m_DisposeSentinel;
		
			private AtomicSafetyHandle m_Safety;
		#endif
	
		public int Length => m_Length;
		
		private GrowableBuffer this[int x]
		{
			get	=> UnsafeUtility.ReadArrayElement<GrowableBuffer>(m_Buffer, x);
			set => *(m_Buffer + x) = value;
		}
		
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			public Concurrent(GrowableBuffer* m_Buffer, int length, DisposeSentinel dispose, AtomicSafetyHandle safety)
			{
				this.m_Buffer     = m_Buffer;
				m_ThreadIndex 	  = 0;
				m_Length 	 	  = length;
				m_DisposeSentinel = dispose;
				m_Safety  		  = safety;
			}
		#else
			public Concurrent(GrowableBuffer* buffer, int length)
			{
				this.m_Buffer = m_Buffer;
				m_ThreadIndex = 0;
				m_Length 	  = length;
			}
		#endif
	
		[WriteAccessRequired]
		public bool
		Get(int i_list, int i_inner, out T value)
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException(); }
			#endif

			var list = this[i_list];
			
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(i_inner < 0 || i_inner > list.length) { throw new IndexOutOfRangeException(); }
			#endif

			bool res = true;
			
			// Need to write lock to prevent other threads from writing over data we might want to read from.
			while (Interlocked.CompareExchange(ref *list.threadlock, 1, 0) != 0) {} // Wait for other threads to finish writing.

			if (i_inner > list.length)
			{
				res   = false;
				value = default;
			}
			else
			{
				value = list.Get<T>(i_inner);
			}
			
			Interlocked.Exchange(ref *list.threadlock, 0); // Free thread locked writes so that other threads may write to the inner data.

			return res;
		}
		
		[WriteAccessRequired]
		public int
		GetInnerLength(int i_list)
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException(); }
			#endif

			var list = this[i_list];
			
			// Need to write lock to prevent other threads from writing over data we might want to read from.
			while (Interlocked.CompareExchange(ref *list.threadlock, 1, 0) != 0) {} // Wait for other threads to finish writing.

			var len = list.length;
			
			Interlocked.Exchange(ref *list.threadlock, 0); // Free thread locked writes so that other threads may write to the inner data.

			return len;
		}
		
		public GrowableBuffer
		GetListBuffer(int i_list)
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException(); }
			#endif

			var list = this[i_list];
			
			// Need to write lock to prevent other threads from writing over data we might want to read from.
			while (Interlocked.CompareExchange(ref *list.threadlock, 1, 0) != 0) {} // Wait for other threads to finish writing.
			Interlocked.Exchange(ref *list.threadlock, 0); // Free thread locked writes so that other threads may write to the inner data.

			return list;
		}
		
		[WriteAccessRequired]
		public void
		Add(int i_list, T value)
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException($"{i_list} is outside of the outer array bounds."); }
			#endif

			var list = this[i_list];
			
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				const int LOCK_WAIT_TIMEOUT = 100_000_000;
				int num_cycles_waited = 0;
			#endif
			
			while (Interlocked.CompareExchange(ref *list.threadlock, 1, 0) != 0) {} // Wait for other threads to finish writing.
			{
				#if ENABLE_UNITY_COLLECTIONS_CHECKS
					if(num_cycles_waited >= LOCK_WAIT_TIMEOUT)
					{
						throw new ThreadStateException("Thread access stall!");
					}
					num_cycles_waited++;
				#endif
			} 

			list.Add(value);
			
			Interlocked.Exchange(ref *list.threadlock, 0); // Free thread locked writes so that other threads may write to the inner data.
		}
		
		[WriteAccessRequired]
		public void
		Clear(int i_list)
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if(i_list < 0 || i_list >= m_Length) { throw new IndexOutOfRangeException(); }
			#endif
			
			var list = *((GrowableBuffer*)m_Buffer + i_list);
			
			while (Interlocked.CompareExchange(ref *list.threadlock, 1, 0) != 0) {} // Wait for other threads to finish writing.

			list.length = 0;
			
			Interlocked.Exchange(ref *list.threadlock, 0); // Free thread locked writes so that other threads may write to the inner data.
		}
	}
}
		


	
//====
}
//====
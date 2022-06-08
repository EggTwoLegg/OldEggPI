//-----------------------------------------------------------------------
// <copyright file="NativeIntPtr.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.txt.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace JacksonDunstan.NativeCollections
{
	/// <summary>
	/// A pointer to an int stored in native (i.e. unmanaged) memory
	/// </summary>
	[NativeContainer]
	[NativeContainerSupportsDeallocateOnJobCompletion]
	//[DebuggerDisplay("Value = {Value}")]
	// [StructLayout(LayoutKind.Sequential)]
	public unsafe struct NativeIntPtr<T> : IDisposable where T : struct
	{
		/// <summary>
		/// An atomic write-only version of the object suitable for use in a
		/// ParallelFor job
		/// </summary>
		[NativeContainer]
		[NativeContainerIsAtomicWriteOnly]
		public struct Parallel
		{
			/// <summary>
			/// Pointer to the value in native memory
			/// </summary>
			[NativeDisableUnsafePtrRestriction]
			internal void* m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			/// <summary>
			/// A handle to information about what operations can be safely
			/// performed on the object at any given time.
			/// </summary>
			internal AtomicSafetyHandle m_Safety;

			/// <summary>
			/// Create a parallel version of the object
			/// </summary>
			/// 
			/// <param name="value">
			/// Pointer to the value
			/// </param>
			/// 
			/// <param name="safety">
			/// Atomic safety handle for the object
			/// </param>
			internal Parallel(void* value, AtomicSafetyHandle safety, int* threadlock)
			{
				m_Buffer = value;
				m_Safety = safety;
				
				this.threadlock = threadlock;
			}
#else
			/// <summary>
			/// Create a parallel version of the object
			/// </summary>
			/// 
			/// <param name="value">
			/// Pointer to the value
			/// </param>
			internal Parallel(void* value, int* threadlock)
			{
				m_Buffer = value;
				this.threadlock = threadlock;
			}
#endif
			
			[NativeDisableUnsafePtrRestriction]
			internal int* threadlock;
			
			[WriteAccessRequired]
			public void
			Set(T value)
			{
				RequireWriteAccess();	
				
				// Need to write lock to prevent other threads from writing over data we might want to read from.
				while (Interlocked.CompareExchange(ref *threadlock, 1, 0) != 0) {} // Wait for other threads to finish writing.

				UnsafeUtility.CopyStructureToPtr(ref value, m_Buffer);
				
				Interlocked.Exchange(ref *threadlock, 0); // Free thread locked writes so that other threads may write to the inner data.
			}

			/// <summary>
			/// Throw an exception if the object isn't writable
			/// </summary>
			[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
			[BurstDiscard]
			private void RequireWriteAccess()
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			}
		}

		/// <summary>
		/// Pointer to the value in native memory. Must be named exactly this
		/// way to allow for [NativeContainerSupportsDeallocateOnJobCompletion]
		/// </summary>
		[NativeDisableUnsafePtrRestriction]
		internal void* m_Buffer;

		/// <summary>
		/// Allocator used to create the backing memory
		/// 
		/// This field must be named this way to comply with
		/// [NativeContainerSupportsDeallocateOnJobCompletion]
		/// </summary>
		internal Allocator m_AllocatorLabel;

		// These fields are all required when safety checks are enabled
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		/// <summary>
		/// A handle to information about what operations can be safely
		/// performed on the object at any given time.
		/// </summary>
		private AtomicSafetyHandle m_Safety;

		/// <summary>
		/// A handle that can be used to tell if the object has been disposed
		/// yet or not, which allows for error-checking double disposal.
		/// </summary>
		[NativeSetClassTypeToNullOnSchedule]
		private DisposeSentinel m_DisposeSentinel;
#endif
		
		[NativeDisableUnsafePtrRestriction]
		internal int* threadlock;

		/// <summary>
		/// Allocate memory and set the initial value
		/// </summary>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate and deallocate with. Must be valid.
		/// </param>
		/// 
		/// <param name="initialValue">
		/// Initial value of the allocated memory
		/// </param>
		public NativeIntPtr(Allocator allocator, T initialValue = default)
		{
			// Require a valid allocator
			if (allocator <= Allocator.None)
			{
				throw new ArgumentException(
					"Allocator must be Temp, TempJob or Persistent",
					"allocator");
			}

			// Allocate the memory for the value
			m_Buffer = UnsafeUtility.Malloc(
				UnsafeUtility.SizeOf<T>(),
				UnsafeUtility.AlignOf<T>(),
				allocator);

			// Store the allocator to use when deallocating
			m_AllocatorLabel = allocator;

			// Create the dispose sentinel
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
        	DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#else
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif

			// Set the initial value
			UnsafeUtility.CopyStructureToPtr(ref initialValue, m_Buffer);
			
			threadlock  = (int*)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), allocator);
			*threadlock = 0;
		}
		
		public NativeIntPtr(void* buf, Allocator allocator)
		{
			if(allocator <= Allocator.None)
			{
				throw new ArgumentException(
					"Allocator must be Temp, TempJob or Persistent",
					"allocator");
			}

			m_Buffer = (int*)buf;

			m_AllocatorLabel = allocator;
			
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
				#if UNITY_2018_3_OR_NEWER
					DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
				#else
					DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
				#endif
			#endif
			
			threadlock  = (int*)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), allocator);
			*threadlock = 0;
		}

		/// <summary>
		/// Get or set the contained value
		/// 
		/// This operation requires read access to the node for 'get' and write
		/// access to the node for 'set'.
		/// </summary>
		/// 
		/// <value>
		/// The contained value
		/// </value>
		public T Value
		{
			get
			{
				RequireReadAccess();
				UnsafeUtility.CopyPtrToStructure<T>(m_Buffer, out var output);
				return output;
			}

			[WriteAccessRequired]
			set
			{
				RequireWriteAccess();
				UnsafeUtility.CopyStructureToPtr(ref value, m_Buffer);
			}
		}

		/// <summary>
		/// Get a version of this object suitable for use in a ParallelFor job
		/// </summary>
		/// 
		/// <returns>
		/// A version of this object suitable for use in a ParallelFor job
		/// </returns>
		public Parallel ToParallel()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			Parallel parallel = new Parallel(m_Buffer, m_Safety, threadlock);
			AtomicSafetyHandle.UseSecondaryVersion(ref parallel.m_Safety);
#else
			Parallel parallel = new Parallel(m_Buffer, threadlock);
#endif
			return parallel;
		}

		/// <summary>
		/// Check if the underlying unmanaged memory has been created and not
		/// freed via a call to <see cref="Dispose"/>.
		/// 
		/// This operation has no access requirements.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// Initially true when a non-default constructor is called but
		/// initially false when the default constructor is used. After
		/// <see cref="Dispose"/> is called, this becomes false. Note that
		/// calling <see cref="Dispose"/> on one copy of this object doesn't
		/// result in this becoming false for all copies if it was true before.
		/// This property should <i>not</i> be used to check whether the object
		/// is usable, only to check whether it was <i>ever</i> usable.
		/// </value>
		public bool IsCreated
		{
			get
			{
				return m_Buffer != null;
			}
		}

		/// <summary>
		/// Release the object's unmanaged memory. Do not use it after this. Do
		/// not call <see cref="Dispose"/> on copies of the object either.
		/// 
		/// This operation requires write access.
		/// 
		/// This complexity of this operation is O(1) plus the allocator's
		/// deallocation complexity.
		/// </summary>
		[WriteAccessRequired]
		public void Dispose()
		{
			RequireWriteAccess();

// Make sure we're not double-disposing
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
        	DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
			DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif

			UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
			m_Buffer = null;
			
			UnsafeUtility.Free(threadlock, m_AllocatorLabel);
			threadlock = null;
		}

		/// <summary>
		/// Set whether both read and write access should be allowed. This is
		/// used for automated testing purposes only.
		/// </summary>
		/// 
		/// <param name="allowReadOrWriteAccess">
		/// If both read and write access should be allowed
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		public void TestUseOnlySetAllowReadAndWriteAccess(
			bool allowReadOrWriteAccess)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.SetAllowReadOrWriteAccess(
				m_Safety,
				allowReadOrWriteAccess);
#endif
		}

		/// <summary>
		/// Throw an exception if the object isn't readable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		private void RequireReadAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
		}

		/// <summary>
		/// Throw an exception if the object isn't writable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		private void RequireWriteAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		}
		
		public static implicit operator 
		IntPtr(NativeIntPtr<T> ptr)
		{
			return (IntPtr)ptr.m_Buffer;
		}
	}

	/// <summary>
	/// Provides a debugger view of <see cref="NativeIntPtr"/>.
	/// </summary>
	internal sealed class NativeIntPtrDebugView<T> where T : struct
	{
		/// <summary>
		/// The object to provide a debugger view for
		/// </summary>
		private NativeIntPtr<T> ptr;

		/// <summary>
		/// Create the debugger view
		/// </summary>
		/// 
		/// <param name="ptr">
		/// The object to provide a debugger view for
		/// </param>
		public NativeIntPtrDebugView(NativeIntPtr<T> ptr)
		{
			this.ptr = ptr;
		}

		/// <summary>
		/// Get the viewed object's value
		/// </summary>
		/// 
		/// <value>
		/// The viewed object's value
		/// </value>
		public T Value => ptr.Value;
	}
}
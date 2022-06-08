using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityScript.Steps;


//====
namespace EggPI
{
//====
	
	
public static unsafe class FileIO
{
	public delegate void* MallocCallback(IntPtr size);
	public delegate void  FreeCallback(IntPtr buf);
	public delegate void  NoMemCallback();
	
	public static void
	Init()
	{
		BHRPGIO_Callbacks cbacks = new BHRPGIO_Callbacks
		(
			Marshal.GetFunctionPointerForDelegate(new MallocCallback(FileIO.Malloc)),
			Marshal.GetFunctionPointerForDelegate(new FreeCallback(FileIO.Free)),
			Marshal.GetFunctionPointerForDelegate(new NoMemCallback(FileIO.NoMem))
		);
		
		InitCallbacks(ref cbacks);
	}
	
	public static void*
	Malloc(IntPtr size)
	{
		return UnsafeUtility.Malloc((long)size, UnsafeUtility.AlignOf<byte>(), Allocator.Persistent);
	}
	
	public static void
	Free(IntPtr buf)
	{
		UnsafeUtility.Free((void*)buf, Allocator.Persistent);
	}
	
	public static void
	NoMem()
	{
		throw new OutOfMemoryException();
	}
	
	public static int
	Save(string path, void* data, int len)
	{
		return SaveBytes(path, data, len);
	}
	
	public static void*
	Load(string path, ref int num_bytes)
	{
		return LoadBytes(path, ref num_bytes);
	}
	
	[DllImport("bhrpg_io")]
	private static extern void InitCallbacks(ref BHRPGIO_Callbacks cbacks);
	
	[DllImport("bhrpg_io")]
	private static extern int SaveBytes(string path, void* data, int len);

	[DllImport("bhrpg_io")]
	private static extern void* LoadBytes(string path, ref int num_bytes);
}
	
[StructLayout(LayoutKind.Sequential)]
public struct BHRPGIO_Callbacks
{
	public IntPtr Malloc;
	public IntPtr Free;
	public IntPtr NoMem;
	
	public BHRPGIO_Callbacks(IntPtr malloc, IntPtr free, IntPtr nomem)
	{
		Malloc = malloc;
		Free   = free;
		NoMem  = nomem;
	}
}


//====
}
//====
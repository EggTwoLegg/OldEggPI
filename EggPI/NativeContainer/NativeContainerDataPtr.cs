using System;
using Unity.Collections;
using UnityEngine;


//====
namespace EggPI
{
//====


public struct NativeContainerDataPtr
{
	public IntPtr ptr;
	public Allocator allocator;
	public int len;
	public int capacity;
	
	public NativeContainerDataPtr(IntPtr ptr, Allocator allocator, int len, int capacity)
	{
		this.ptr = ptr;
		this.allocator = allocator;
		this.len = len;
		this.capacity = capacity;
	}
}


//====
}
//====
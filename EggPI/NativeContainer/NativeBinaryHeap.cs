using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI
{
//====


public struct NativeBinaryHeap<T> where T : struct, IHeapItem<T>
{
	public int count 	=> _count;
	public int max_size => _max_size;

	private int _count;
	private int _max_size;

	private NativeArray<T> data;
	private Allocator allocator;
	
	public NativeBinaryHeap(int heap_max_size, Allocator allocator = Allocator.Persistent)
	{
		_max_size = heap_max_size;
		data 	  = new NativeArray<T>(heap_max_size, allocator);
		_count 	  = 0;
		this.allocator = allocator;
	}
	
	public void
	Add(T item)
	{
		if(_count >= _max_size && count < int.MaxValue)
		{
			_max_size = math.min(_max_size * 2, int.MaxValue);
			
			var n_data = new NativeArray<T>(_max_size, allocator);
			NativeArray<T>.Copy(data, n_data);
			
			data.Dispose();
			data = n_data;
		}
		
		item.heap_index = count;
		data[count] = item;
		SortUp(item);
		_count++;
	}
	
	public T
	RemoveFirst()
	{
		T first = data[0];
		
		_count--;

		T last = data[_count];
		last.heap_index = 0;

		data[0] = last;
		
		SortDown(last);

		return last;
	}
	
	public void
	Dispose()
	{
		_count = 0;
		data.Dispose();
	}
	
	private void
	SortUp(T item)
	{
		T parent = data[(item.heap_index - 1) / 2];
		while(true)
		{
			if(item.CompareTo(parent) > 0)
			{
				Swap(ref item, ref parent);
			}
			else
			{
				break;
			}
			
			parent = data[(item.heap_index - 1) / 2];
		}
	}
	
	private void
	SortDown(T item)
	{
		while(true)
		{
			int i_left  = item.heap_index * 2 + 1;
			int i_right = item.heap_index * 2 + 1;
			int i_swap  = 0;
			
			if(i_left < _count) // Left smaller than parent.
			{
				i_swap = i_left;
				
				if(i_right < _count && data[i_left].CompareTo(data[i_right]) < 0) // Right smaller than left.
				{
					i_swap = i_right;
				}
				
				T swap = data[i_swap];

				if (item.CompareTo(data[i_swap]) >= 0) { return; }
				
				Swap(ref item, ref swap);
			}
			else { return; }

		}
	}
	
	private void
	Swap(ref T a, ref T b)
	{
		int i_tmp = a.heap_index;	
		a.heap_index = b.heap_index;
		b.heap_index = i_tmp;
		
		data[a.heap_index] = b;
		data[b.heap_index] = a;
	}
}
	
public interface IHeapItem<T> : IComparable<T>
{
	int heap_index { get; set; }
}


//====
}
//====
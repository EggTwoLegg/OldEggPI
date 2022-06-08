using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using EggPI.Mathematics;


//====
namespace EggPI
{
//====


[NativeContainer]
public unsafe struct NativeStaticOctree<T> : IDisposable where T : struct
{
	public const int MIN_CELL_SIZE = 2;

	private NativeListOfLists<IntPtr> nodes_by_depth;
	private NativeQueue<OctreeElem<T>>   pending_add;

	public int MaxItemsPerNode => max_items_per_node;
	public int MaxDepth 	   => max_depth;
	
	private int 	  max_items_per_node;
	private int 	  max_depth;
	private Allocator allocator;
	
	public NativeStaticOctree(int max_items_per_node, int max_depth, AABB total_bounds, Allocator allocator)
	{		
		this.max_items_per_node = max_items_per_node;
		this.max_depth 			= max_depth;
		this.allocator			= allocator;
		
		// Create root node.
		void* rootnode = (void*)UnsafeUtility.Malloc(IntPtr.Size, UnsafeUtility.AlignOf<int>(), allocator);
		UnsafeUtility.WriteArrayElement(rootnode, 0, new Node(0, total_bounds, null, null, allocator));
		
		// Root node is sorted into the '0' depth bucket.
		nodes_by_depth = new NativeListOfLists<IntPtr>(1, allocator);
		nodes_by_depth.AddInnerList();
		nodes_by_depth.Add(0, (IntPtr)rootnode);
		
		pending_add = new NativeQueue<OctreeElem<T>>(allocator);

		void* b = (void*)IntPtr.Zero;

		UnsafeUtility.CopyStructureToPtr(ref this, b);
	}
	
	public void 
	Dispose()
	{
		// Dispose all data stored in the nodes.
		var len = nodes_by_depth.length;
		for(int i_node = 0; i_node < len; i_node++)
		{
			var inner_len = nodes_by_depth.GetListLength(i_node);
			for(int i_inner = 0; i_inner < inner_len; i_inner++)
			{
				var inner = nodes_by_depth[i_node, i_inner];
				
			}
		}
	
		nodes_by_depth.Dispose();	
		pending_add.Dispose();
	}
	
	public bool
	AddElem(T elem, AABB aabb)
	{
		// If the pending element doesn't have much volume, we can't feasibly add it due to floating point precision limits.
		if(aabb.GetVolume() <= math.FLT_MIN_NORMAL) { return false; }
		
		pending_add.Enqueue(new OctreeElem<T>(elem, aabb));
		return true;
	}
	
	public void
	BuildTree()
	{
		while(pending_add.TryDequeue(out var pending))
		{
			Insert((Node*)nodes_by_depth[0, 0], pending); // Get root node, build outwards.
		}
	}
	
	private void
	Insert(Node* node, OctreeElem<T> elem)
	{
		// If there's no room to hold this item, we need to 'split' this node into octants.
		if(node->IsLeaf && node->m_Length >= max_items_per_node && node->depth < max_depth)
		{
			var min 	 = node->aabb.min;
			var max 	 = node->aabb.max;
			var halfstep = (max - min) * 0.5f;

			var children    = (Node*)UnsafeUtility.Malloc(IntPtr.Size * 8, UnsafeUtility.AlignOf<IntPtr>(), allocator);
			node->children  = children;
			var child_depth = node->depth + 1;
			
			// (-x, -y, -z)
			*(children + 0) = new Node(child_depth, new AABB(min, min + halfstep), node, null, allocator);
			nodes_by_depth.Add(child_depth, (IntPtr)children + 0);

			// (-x, -y, +z)
			*(children + 1) = new Node(child_depth, new AABB(min + halfstep.nnz(), min + halfstep.nnz() + halfstep), node, null, allocator);
			nodes_by_depth.Add(child_depth, (IntPtr)children + 1);
			
			// (+x, -y, +z)
			*(children + 2) = new Node(child_depth, new AABB(min + halfstep.xnz(), max - halfstep.nyn()), node, null, allocator);
			nodes_by_depth.Add(child_depth, (IntPtr)children + 2);
			
			// (+x, -y, -z)
			*(children + 3) = new Node(child_depth, new AABB(min + halfstep.xnn(), min + halfstep.xnn() + halfstep), node, null, allocator);
			nodes_by_depth.Add(child_depth, (IntPtr)children + 3);
			
			// (-x, +y, -z)
			*(children + 4) = new Node(child_depth, new AABB(min + halfstep.nyn(), min + halfstep.nyn() + halfstep), node, null, allocator);
			nodes_by_depth.Add(child_depth, (IntPtr)children + 4);
			
			// (-x, +y, +z)
			*(children + 5) = new Node(child_depth, new AABB(min + halfstep.nyz(), min + halfstep.nyz() + halfstep), node, null, allocator);
			nodes_by_depth.Add(child_depth, (IntPtr)children + 5);
			
			// (+x, +y, +z)
			*(children + 6) = new Node(child_depth, new AABB(min + halfstep, max), node, null, allocator);
			nodes_by_depth.Add(child_depth, (IntPtr)children + 6);
			
			// (+x, +y, -z)
			*(children + 7) = new Node(child_depth, new AABB(min + halfstep.xyn(), max - halfstep.nnz()), node, null, allocator);
			nodes_by_depth.Add(child_depth, (IntPtr)children + 7);
			
			// We've now made this node a non-leaf node, so we need to propogate all of its data to the children.
			for(int i_child = 0; i_child < 8; i_child++)
			{
				var chaabb = (children + i_child)->aabb;
				
				for(int i_elem = 0; i_elem < node->m_Length; i_elem++)
				{
					if(chaabb.Overlaps(elem.aabb))
					{
						Insert(children + i_child, node->GetElement<T>(i_elem));
					}
				}
			}

			// Free this node's data, since it's been copied to the children.
			node->m_Length = 0;
			node->Dispose();
			
			// Now see if the to-be-inserted element fits into any of the child nodes.
			for(int i_child = 0; i_child < 8; i_child++)
			{
				var chaabb = (children + i_child)->aabb;
				
				if(chaabb.Overlaps(elem.aabb))
				{
					Insert(children + i_child, elem);
				}
			}
			
			return;
		}
		
		// Otherwise, we're at the maximum depth or don't need to split the current node, so we can proceed to add the element to this node.
		if(!node->aabb.Overlaps(elem.aabb))
		{
			return;
		}

		node->AddElement(elem);
	}
}
	
public unsafe struct Node : IDisposable
{
	public int  depth;
	public AABB aabb;

	public void* parent;
	public void* children;

	public  int   	  m_Length;
	private void* 	  data;
	private Allocator allocator;

	public bool IsLeaf => (IntPtr)children == IntPtr.Zero;
	
	public Node(int depth, AABB aabb, Node* parent, Node* children, Allocator allocator)
	{
		this.depth = depth;
		this.aabb  = aabb;

		this.parent   = parent;
		this.children = children;
		
		data 	 = (void*)IntPtr.Zero;
		this.allocator = allocator;
	
		m_Length = 0;
	}
	
	public void
	AddElement<T>(OctreeElem<T> elem) where T : struct
	{
		UnsafeUtility.WriteArrayElement(data, m_Length, elem);
	}
	
	public OctreeElem<T>
	GetElement<T>(int i_elem) where T : struct
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if(i_elem < 0 || i_elem > m_Length)
			{
				throw new IndexOutOfRangeException();
			}
		#endif

		return UnsafeUtility.ReadArrayElement<OctreeElem<T>>(data, i_elem);
	}
	
	public void
	Dispose()
	{
		// This check is necessary, because we deallocate a node's data when it becomes a parent instead of a leaf node.
		if((IntPtr)data != IntPtr.Zero)
		{
			UnsafeUtility.Free(data, allocator);
		}
	}
}
	
public struct OctreeElem<T> where T : struct
{
	public AABB aabb;
	public T    elem;
	
	public OctreeElem(T elem, AABB aabb)
	{
		this.elem = elem;
		this.aabb = aabb;
	}
}


//====
}
//====
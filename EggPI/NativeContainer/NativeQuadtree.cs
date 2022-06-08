using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;


//====
namespace EggPI
{
//====


[NativeContainer]
public unsafe struct NativeQuadtree<T> : IDisposable where T : struct
{
	public struct Node : IDisposable
	{
		public int    i_first_child;
		public int    depth;
		public AABB2D aabb;

		public IntPtr elems;
		public int 	  num_elements;

		public Allocator allocator;

		public bool IsLeaf => i_first_child < 0;
		
		public Node(int depth, AABB2D aabb, Allocator allocator)
		{
			this.depth = depth;
			this.aabb  = aabb;

			this.allocator = allocator;

			i_first_child = -1;

			num_elements = 0;
			
			elems = IntPtr.Zero;
		}
		
		public void
		Dispose()
		{
			if(elems == IntPtr.Zero) { return; }
			
			UnsafeUtility.Free((void*)elems, allocator);
		}
	}
	
	public struct NodeElement
	{
		public T 	  elem;
		public AABB2D aabb;
		
		public NodeElement(T elem, AABB2D aabb)
		{
			this.elem = elem;
			this.aabb = aabb;
		}
	}

	[NativeDisableUnsafePtrRestriction] 
	private NativeList<Node> nodes;

	[NativeDisableUnsafePtrRestriction] 
	private NativeQueue<NodeElement> build_queue;

	private int max_elems_per_node;
	private int max_depth;
	
	private Allocator allocator;
	
	public NativeQuadtree(int initial_capacity, int max_elems_per_node, int max_depth, Allocator allocator)
	{
		nodes = new NativeList<Node>(initial_capacity, allocator);	
		
		build_queue = new NativeQueue<NodeElement>(Allocator.Persistent);

		this.max_elems_per_node = max_elems_per_node;
		this.max_depth 			= max_depth;
		this.allocator			= allocator;
	}
	
	public void
	Dispose()
	{
		// Dispose internal lists for all nodes.
		for(int i_node = 0; i_node < nodes.Length; i_node++)
		{
			var node = nodes[i_node];
			node.Dispose();
		}
		
		nodes.Dispose();
		build_queue.Dispose();
	}
	
	public void
	Query(float3 pt, ref NativeList<T> results)
	{
		if(nodes.Length == 0) { return; }
		
		// Start at root node.
		Query(pt, 0, ref results);
	}
	
	public void
	Query(float3 pt, int i_node, ref NativeList<T> results)
	{
		var node = nodes[i_node];
		
		if(node.IsLeaf)
		{
			for(int i_elem = 0; i_elem < node.num_elements; i_elem++)
			{
				var elem = UnsafeUtility.ReadArrayElement<NodeElement>((void*)node.elems, i_elem);
				results.Add(elem.elem);
			}

			return;
		}
		
		// Not leaf, need to check children.
		for(int i_child = 0; i_child < 4; i_child++)
		{
			var child_node = nodes[node.i_first_child + i_child];
			
			if(child_node.aabb.Contains(pt))
			{
				// Recursively scan down the tree, until we reach a leaf node.
				Query(pt, node.i_first_child + i_child, ref results);
			}
		}
	}
	
	public void
	Add(T item, AABB2D aabb)
	{
		build_queue.Enqueue(new NodeElement(item, aabb));
	}
	
	public void
	Build()
	{
		while(build_queue.TryDequeue(out var elem))
		{
			Build(0, elem);
		}
		
		build_queue.Clear();
	}
	
	private void
	Build(int i_node, NodeElement elem)
	{
		var node = nodes[i_node];
		
		// Doesn't intersect the total bounding region, quit out.
		if(!elem.aabb.Overlaps(node.aabb)) { return; }
		
		// No room in this node --- split it into four quadrants.
		if(node.IsLeaf && node.num_elements >= max_elems_per_node && node.depth <= max_depth)
		{
			var bounds_min = node.aabb.min;
			var bounds_max = node.aabb.max;
			var half_step  = (bounds_max - bounds_min) / 2f;

			node.i_first_child = nodes.Length;
			
			// We've now made this node a non-leaf node, so we need to propogate all of its data to the children.

			var bl_aabb = new AABB2D
			(
				bounds_min,
				bounds_min + half_step
			);

			var tl_aabb = new AABB2D
			(
				bounds_min + new float2(0f, half_step.y),
				bounds_min + new float2(0f, half_step.y) + half_step
			);

			var tr_aabb = new AABB2D
			(
				bounds_min + half_step,
				bounds_max
			);

			var br_aabb = new AABB2D
			(
				bounds_min + new float2(half_step.x, 0f),
				bounds_min + new float2(half_step.x, 0f) + half_step
			);

			nodes.Add(new Node(node.depth + 1, bl_aabb, allocator));
			nodes.Add(new Node(node.depth + 1, tl_aabb, allocator));
			nodes.Add(new Node(node.depth + 1, tr_aabb, allocator));
			nodes.Add(new Node(node.depth + 1, br_aabb, allocator));
			
			// We need to migrate this node's data to newly created child nodes (where it overlaps)
			for(int i_elem = 0; i_elem < node.num_elements; i_elem++)
			{
				var cpy_elem = UnsafeUtility.ReadArrayElement<NodeElement>((void*)node.elems, i_elem);
				
				if(bl_aabb.Overlaps(cpy_elem.aabb) || bl_aabb.Contains(cpy_elem.aabb))
				{
					Build(node.i_first_child + 0, elem);	
				}
			
				if(tl_aabb.Overlaps(cpy_elem.aabb) || tl_aabb.Contains(cpy_elem.aabb))
				{
					Build(node.i_first_child + 1, elem);
				}
			
				if(tr_aabb.Overlaps(cpy_elem.aabb) || tr_aabb.Contains(cpy_elem.aabb))
				{
					Build(node.i_first_child + 2, elem);
				}
			
				if(br_aabb.Overlaps(cpy_elem.aabb) || br_aabb.Contains(cpy_elem.aabb))
				{
					Build(node.i_first_child + 3, elem);
				}
			}
			
			// Our data has been copied to the child node(s), so we can dispose the original contents.
			node.num_elements = 0;
			node.Dispose();
			
			// Now we can finally insert the intended element.
			// See what nodes the inserted element overlaps.
			if(bl_aabb.Overlaps(elem.aabb) || bl_aabb.Contains(elem.aabb))
			{
				Build(node.i_first_child + 0, elem);	
			}
			
			if(tl_aabb.Overlaps(elem.aabb) || tl_aabb.Contains(elem.aabb))
			{
				Build(node.i_first_child + 1, elem);
			}
			
			if(tr_aabb.Overlaps(elem.aabb) || tr_aabb.Contains(elem.aabb))
			{
				Build(node.i_first_child + 2, elem);
			}
			
			if(br_aabb.Overlaps(elem.aabb) || br_aabb.Contains(elem.aabb))
			{
				Build(node.i_first_child + 3, elem);
			}

			// Update changes to the parent node.
			nodes[i_node] = node;
			
			return;
		}

		// Otherwise, just insert the element into the given node.
		if(node.elems == IntPtr.Zero)
		{
			node.elems = (IntPtr)
				UnsafeUtility.Malloc(UnsafeUtility.SizeOf<NodeElement>(), UnsafeUtility.AlignOf<NodeElement>(), Allocator.Persistent);
		}
		
		UnsafeUtility.WriteArrayElement((void*)node.elems, node.num_elements, elem);	
		
		node.num_elements++;
		nodes[i_node] = node;
	}
}


//====
}
//====
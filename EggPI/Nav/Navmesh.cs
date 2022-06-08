using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using EggPI.Mathematics;


//====
namespace EggPI.Nav
{
//====


[Serializable]
public unsafe struct Navmesh
{	
	public NativeArray<NavmeshNode> nodes;
	public NativeArrayOfLists<int> nodes_in_tile;
	public NativeArray<float3> 	 	verts;

	public  static void* navmeshes;
	private const  int   MAX_ACTIVE_NAVMESHES = 8;
	
	public float tile_size;

	private const float EDGE_TOLERANCE = 0.0001f;

	private float3 neg;
	private float3 neg_clamped;
	private float3 pos;
	private float3 pos_clamped;

	private int3 tile_dims;

	private NativeArray<int> side_lookup_table;
	
	static Navmesh()
	{
		long sz   = UnsafeUtility.SizeOf<Navmesh>() * MAX_ACTIVE_NAVMESHES;
		navmeshes = UnsafeUtility.Malloc(sz, UnsafeUtility.AlignOf<Navmesh>(), Allocator.Persistent);
	}
	
	public static Navmesh
	Get(int i_navmesh)
	{
		return UnsafeUtility.ReadArrayElement<Navmesh>(navmeshes, i_navmesh);
	}
	
	public static Navmesh
	Create(float3 bounds_min, float3 bounds_max, float tile_size, NativeArray<float3> verts, 
		NativeArray<NavmeshNode> nodes, NativeArrayOfLists<int> nodes_in_tile)
	{
		var nm = new Navmesh(bounds_min, bounds_max, tile_size, verts, nodes, nodes_in_tile);
		return nm;
	}
	
	public static Navmesh
	CreateFromUnityMesh(float tile_size, Mesh mesh)
	{
		var nm = new Navmesh(mesh.bounds.min, mesh.bounds.max, tile_size);
		nm.BuildFromUnityMesh(mesh);
		return nm;
	}
	
	public static void
	RegisterWithId(Navmesh nm, int i_navmesh)
	{
		UnsafeUtility.WriteArrayElement(navmeshes, i_navmesh, nm);
	}
	
	public Navmesh(float3 bottom_left, float3 top_right, float tile_size)
	{
		neg = bottom_left;
		pos = top_right;
		neg_clamped = math.floor(bottom_left / tile_size) * tile_size;
		pos_clamped = math.ceil (top_right   / tile_size) * tile_size;

		tile_dims = math.max(new int3(1), (int3)math.ceil((pos - neg) / tile_size) + new int3(1, 0, 1));
		
		nodes 		  = new NativeArray<NavmeshNode>(0, Allocator.Temp);
		nodes_in_tile = new NativeArrayOfLists<int>(tile_dims.x * tile_dims.y * tile_dims.z, Allocator.Persistent);
		verts		  = new NativeArray<float3>(0, Allocator.Temp);

		this.tile_size = tile_size;
		
		side_lookup_table = new NativeArray<int>(64, Allocator.Persistent);
		CacheRaycastLookupTable();
	}
	
	public Navmesh(float3 bottom_left, float3 top_right, float tile_size, NativeArray<float3> verts, NativeArray<NavmeshNode> nodes, 
		NativeArrayOfLists<int> nodes_in_tile)
	{
		neg = bottom_left;
		pos = top_right;
		neg_clamped = math.floor(bottom_left / tile_size) * tile_size;
		pos_clamped = math.ceil (top_right   / tile_size) * tile_size;

		tile_dims = math.max(new int3(1), (int3)math.ceil((pos - neg) / tile_size) + new int3(1, 0, 1));

		this.verts = verts;
		this.nodes = nodes;
		this.nodes_in_tile = nodes_in_tile;
		this.tile_size 	   = tile_size;
		
		side_lookup_table = new NativeArray<int>(64, Allocator.Persistent);
		CacheRaycastLookupTable();
	}
	
	private void 
	CacheRaycastLookupTable()
	{
		// Precompute a side lookup table to optimize raycasts against this mesh.
		NativeArray<Navmesh.NodeSide> side_of_line = new NativeArray<Navmesh.NodeSide>(3, Allocator.Temp);
		
		for(int i_lookup = 0; i_lookup < 64; i_lookup++)
		{
			side_of_line[0] = (Navmesh.NodeSide)((i_lookup >> 0) & 0x3);
			side_of_line[1] = (Navmesh.NodeSide)((i_lookup >> 2) & 0x3);
			side_of_line[2] = (Navmesh.NodeSide)((i_lookup >> 4) & 0x3);

			// Default the value to -1, meaning no intersection.
			side_lookup_table[i_lookup] = -1;
			
			// 3 is an invalid value, so we skip it.
			if(side_of_line[0] == (Navmesh.NodeSide)3 || side_of_line[1] == (Navmesh.NodeSide)3 || side_of_line[2] == (Navmesh.NodeSide)3)
			{
				continue;
			}

			int lowest_num_verts = int.MaxValue;
			
			// Determine which side of the triangle a segment crosses through.
			// This handles cases where the edge passes through a vertex or is colinear to a side of the triangle.
			// In either case, we'll pick the one with the lowest number of vertices.
			for(int i_vert = 0; i_vert < 3; i_vert++)
			{
				bool left_colinear  = side_of_line[i_vert] == Navmesh.NodeSide.COLINEAR;
				bool right_colinear = side_of_line[(i_vert + 1) % 3] == Navmesh.NodeSide.COLINEAR;
				
				if((side_of_line[i_vert] == Navmesh.NodeSide.LEFT || left_colinear) &&
				   (side_of_line[(i_vert + 1) % 3] == Navmesh.NodeSide.RIGHT || right_colinear))
				{
					int num_verts = (left_colinear ? 1 : 0) + (right_colinear ? 1 : 0);
					
					if(num_verts < lowest_num_verts)
					{
						side_lookup_table[i_lookup] = i_vert;
						lowest_num_verts 		    = num_verts;
					}
				}
			}
		}
		
		side_of_line.Dispose();
	}
	
	
	public void
	Dispose()
	{	
		if(nodes.IsCreated)
			nodes.Dispose();
		
		if(nodes_in_tile.IsCreated)
			nodes_in_tile.Dispose();
		
		if(verts.IsCreated)
			verts.Dispose();
		
		if(side_lookup_table.IsCreated)
			side_lookup_table.Dispose();
	}
	
	public void
	SetVerts(NativeArray<float3> verts)
	{
		this.verts = verts;
	}
	
	public int
	GetClosestNodeIdToPt(float3 pt)
	{
		pt = math.clamp(pt, neg, pos); // Don't let the test point be sampled from outside the bounds of the mesh.
		
		int3 tile = (int3)((pt - neg) / tile_size);
		
		int  i_tile = (tile.z * tile_dims.x + tile.x) + (tile.y * (tile.z * tile_dims.x + tile.x));

		float best_dist = float.MaxValue;
		int   i_closest = 0;
		
		int num_nodes = nodes_in_tile.GetListLength(i_tile);
		for(int i_node = 0; i_node < num_nodes; i_node++)
		{
			int i_inner = nodes_in_tile[i_tile, i_node];
			var node    = nodes[i_inner];
			
			if(node.IsPointInBounds(pt))
			{
				// Sample the nodes and point on the xz plane to make distance checking simpler.
				float3 v0 = verts[node.vertex_ids[0]].xnz();
				float3 v1 = verts[node.vertex_ids[1]].xnz();
				float3 v2 = verts[node.vertex_ids[2]].xnz();

				float3 closest_pt = bmath.ClosestPointOnTriangleToPoint(v0, v1, v2, pt.xnz());

				float sqrdist = math.lengthsq(closest_pt - pt);
				
				if(sqrdist < best_dist)
				{
					best_dist = sqrdist;
					i_closest = node.id;
					
					// Node is "close enough," so we can break out early and use it.
					if(sqrdist <= EDGE_TOLERANCE)
					{
						break;
					}
				}
			}
		}
		
		return i_closest;
	}
	
	public int2
	GetTileCoordsXZ(float3 pt)
	{
		int2 tile = (int2)((pt.xz - neg.xz) / tile_size);
		return tile;
	}
	
	public NavmeshNode
	GetClosestNodeToPt(float3 pt)
	{
		return nodes[GetClosestNodeIdToPt(pt)];
	}
	
	public Navmesh.RaycastHit
	Raycast(float3 start_pos, float3 end_pos, int i_node_start = -1)
	{
		// Find initial node, if it isn't passed in.
		if(i_node_start == -1 || i_node_start < 0 || i_node_start > nodes.Length)
		{
			i_node_start = GetClosestNodeIdToPt(start_pos);

			var t_n = nodes[i_node_start];

			var t_v0 = verts[t_n.vertex_ids[0]];
			var t_v1 = verts[t_n.vertex_ids[1]];
			var t_v2 = verts[t_n.vertex_ids[2]];
		}
		
		var node = nodes[i_node_start];

		var res = new Navmesh.RaycastHit(i_node_start, -1, start_pos, end_pos, false);
		
		// Early exit if we don't need to move.
		if(start_pos.Equals(end_pos))
		{
			return res;
		}
		
		// Early exit for starting in an unwalkable node.
		if(!node.IsWalkable)
		{
			res.did_hit = 1;
			return res;
		}
		
		int iters = 0;
		
		// Determine which edge the ray crosses through, if any.
		while(iters < 1024)
		{
			iters++;
			
			float3 v0 = verts[node.vertex_ids[0]];
			float3 v1 = verts[node.vertex_ids[1]];
			float3 v2 = verts[node.vertex_ids[2]];

			int side_of_line  = (int)GetSidePtRelativeToLineXZ(start_pos, end_pos, v0) << 0;
			side_of_line 	 |= (int)GetSidePtRelativeToLineXZ(start_pos, end_pos, v1) << 2;
			side_of_line	 |= (int)GetSidePtRelativeToLineXZ(start_pos, end_pos, v2) << 4;
			
			int i_node_edge = side_lookup_table[side_of_line];

			float3 edge_v0 = i_node_edge == 0 ? v0 : (i_node_edge == 1 ? v1 : v2);
			float3 edge_v1 = i_node_edge == 0 ? v1 : (i_node_edge == 1 ? v2 : v0);

			int exit_side_mask = (int)GetSidePtRelativeToLineXZ(edge_v0, edge_v1, end_pos);
			
			// Ray doesn't exit node.
			if(exit_side_mask != (int)NodeSide.LEFT)
			{			
				res.end_pos    = end_pos;
				res.i_end_node = node.id;
				return res;
			}
			
			// Ray doesn't hit anything --- FPU error?
			if(i_node_edge == -1)
			{
				// Debug.LogError("Ray hit no nodes. This likely due to floating point precision errors; oof.");
				return res;
			}
			
			var i_neighbor = node.edge_neighbor_ids[i_node_edge];
						
			// No connection on this edge.
			if(i_neighbor < 0)
			{
				res.did_hit = 1;
				SegSegIntersectionXZ(start_pos, end_pos, edge_v0, edge_v1, out res.end_pos);
				res.i_end_node = node.id;
				return res;
			}

			NavmeshNode neighbor = nodes[i_neighbor];
				
			if(!neighbor.IsWalkable)
			{
				res.did_hit = 1;
				SegSegIntersectionXZ(start_pos, end_pos, edge_v0, edge_v1, out res.end_pos);
				res.i_end_node = node.id;
				return res;
			}

			node = neighbor;
		}

		throw new Exception("Raycast caught in an endless loop!");
		return res;
	}
	
	public void
	BuildFromUnityMesh(Mesh mesh)
	{
		nodes = BuildTriangles(mesh.GetIndices(0), mesh.vertices);
	}
	
	public int 
	GetNumBytesUsed()
	{
		int num_bytes = 0;

		int float_sz = sizeof(float);
		int int_sz	 = sizeof(int);
		int node_sz  = UnsafeUtility.SizeOf<NavmeshNode>();
		
		// 6 * 4 bytes for the min and max bounds.
		num_bytes += float_sz * 6;
		
		// 4 bytes for the tile size.
		num_bytes += float_sz;
		
		// int_sz for the number of verts.
		num_bytes += int_sz;
		
		// 24 bytes for each vert.
		num_bytes += float_sz * 3 * verts.Length;
		
		// int_sz for the number of nodes.
		num_bytes += int_sz;
		
		// node_sz bytes for each node.
		num_bytes += node_sz * nodes.Length;

		int num_tiles = tile_dims.x * tile_dims.y * tile_dims.z;
		for(int i_tile = 0; i_tile < num_tiles; i_tile++)
		{				
			// Put the # of nodes that belong to this tile.
			int num_nodes  = nodes_in_tile.IsListCreated(i_tile) ? nodes_in_tile.GetListLength(i_tile) : 0;

			// int_sz for the number of nodes.
			num_bytes += int_sz;

			// int_sz for each node in the mapping.
			num_bytes += int_sz * num_nodes;
		}
		
		
		return num_bytes;
	}
	
	public bool
	SaveToDisk(string path)
	{		
		// @TODO: When using PutBytes from the editor, strange errors happen, yet it works perfectly fine from the standalone game.
		// @TODO: Add support to detect when in the editor to iterate vs when in standalone to memcpy.

		int num_bytes = GetNumBytesUsed();

		NativeDataWriter writer = new NativeDataWriter(num_bytes, true, Allocator.Temp);
		
		// Write bounds.
		writer.Put(neg);
		writer.Put(pos);
		
		// Write tile size.
		writer.Put(tile_size);
		
		// Write length of verts.
		writer.Put(verts.Length);

		// Write each vert.
		for(int i_vert = 0; i_vert < verts.Length; i_vert++)
		{
			var vert = verts[i_vert];
			writer.Put(vert);
		}
	
		// Write length of nodes and each node.
		writer.Put(nodes.Length);
		for(int i_node = 0; i_node < nodes.Length; i_node++)
		{
			writer.Put(nodes[i_node]);
		}

		// Write node tile mappings.
		int num_tiles = tile_dims.x * tile_dims.y * tile_dims.z;
		for(int i_tile = 0; i_tile < num_tiles; i_tile++)
		{			
			// Put the # of nodes that belong to this tile.
			int num_nodes = nodes_in_tile.GetListLength(i_tile);
			writer.Put(num_nodes);
			
			for(int i_node = 0; i_node < num_nodes; i_node++)
			{
				writer.Put(nodes_in_tile[i_tile, i_node]);
			}
		}
		
		FileIO.Init();
		
		int res = FileIO.Save(path, writer.buffer, writer.length);
		
		writer.Dispose();
		
		return res == writer.length;
	}
	
	public static Navmesh
	LoadFromDisk(string path)
	{		
		// @TODO: When using GetNativeArray from the editor, strange errors happen, yet it works perfectly fine from the standalone game.
		// @TODO: Add support to detect when in the editor to iterate vs when in standalone to memcpy.
		FileIO.Init();
		
		int len = 0;
		var buf = (byte*)FileIO.Load(path, ref len);
		
		NativeDataReader reader = new NativeDataReader(buf, len);

		float3 neg = reader.GetFloat3();
		float3 pos = reader.GetFloat3();
		float tile_size = reader.GetFloat();
		
		// Read verts.
		int num_verts = reader.GetInt();
		
		var verts = new NativeArray<float3>(num_verts, Allocator.Persistent);
		for(int i_vert = 0; i_vert < num_verts; i_vert++)
		{
			verts[i_vert] = reader.GetFloat3();
		}

		// Read nodes.
		int num_nodes = reader.GetInt();
		var nodes = new NativeArray<NavmeshNode>(num_nodes, Allocator.Persistent);
		for(int i_node = 0; i_node < num_nodes; i_node++)
		{
			nodes[i_node] = reader.GetNavmeshNode();
		}
		
		int3 tile_dims = math.max(new int3(1), (int3)(math.ceil((pos - neg) / tile_size) + new int3(1, 0, 1)));
		int  num_tiles = tile_dims.x * tile_dims.y * tile_dims.z;
		
		// Read tile mappings.
		var nodes_in_tile = new NativeArrayOfLists<int>(num_tiles, Allocator.Persistent);
		for(int i_tile = 0; i_tile < num_tiles; i_tile++)
		{
			int num_tile_nodes = reader.GetInt();
				
			for(int i_node = 0; i_node < num_tile_nodes; i_node++)
			{
				nodes_in_tile.Add(i_tile, reader.GetInt());
			}
		}
		
		Navmesh nm = new Navmesh(neg, pos, tile_size, verts, nodes, nodes_in_tile);

		UnsafeUtility.Free(buf, Allocator.Persistent);
		
		return nm;
	}
	
	private NativeArray<NavmeshNode>
	BuildTriangles(int[] triangles, Vector3[] verts)
	{
		NativeArray<int> tris = new NativeArray<int>(triangles, Allocator.Persistent);
		this.verts.Dispose();
		this.verts = new NativeArray<float3>(verts.Length, Allocator.Persistent);
		
		for(int i_vert = 0; i_vert < verts.Length; i_vert++)
		{
			this.verts[i_vert] = verts[i_vert];
		}
		
		return BuildTriangles(ref tris);
	}
	
	private NativeArray<NavmeshNode>
	BuildTriangles(ref NativeArray<int> indices)
	{
		nodes.Dispose(); // We had to allocate this in the constructor to compile, but we used an invalid allocation with 0 length.
		
		int num_tris  = indices.Length / 3;
		var node_list = new NativeList<NavmeshNode>(num_tris, Allocator.Persistent);
		
		var edges_to_tris = new NativeHashMap<int2, int>(num_tris, Allocator.Persistent);
			
		// Fill array with default (invalid) nodes to be corrected below. Faster to use a single native memcpy operation than a loop.
		var copy_me = stackalloc NavmeshNode[1];
		copy_me[0]  = new NavmeshNode(-1, -1, -1, -1);
		UnsafeUtility.MemCpyReplicate(node_list.GetUnsafePtr(), copy_me, NavmeshNode.GetSizeInBytes(), num_tris);
		
		NativeArrayOfLists<int> edge_nodes_hz = new NativeArrayOfLists<int>(tile_dims.z * (tile_dims.x + 1), Allocator.Temp);
		NativeArrayOfLists<int> edge_nodes_vt = new NativeArrayOfLists<int>((tile_dims.z + 1) * tile_dims.x, Allocator.Temp);
				
		// Add nodes, build edge mappings, and sort vertices (if they are not clockwise sorted).
		for(int i_index = 0; i_index < indices.Length; i_index += 3)
		{
			int i_v0 = indices[(i_index + 0) % indices.Length];
			int i_v1 = indices[(i_index + 1) % indices.Length];
			int i_v2 = indices[(i_index + 2) % indices.Length];

			int i_node = node_list.Length;
			
			var node = CreateNode(edges_to_tris, i_node, ref i_v0, ref i_v1, ref i_v2, out var v0, out var v1, out var v2, out var bounds_min, 
				out var bounds_max, out var i_tile_start, out var i_tile_end);
			
			node_list.Add(node);

			
			// If this node overlaps 2 to 3 tiles in either the x or z axes, we need to add this node to the corresponding edge
			// list of nodes for potential splitting. We make the assumption, based off how the recast algorithm works, that we won't
			// have any triangles that span more than 2 edges in either axis. If we do end up with a larger than 2 edge span, it means that
			// we've erroneously manually edited a navmesh and we should log it for correction.

			int num_edges_spanned_x = math.abs(i_tile_end.x - i_tile_start.x);
			if(num_edges_spanned_x > 2)
			{
				Debug.LogError("Node spans more than 2 tiles in the x axis. This is likely caused by an error in manual navmesh editing. Fix!");
				return node_list;
			}

			int num_edges_spanned_z = math.abs(i_tile_end.z - i_tile_start.z);
			if(num_edges_spanned_z > 2)
			{
				Debug.LogError("Node spans more than 2 tiles in the z axis. This is likely caused by an error in manual navmesh editing. Fix!");
				return node_list;
			}
			
			// 2 or 3 tiles spanned in x direction.
			if(num_edges_spanned_x >= 1)
			{
				int i_edge_x = (i_tile_start.z * (tile_dims.x + 1)) + i_tile_start.x + 1;
				edge_nodes_vt.Add(i_edge_x, i_node);
				
				if(num_edges_spanned_x == 2)
				{
					i_edge_x = (i_tile_start.z * (tile_dims.x + 1)) + i_tile_start.x + 2;
					edge_nodes_vt.Add(i_edge_x, i_node);
				}
			}
			
			// 2 or 3 tiles spanned in z direction.
			if(num_edges_spanned_z >= 1)
			{
				int i_edge_z = (i_tile_start.z * tile_dims.x) + tile_dims.x;
				edge_nodes_hz.Add(i_edge_z, i_node);
				
				if(num_edges_spanned_z == 2)
				{
					i_edge_z = (i_tile_start.z * tile_dims.x) + tile_dims.x;
					edge_nodes_hz.Add(i_edge_z, i_node);
				}
			}
		}
		
		SplitAdjacentTileTriangles(edge_nodes_vt, node_list, edges_to_tris, false /* horizontal */);
		SplitAdjacentTileTriangles(edge_nodes_hz, node_list, edges_to_tris, true  /* vertical   */);
		
		// Determine what edges share triangles.
		int num_shared = 0;
		for(int i_node = 0; i_node < node_list.Length; i_node++)
		{
			NavmeshNode node = node_list[i_node];
			
			for(int i_vert = 0; i_vert < 3; i_vert++)
			{
				int first, second;
				
				first  = node.vertex_ids[i_vert];
				second = node.vertex_ids[(i_vert + 1) % 3];	
					
				if(!edges_to_tris.TryGetValue(new int2(second, first), out var i_other)) { continue; }

				NavmeshNode other = node_list[i_other];
				
				for(int i_other_vert = 0; i_other_vert < 3; i_other_vert++)
				{		
					if(other.vertex_ids[i_other_vert] == second && other.vertex_ids[(i_other_vert + 1) % 3] == first)
					{
						node.edge_neighbor_ids[i_vert] = i_other;
						num_shared++;
					}
				}
			}

			node_list[i_node] = node;
		}
		
		indices.Dispose();
		edges_to_tris.Dispose();
		edge_nodes_hz.Dispose();
		edge_nodes_vt.Dispose();
		// node_list.Dispose();

		return node_list;
	}
	
	private NavmeshNode
	CreateNode(NativeHashMap<int2, int> edges_to_tris, int i_node, ref int i_v0, ref int i_v1, ref int i_v2, 
		out float3 v0, out float3 v1, out float3 v2, out float3 bounds_min, out float3 bounds_max, out int3 i_tile_start, out int3 i_tile_end,
		bool sortcw = true)
	{
		v0 = verts[i_v0];
		v1 = verts[i_v1];
		v2 = verts[i_v2];
			
		if(sortcw) { bmath.SortCWXZ(ref v0, ref v1, ref v2, ref i_v0, ref i_v1, ref i_v2); }

		bounds_min = math.min(v2, math.min(v0, v1));
		bounds_max = math.max(v2, math.max(v0, v1));
		
		// Ensure the node has enough vertical bounding space to make spatial querying a smidge easier.
		float vdiff = math.abs(bounds_max.y - bounds_min.y);
		
		if(vdiff < 0.25f)
		{
			bounds_max.y += 0.125f;
			bounds_min.y -= 0.125f;
		}
			
		NavmeshNode node = new NavmeshNode(i_node, i_v0, i_v1, i_v2)
		{
			bounds_min = bounds_min,
			bounds_max = bounds_max
		};
		
		// Create initial edge mappings for adjacency / neighbor tests later.
		int2 key = new int2(i_v0, i_v1);
		edges_to_tris.TryAdd(key, i_node);
			
		key = new int2(i_v1, i_v2);
		edges_to_tris.TryAdd(key, i_node);

		key = new int2(i_v2, i_v0);
		edges_to_tris.TryAdd(key, i_node);
			
		// "Grow" our bounds by a small amount to make the math easier for when a node sits on the edge of two tiles,
		// as it needs to be included into both tiles for the cases where a query starts on an edge between two tiles.
		bounds_min -= EDGE_TOLERANCE;
		bounds_max += EDGE_TOLERANCE;
			
		// Ensure that our bounds do not extend beyond the actual confines of the area, lest we run into indices out of range.
		bounds_min = math.max(neg, bounds_min);
		bounds_max = math.min(pos, bounds_max);

		// Determine which tile(s) this node belongs to.
		i_tile_start = (int3)math.floor((bounds_min - neg) / tile_size);
		i_tile_end   = (int3)math.floor((bounds_max - neg) / tile_size);
			
		// Assign this node to the tile(s) that it overlaps.
		for(int i_tile_y = i_tile_start.y; i_tile_y <= i_tile_end.y; i_tile_y++)
		for(int i_tile_z = i_tile_start.z; i_tile_z <= i_tile_end.z; i_tile_z++)
		for(int i_tile_x = i_tile_start.x; i_tile_x <= i_tile_end.x; i_tile_x++)
		{			
			var i_tile = (i_tile_x + (tile_dims.x * i_tile_z)) + (i_tile_y * tile_dims.x * tile_dims.z);
			
			nodes_in_tile.Add(i_tile, i_node);
		}

		return node;
	}
	
	private void 
	RemoveEdgeMapping(NativeHashMap<int2, int> edges_to_tris, int i_v0, int i_v1, int i_v2)
	{
		int2 key = new int2(i_v0, i_v1);
		edges_to_tris.Remove(key);
			
		key = new int2(i_v1, i_v2);
		edges_to_tris.Remove(key);

		key = new int2(i_v2, i_v0);
		edges_to_tris.Remove(key);
	}
	
	private void
	SplitAdjacentTileTriangles(NativeArrayOfLists<int> mapping, NativeList<NavmeshNode> node_list, NativeHashMap<int2, int> edge_hashes, 
		bool horizontal)
	{
		// Split triangles that only partially share edges, so that we can properly assign neighbors to them.
		// Start with vertical edges.
		for(int i_edge = 0; i_edge < mapping.Length; i_edge++)
		{
			if(!mapping.IsListCreated(i_edge)) { continue; }
			
			// Go through each triangle assigned to this edge and see if they partially share an edge.
			int num_edge_tris = mapping.GetListLength(i_edge);
						
			int edge_axis = (horizontal) ? i_edge % tile_dims.x : i_edge % (tile_dims.x + 1); 
			
			for(int i_compare_a = 0; i_compare_a < num_edge_tris; i_compare_a++)
			for(int i_compare_b = 0; i_compare_b < num_edge_tris; i_compare_b++)
			{
				var i_node_a = mapping[i_edge, i_compare_a];
				var i_node_b = mapping[i_edge, i_compare_b];
				
				if(i_node_a == i_node_b) { continue; }

				var node_a = node_list[i_node_a];
				var node_b = node_list[i_node_b];
				
//				// If the bounding boxes of the two nodes don't touch, then they're not potential neighbors.
//				if(node_a.bounds_max.x < node_b.bounds_min.x || node_b.bounds_max.x < node_a.bounds_min.x ||
//				   node_a.bounds_max.y < node_b.bounds_min.y || node_b.bounds_max.y < node_a.bounds_min.y ||
//				   node_a.bounds_max.z < node_b.bounds_min.z || node_b.bounds_max.z < node_a.bounds_min.z)
//				{
//					Debug.Log("Not in bounds...");
//					continue;
//				}
				
				// Determine the min and max range of the verts along the edge of node a that lie on the tile edge.
				float  min_axis   = float.MaxValue;
				float  max_axis   = float.MinValue;
				float  max_dist_from_edge = float.MinValue;
				int num_verts_on_tile_edge = 0;
				int i_node_a_apex = -1;
				
				for(int i_vert_a = 0; i_vert_a < 3; i_vert_a++)
				{
					var vert_a = verts[node_a.vertex_ids[i_vert_a]];
					
					// Determine apex by furthest distance from the edge.
					
					float vert_dist_edge = (horizontal) ? math.abs((neg_clamped.z + edge_axis * tile_size) - vert_a.z) : 
														  math.abs((neg_clamped.x + edge_axis * tile_size) - vert_a.x);
					
					if(vert_dist_edge > max_dist_from_edge)
					{
						max_dist_from_edge = vert_dist_edge;
						i_node_a_apex = i_vert_a;
					}

					// Skip below if vert does not lie on edge.
					if(vert_dist_edge > EDGE_TOLERANCE) { continue; }

					float cmp = (horizontal) ? vert_a.x : vert_a.z;
					
					min_axis = math.min(cmp, min_axis);
					max_axis = math.max(cmp, max_axis);
					num_verts_on_tile_edge++;
				}
				
				// Nodes that only touch the tile edge with one vertex aren't split, so we can skip this iteration early.
				if(num_verts_on_tile_edge < 2 || i_node_a_apex == -1)
				{							
					continue;
				}
				
				// If any of the verts of node b lie *between* the min and max z values of node_a's verts that lie along the tile edge,
				// We need to split node a into 2 new nodes.
				for(int i_vert_b = 0; i_vert_b < 3; i_vert_b++)
				{
					var i_node_b_v0 = node_b.vertex_ids[i_vert_b];
					var vert_b 		= verts[i_node_b_v0];
					
					float vert_dist_edge = (horizontal) ? math.abs((neg_clamped.z + edge_axis * tile_size) - vert_b.z) :
														  math.abs((neg_clamped.x + edge_axis * tile_size) - vert_b.x);
					
					if(vert_dist_edge > EDGE_TOLERANCE) { continue; }
					
					// Vert lies between bounding range, split node_a into two nodes.
					float cmp = (horizontal) ? vert_b.x : vert_b.z;
					if(cmp > min_axis && cmp < max_axis)
					{
						var i_nv0 = node_a.vertex_ids[0];
						var i_nv1 = node_a.vertex_ids[1];
						var i_nv2 = node_a.vertex_ids[2];
						
						// Remove the original triangle from the edge mappings, since it'll be split.
						RemoveEdgeMapping(edge_hashes, i_nv0, i_nv1, i_nv2);

						i_nv0 = node_a.vertex_ids[(i_node_a_apex + 1) % 3];
						i_nv1 = node_b.vertex_ids[i_vert_b];
						i_nv2 = node_a.vertex_ids[i_node_a_apex];
						
						var newnode_a =
							CreateNode(edge_hashes, i_node_a, ref i_nv0, ref i_nv1, ref i_nv2, out _, out _, out _, out _, out _, out _, 
							out _);

						i_nv0 = node_a.vertex_ids[(i_node_a_apex + 2) % 3];
						i_nv1 = node_b.vertex_ids[i_vert_b];
						i_nv2 = node_a.vertex_ids[i_node_a_apex];
						
						var newnode_b =
							CreateNode(edge_hashes, node_list.Length, ref i_nv0, ref i_nv1, ref i_nv2, out var v0, out var v1, out var v2, out _, out _, 
							out _, out _);
					
						// Add new nodes and update what tiles they belong to. We can infer the tile id from the edge id.
						node_list[i_node_a] = newnode_a;
						node_list.Add(newnode_b);
					}
				}
			}
		}
	}
	
	public static bool
	IsPointInNodeBounds(float3 test_pt, NavmeshNode node)
	{
		return test_pt.x >= node.bounds_min.x && test_pt.x <= node.bounds_max.x &&
		       test_pt.y >= node.bounds_min.y && test_pt.y <= node.bounds_max.y &&
		       test_pt.z >= node.bounds_min.z && test_pt.z <= node.bounds_max.z;
	}
	
	public static NodeSide
	GetSidePtRelativeToLineXZ(float3 a, float3 b, float3 pt)
	{
		double s = ((b.x - a.x) * (double)(pt.z - a.z)) - ((pt.x - a.x) * (double)(b.z - a.z));

		return s > 0 ? NodeSide.LEFT : ((s < 0) ? NodeSide.RIGHT : NodeSide.COLINEAR);
	}
	
	public static bool
	SegSegIntersectionXZ(float3 a0, float3 a1, float3 b0, float3 b1, out float3 pt)
	{
		// NOTE: This makes the assumption that the lines will meet, since they form a triangle.
		// 		 Thus, we won't check for a lot of edge cases, such as colinearity.
		
		float3 dir_seg_0 = a1 - a0;
		float3 dir_seg_1 = b1 - b0;

		float denom = (dir_seg_1.x * dir_seg_0.z) - (dir_seg_1.z * dir_seg_0.x); // 2D perp product.
		
		// Segments are parallel (or close to it).
		if(math.abs(denom) <= math.FLT_MIN_NORMAL)
		{
			pt = a0;
			return false;
		}

		float3 w 	 = a0 - b0;
		float  numer = (dir_seg_1.z * w.x) - (dir_seg_1.x * w.z);
		float  t     = numer / denom;

		pt = a0 + (t * dir_seg_0);
		
		return true;
	}
	
	public struct RaycastRequest
	{
		public int	   i_navmesh;
		public int     i_node_start;
		public float3  start_pos;
		public float3  end_pos;
		
		public RaycastRequest(int i_navmesh, int i_node_start, float3 start_pos, float3 end_pos)
		{
			this.i_navmesh	  = i_navmesh;
			this.i_node_start = i_node_start;
			this.start_pos 	  = start_pos;
			this.end_pos 	  = end_pos;
		}
	}
	
	public struct RaycastHit
	{
		public int    i_start_node;
		public int    i_end_node;
		public float3 start_pos;
		public float3 end_pos;
		public int    did_hit;
		
		public RaycastHit(int i_start_node, int i_end_node, float3 start_pos, float3 end_pos, bool did_hit)
		{
			this.i_start_node = i_start_node;
			this.i_end_node   = i_end_node;
			this.start_pos 	  = start_pos;
			this.end_pos 	  = end_pos;
			this.did_hit 	  = did_hit ? 1 : 0;
		}
	}
	
	public enum NodeSide
	{
		COLINEAR = 0,
		LEFT	 = 1,
		RIGHT	 = 2
	}
}


//====
}
//====

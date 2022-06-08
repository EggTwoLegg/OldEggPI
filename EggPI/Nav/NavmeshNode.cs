using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Object = System.Object;

using EggPI.Mathematics;


//====
namespace EggPI.Nav
{
//====


[StructLayout(LayoutKind.Sequential)]
public unsafe struct NavmeshNode
{
	public int 		  id;
	public int 		  is_walkable;
	public float3 	  bounds_min, bounds_max;
	public IntIndexer vertex_ids;
	public IntIndexer edge_neighbor_ids;

	public bool IsWalkable => is_walkable == 1;
	
	public NavmeshNode(int id, int i_v0, int i_v1, int i_v2, int i_e0_neighbor = -1, int i_e1_neighbor = -1, int i_e2_neighbor = -1)
	{					
		this.id   = id;
		
		vertex_ids 		  = new IntIndexer(i_v0, i_v1, i_v2);
		edge_neighbor_ids = new IntIndexer(i_e0_neighbor, i_e1_neighbor, i_e2_neighbor);

		bounds_min = bounds_max = new float3(0f, 0f, 0f);

		is_walkable = 1;
	}
	
	public int3
	GetPosition(NativeArray<float3> verts)
	{
		return new Float3Int3Union((verts[vertex_ids[0]] + verts[vertex_ids[1]] + verts[vertex_ids[2]]) * 0.333333f).int3_val;
	}
	
	public bool
	IsPointInBounds(float3 point)
	{
		return point.x >= bounds_min.x && point.x <= bounds_max.x &&
		       point.y >= bounds_min.y && point.y <= bounds_max.y &&
		       point.z >= bounds_min.z && point.z <= bounds_max.z;
	}
	
	public bool
	IsPointInBoundsXZ(float3 point)
	{
		return point.x >= bounds_min.x && point.x <= bounds_max.x &&
		       point.z >= bounds_min.z && point.z <= bounds_max.z;
	}
	
	public static int 
	GetSizeInBytes()
	{
		return sizeof(int) * 7;
	}
	
	public struct IntIndexer
	{
		private int i0, i1, i2;
		
		public int this[int i]
		{
			get
			{
				switch(i)
				{
					case 1:
						return i1;
					case 2:
						return i2;
					default:
						return i0;
				}
			}
			
			set
			{
				switch(i)
				{
					case 1:
					{
						i1 = value;
						break;
					}
					case 2:
					{
						i2 = value;
						break;
					}
					default:
					{
						i0 = value;
						break;
					}
				}
			}
		}
		
		public IntIndexer(int i0, int i1, int i2)
		{
			this.i0 = i0;
			this.i1 = i1;
			this.i2 = i2;
		}
	}
}
	
public struct NavmeshVertex
{
	public int i_vert;
	
	public NavmeshVertex(int i_vert)
	{
		this.i_vert = i_vert;
	}
	
	public override int 
	GetHashCode()
	{
		return i_vert;
	}
}
	
public struct NavmeshNodeEdge : IEquatable<NavmeshNodeEdge>
{
	public float3 v0, v1;
	
	public NavmeshNodeEdge(float3 v0, float3 v1)
	{
		this.v0 = v0;
		this.v1 = v1;
	}

	// https://stackoverflow.com/questions/5928725/hashing-2d-3d-and-nd-vectors
	public override int 
	GetHashCode()
	{
		Float3Int3Union union = default(Float3Int3Union);
		union.float3_val = v0;
		int h0 = (int)math.hash(union.int3_val) % 83492791;

		union.float3_val = v1;
		int h1 = (int)math.hash(union.int3_val) % 73856093;

		return (h0 + h1);
	}

	public bool 
	Equals(NavmeshNodeEdge other)
	{
		return this.v0.Equals(other.v0) && this.v1.Equals(other.v1);
	}
}
	
public struct int2ForNavmeshEdge : IEquatable<int2ForNavmeshEdge>
{
	public int x;
	public int y;

	public int2ForNavmeshEdge (int x, int y) {
		this.x = x;
		this.y = y;
	}

	public long sqrMagnitudeLong {
		get {
			return (long)x*(long)x+(long)y*(long)y;
		}
	}

	public static int2ForNavmeshEdge operator + (int2ForNavmeshEdge a, int2ForNavmeshEdge b) {
		return new int2ForNavmeshEdge(a.x+b.x, a.y+b.y);
	}

	public static int2ForNavmeshEdge operator - (int2ForNavmeshEdge a, int2ForNavmeshEdge b) {
		return new int2ForNavmeshEdge(a.x-b.x, a.y-b.y);
	}

	public static bool operator == (int2ForNavmeshEdge a, int2ForNavmeshEdge b) {
		return a.x == b.x && a.y == b.y;
	}

	public static bool operator != (int2ForNavmeshEdge a, int2ForNavmeshEdge b) {
		return a.x != b.x || a.y != b.y;
	}

	/** Dot product of the two coordinates */
	public static long DotLong (int2ForNavmeshEdge a, int2ForNavmeshEdge b) {
		return (long)a.x*(long)b.x + (long)a.y*(long)b.y;
	}

	public override bool Equals (Object o) {
		if (o == null) return false;
		var rhs = (int2ForNavmeshEdge)o;

		return x == rhs.x && y == rhs.y;
	}

	#region IEquatable implementation

	public bool Equals (int2ForNavmeshEdge other) {
		return x == other.x && y == other.y;
	}

	#endregion

	public override int GetHashCode () {
		return x*49157+y*98317;
	}
}


//====
}
//====
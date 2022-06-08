using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI
{
//====


public struct AABB
{
	public float3 min, max;
	public float  surface_area;
	
	public AABB(float3 min, float3 max)
	{
		this.min = min;
		this.max = max;

		// 2 * (Width * Height) + (Width * Depth) + (Height * Depth)
		surface_area = 2.0f * ((max.x - min.x) * (max.y - min.y) + ((max.x - min.x) * (max.z - min.z)) +
		                      ((max.y - min.y) * (max.z - min.z)));
	}
	
	public float
	GetVolume()
	{
		var dim = max - min;
		return dim.x * dim.y * dim.z;
	}
	
	public bool
	Overlaps(AABB other)
	{
		return !(
			max.y < other.min.y && min.y > other.max.y &&
			max.x < other.min.x && min.x > other.max.x &&
			max.z < other.min.z && min.z > other.max.z
		);
	}
	
	public bool
	Contains(AABB other)
	{
		return min.y <= other.min.y &&
		       max.y >= other.max.y &&
		       min.x <= other.min.x &&
		       max.x >= other.max.x &&
		       min.z <= other.min.z &&
		       max.z >= other.max.z;
	}
	
	public bool
	Contains(float3 pt)
	{
		return pt.x >= min.x &&
		       pt.x <= max.x &&
		       pt.y >= min.y &&
		       pt.y <= max.y &&
		       pt.z >= min.z &&
		       pt.z <= max.z;
	}
	
	public AABB
	Merge(ref AABB other)
	{
		return new AABB(math.min(min, other.min), math.max(max, other.max));
	}
}
	
public struct AABB2D
{
	public float2 min, max;
	public float  surface_area;
	
	public AABB2D(float2 min, float2 max)
	{
		this.min = min;
		this.max = max;

		// 2 * (Width * Height) + (Width * Depth) + (Height * Depth)
		surface_area = (max.x - min.x) * (max.y - min.y);
	}
	
	public float
	GetVolume()
	{
		var dim = max - min;
		return dim.x * dim.y;
	}
	
	public bool
	Overlaps(AABB2D other)
	{
		return !(
			max.y < other.min.y && min.y > other.max.y &&
			max.x < other.min.x && min.x > other.max.x
		);
	}
	
	public bool
	Contains(AABB2D other)
	{
		return min.y <= other.min.y &&
		       max.y >= other.max.y &&
		       min.x <= other.min.x &&
		       max.x >= other.max.x;
	}
	
	public bool
	Contains(float3 pt)
	{
		return pt.x >= min.x &&
		       pt.x <= max.x &&
		       pt.z >= min.y &&
		       pt.z <= max.y;
	}
	
	public AABB2D
	Merge(ref AABB2D other)
	{
		return new AABB2D(math.min(min, other.min), math.max(max, other.max));
	}
}
	
public struct AABBTreeNode
{
	public AABB aabb;
	
	public int i_parent;
	public int i_left_child;
	public int i_right_child;
	
	public AABBTreeNode(AABB aabb, int i_parent, int i_left_child, int i_right_child)
	{
		this.aabb = aabb;
		this.i_parent 	   = i_parent;
		this.i_left_child  = i_left_child;
		this.i_right_child = i_right_child;
	}
}
	
public struct AABBTree
{
	
}


//====
}
//====
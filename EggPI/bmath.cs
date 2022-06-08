using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI.Mathematics
{
//====

	
public static class bmath
{
	public const float KINDA_SMALL_NUMBER = 1e-4f;
	public const float VERY_SMALL_NUMBER  = 1e-10f;

	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool
	RayPlaneIntersection(float3 ray_origin, float3 ray_dir, float3 point_on_plane, float3 plane_normal, out float3 point_of_intersection)
	{
		point_of_intersection = new float3(0f);
			
		float denom = math.dot(ray_dir, plane_normal);
		
		if(math.abs(denom) < KINDA_SMALL_NUMBER) // Parallel or almost parallel.
		{
			return false;
		}
		
		float numer = math.dot(point_on_plane - ray_origin, plane_normal);

		float t = numer / denom;
		point_of_intersection = t * ray_dir + ray_origin;
		
		return true;
	}
	

	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xxn(this float3 v)
	{
		return new float3(v.x, v.x, 0f);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xyn(this float3 v)
	{
		return new float3(v.x, v.y, 0f);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xzn(this float3 v)
	{
		return new float3(v.x, v.z, 0f);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xnn(this float3 v)
	{
		return new float3(v.x, 0f, 0f);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xnx(this float3 v)
	{
		return new float3(v.x, 0f, v.x);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xny(this float2 v)
	{
		return new float3(v.x, 0f, v.y);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xny(this float3 v)
	{
		return new float3(v.x, 0f, v.y);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xnz(this float3 v)
	{
		return new float3(v.x, 0f, v.z);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 xnz(this float2 v)
	{
		return new float3(v.x, 0f, v.y);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 nyz(this float3 v)
	{
		return new float3(0f, v.y, v.z);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 nyn(this float3 v)
	{
		return new float3(0f, v.y, 0f);
	}
	
	[EditorBrowsable(EditorBrowsableState.Never)]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 nnz(this float3 v)
	{
		return new float3(0f, 0f, v.z);
	}
	
	// Calculate the closest point of approach for line-segment vs line-segment.
	public static bool 
	SegmentSegmentCPA (out float3 c0, out float3 c1, float3 p0, float3 p1, float3 q0, float3 q1)
	{
		var u = p1 - p0;
		var v = q1 - q0;
		var w0 = p0 - q0;

		float a = math.dot (u, u);
		float b = math.dot (u, v);
		float c = math.dot (v, v);
		float d = math.dot (u, w0);
		float e = math.dot (v, w0);

		float den = (a * c - b * b);
		float sc, tc;

		if (den == 0)
		{
			sc = 0;
			tc = d / b;

			// todo: handle b = 0 (=> a and/or c is 0)
		}
		else
		{
			sc = (b * e - c * d) / (a * c - b * b);
			tc = (a * e - b * d) / (a * c - b * b);
		}

		c0 = math.lerp (p0, p1, sc);
		c1 = math.lerp (q0, q1, tc);

		return den != 0;
	}
	
	public static bool
	IsCWXZ(float3 a, float3 b)
	{
		return ((a.x * b.x) * (a.z * b.z)) - ((a.z * b.x) * (a.x * b.z)) < 0f;
	}
	
	public static bool
	IsCWOrColinearXZ(float3 a, float3 b)
	{
		return ((a.x * b.x) * (a.z * b.z)) - ((a.z * b.x) * (a.x * b.z)) <= 0f;
	}
	
	public static bool
	IsColinearXZ(float3 a, float3 b)
	{
		return math.abs(((a.x * b.x) * (a.z * b.z)) - ((a.z * b.x) * (a.x * b.z))) < math.FLT_MIN_NORMAL;
	}
	
	public static bool
	IsCCWXZ(float3 a, float3 b)
	{
		return ((a.x * b.x) * (a.z * b.z)) - ((a.z * b.x) * (a.x * b.z)) > 0f;
	}
	
	public static bool
	IsCWXZ(float3 a, float3 b, float3 c)
	{
		return (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z) < 0f;
	}
	
	public static bool
	IsCWOrColinearXZ(float3 a, float3 b, float3 c)
	{
		return math.abs(((b.x - a.x) * (c.z - a.z)) - ((c.x - a.x) * (b.z - a.z))) <= math.FLT_MIN_NORMAL;
	}
	
	public static bool
	IsCCWXZ(float3 a, float3 b, float3 c)
	{
		return ((b.x - a.x) * (c.z - a.z)) - ((c.x - a.x) * (b.z - a.z)) > 0f;
	}
	
	public static void
	SortCWXZ(ref float3 v0, ref float3 v1, ref float3 v2)
	{
		if(!IsCWXZ(v0, v1, v2))
		{
			var t = v0;
			v0 	  = v2;
			v2 	  = t;
		}
	}
	
	public static void
	SortCWXZ(ref float3 v0, ref float3 v1, ref float3 v2, ref int i_v0, ref int i_v1, ref int i_v2)
	{		
		if(!IsCWXZ(v0, v1, v2))
		{			
			var t = v0;
			v0 	  = v2;
			v2 	  = t;

			var i_t = i_v0;
			i_v0 	= i_v2;
			i_v2 	= i_t;
		}
	}
	
	public static float3
	BarycentricCoordsTriangle(float3 t0, float3 t1, float3 t2, float3 pt)
	{
		// Start with the equation:
		// | v * v0 + w * v1 | = v2 
		//
		// v0 is the vector from t0 to t1.
		// v1 is the vector from t0 to t2.
		// v2 is the vector from t0 to the test point pt.
		//
		// Dot the above equation twice by v0 and v1:
		// dot(v * v0 + w * v1, v0) = dot(v2, v0)
		// dot(v * v0 + w * v1, v1) = dot(v2, v1)
		//
		// The dot product is a linear operator, so:
		// dot(v * v0, v0) + dot(w * v1, v0) = dot(v2, v0)
		// dot(v * v0, v1) + dot(w * v1, v1) = dot(v2, v1)
		//
		// This can be turned into a 2x2 matrix:
		// | dot(v * v0, v0)   dot(w * v1, v0) | = dot(v2, v0)
		// | dot(v * v0, v1)   dot(w * v1, v1) | = dot(v2, v1)
		//
		// Which can become a 2x2 matrix multiplied by a column matrix:
		// | dot(v0, v0)   dot(v1, v0) | | v | = dot(v2, v0)
		// | dot(v0, v1)   dot(v1, v1) | | w | = dot(v2, v1)
		//
		// We can then use Cramer's rule to solve for v and w. Then use u = 1 - v - w.
		
		float3 v0 = t1 - t0;
		float3 v1 = t2 - t0;
		float3 v2 = pt - t0;

		float d00 = math.dot(v0, v0);
		float d01 = math.dot(v0, v1);
		float d11 = math.dot(v1, v1);
		float d20 = math.dot(v0, v2);
		float d21 = math.dot(v1, v2);

		float denom = d00 * d11 - d01 * d01;

		// Use cramer's rule to solve v and w for this system of linear equations.
		float v = (d20 * d11 - d01 * d21) / denom;
		float w = (d00 * d21 - d20 * d01) / denom;
		float u = 1f - v - w;

		return new float3(u, v, w);
	}
	
	// Refer to 'Real Time Collision Detection' by Christer Ericson, chapter 5.1, page 141.
	public static float3
	ClosestPointOnTriangleToPoint(float3 a, float3 b, float3 c, float3 pt)
	{
		// Check if p is in vertex region outside A
		var ab = b - a;
		var ac = c - a;
		var ap = pt - a;

		var d1 = math.dot(ab, ap);
		var d2 = math.dot(ac, ap);

		float u, v, w;

		// Barycentric coordinates (1,0,0)
		if (d1 <= 0 && d2 <= 0) { return a; }

		// Check if p is in vertex region outside B
		var bp = pt - b;
		var d3 = math.dot(ab, bp);
		var d4 = math.dot(ac, bp);

		// Barycentric coordinates (0,1,0)
		if (d3 >= 0 && d4 <= d3) { return b; }

		// Check if p is in edge region outside AB, if so return a projection of p onto AB
		var vc = (d1 * d4) - (d3 * d2);
		if (vc <= 0 && d1 >= 0 && d3 <= 0)
		{
			// Barycentric coordinates (1-v, v, 0)
			v = d1 / (d1 - d3);
			return a + (ab * v);
		}

		// Check if p is in vertex region outside C
		var cp = pt - c;
		var d5 = math.dot(ab, cp);
		var d6 = math.dot(ac, cp);

		// Barycentric coordinates (0,0,1)
		if (d6 >= 0 && d5 <= d6) { return c; }

		// Check if p is in edge region of AC, if so return a projection of p onto AC
		var vb = (d5 * d2) - (d1 * d6);
		if (vb <= 0 && d2 >= 0 && d6 <= 0) 
		{
			// Barycentric coordinates (1-v, 0, v)
			v = d2 / (d2 - d6);
			return a + (ac * v);
		}

		// Check if p is in edge region of BC, if so return projection of p onto BC
		var va = (d3 * d6) - (d5 * d4);
		if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) 
		{
			v = (d4 - d3) / ((d4 - d3) + (d5 - d6));
			return b + (c - b) * v;
		}

		// Pt is inside face region.
		float denom = 1.0f / (va + vb + vc);
		v = vb * denom;
		w = vc * denom;
		u = 1f - v - w;
		
		return a + ab * v + ac * w;
	}
	
	// Refer to 'Real Time Collision Detection' by Christer Ericson, chapter 5.1, page 141.
	public static float3
	ClosestPointOnTriangleToPoint(float3 a, float3 b, float3 c, float3 pt, out float3 barycentric_coords)
	{
		// Check if p is in vertex region outside A
		var ab = b - a;
		var ac = c - a;
		var ap = pt - a;

		var d1 = math.dot(ab, ap);
		var d2 = math.dot(ac, ap);

		float u, v, w;

		// Barycentric coordinates (1,0,0)
		if (d1 <= 0 && d2 <= 0)
		{
			barycentric_coords = new float3(1f, 0f, 0f);
			return a;
		}

		// Check if p is in vertex region outside B
		var bp = pt - b;
		var d3 = math.dot(ab, bp);
		var d4 = math.dot(ac, bp);

		// Barycentric coordinates (0,1,0)
		if (d3 >= 0 && d4 <= d3)
		{
			barycentric_coords = new float3(0f, 1f, 0f);
			return b;
		}

		// Check if p is in edge region outside AB, if so return a projection of p onto AB
		var vc = (d1 * d4) - (d3 * d2);
		if (vc <= 0 && d1 >= 0 && d3 <= 0)
		{
			// Barycentric coordinates (1-v, v, 0)
			v = d1 / (d1 - d3);
			
			barycentric_coords = new float3(1f - v, v, 0f);
			
			return a + (ab * v);
		}

		// Check if p is in vertex region outside C
		var cp = pt - c;
		var d5 = math.dot(ab, cp);
		var d6 = math.dot(ac, cp);

		// Barycentric coordinates (0,0,1)
		if (d6 >= 0 && d5 <= d6)
		{
			barycentric_coords = new float3(0f, 0f, 1f);
			return c;
		}

		// Check if p is in edge region of AC, if so return a projection of p onto AC
		var vb = (d5 * d2) - (d1 * d6);
		if (vb <= 0 && d2 >= 0 && d6 <= 0) 
		{
			// Barycentric coordinates (1-v, 0, v)
			v = d2 / (d2 - d6);

			barycentric_coords = new float3(1f - v, 0f, v);
			
			return a + (ac * v);
		}

		// Check if p is in edge region of BC, if so return projection of p onto BC
		var va = (d3 * d6) - (d5 * d4);
		if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) 
		{
			v = (d4 - d3) / ((d4 - d3) + (d5 - d6));

			barycentric_coords = new float3(0f, 1f - v, v);
			
			return b + (c - b) * v;
		
		}
		
		// Pt is inside face region.
		float denom = 1.0f / (va + vb + vc);
		v = vb * denom;
		w = vc * denom;
		u = 1f - v - w;

		barycentric_coords = new float3(u, v, w);

		return a + ab * v + ac * w;
	}
	
	public static void
	InsertionSort(this NativeList<int> list)
	{
		var len = list.Length;
		for(int i_list = 0; i_list < len - 1; i_list++)
		{
			for(int i_shift = i_list + 1; i_shift > 0; i_shift--)
			{
				if(list[i_shift - 1] > list[i_shift])
				{
					int t = list[i_shift - 1];
					list[i_shift - 1] = list[i_shift];
					list[i_shift] = t;
				}
			}
			
		}
	}
}
	
[StructLayout(LayoutKind.Explicit)]
public struct FloatIntUnion
{
	[FieldOffset(0)]
	public int int_val;

	[FieldOffset(0)]
	public float float_val;
}
	
[StructLayout(LayoutKind.Explicit)]
public struct Float3Int3Union
{
	[FieldOffset(0)]
	public int3 int3_val;

	[FieldOffset(0)]
	public float3 float3_val;
	
	public Float3Int3Union(float3 float3_val)
	{
		this.int3_val   = 0; // Redundant, but necessary to compile. This *must* be assigned first for this constructor.
		this.float3_val = float3_val;
	}
	
	public Float3Int3Union(int3 int3_val)
	{
		this.float3_val = 0; // Redundant, but necessary to compile. This *must* be assigned first for this constructor.
		this.int3_val 	= int3_val;
	}
}
	

//====
}
//====
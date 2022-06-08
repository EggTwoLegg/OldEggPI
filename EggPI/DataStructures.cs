using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI.Common
{
//====
	
	
public struct Delta
{
	public float3 dir;
	public float  len;

	public Delta(float3 dir)
	{
		len 	 = math.length(dir);
		this.dir = math.normalizesafe(dir);
	}

	public Delta(float3 dir, float magnitude)
	{
		this.dir = math.normalizesafe(dir);
		len 	 = magnitude;
	}
	
	public Delta(float3 dir, float magnitude, bool no_normalize)
	{
		this.dir = dir;
		len 	 = magnitude;
	}

	public float3 
	AsVector()
	{
		return dir * len;
	}

	public void 
	Reverse()
	{
		dir *= -1f;
	}

	public void 
	Zero()
	{
		dir = new float3();
		len = 0f;
	}
	
	public static Delta 
	operator *(Delta delta, float val)
	{
		Delta new_delta = delta;
		
		if(val < 0)
		{
			new_delta.dir *= -1f;
			new_delta.len *= -val;
			return new_delta;
		}

		new_delta.len *= val;
		return new_delta;
	}
}
	
public readonly struct HitData
{
	public readonly float 	 time;
	public readonly Collider collider;
	public readonly float3 	 impact_point;
	public readonly float3   col_point;
	public readonly float3	 normal;
	public readonly float 	 dist;
	public readonly bool 	 valid_hit;
	public readonly bool 	 initial_overlap;
	public readonly bool 	 stuck_in_penetration;
	
	public HitData(float time, Collider collider, float3 impact_point, float3 col_point, float3 normal, float dist, bool valid_hit, 
		bool initial_overlap, bool stuck_in_penetration)
	{
		this.time 				  = time;
		this.collider 			  = collider;
		this.impact_point 		  = impact_point;
		this.col_point			  = col_point;
		this.normal 			  = normal;
		this.dist 				  = dist;
		this.valid_hit 			  = valid_hit;
		this.initial_overlap 	  = initial_overlap;
		this.stuck_in_penetration = stuck_in_penetration;
	}
	
	public static HitData
	Zero()
	{
		return new HitData(0f, null, new float3(0f), new float3(0f), new float3(0f), 0f, false, false, false);
	}
}
	
//====
}
//====
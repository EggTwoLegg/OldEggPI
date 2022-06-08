using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

using EggPI.Mathematics;


//====
namespace EggPI.Collision
{
//====


public struct CMP_CollisionWorldSettings : IComponentData
{
	public float3 min, max;
	public float  partition_size;
	
	public CMP_CollisionWorldSettings(float3 min, float3 max, float partition_size)
	{
		this.min = min;
		this.max = max;
		this.partition_size = partition_size;
	}
}
public class CMP_CollisionWorldBounds_Wrapper : ComponentDataWrapper<CMP_CollisionWorldSettings> {}

public struct CMP_CapsuleShape : IComponentData
{
	public float  radius;
	public float  length;
	public float3 up_vec;
	
	public CMP_CapsuleShape(float radius, float length)
	{
		this.radius = radius;
		this.length = length;
		up_vec = math.up();
	}
	
	public CMP_CapsuleShape(float radius, float length, quaternion rotation)
	{
		this.radius = radius;
		this.length = length;
		up_vec 		= math.rotate(rotation, math.up());
	}
	
	public float3
	Support(float3 pos, float3 dir, float epsilon = 0f)
	{
		float3 supvec = float3.zero;
		float  maxdot = float.MinValue;

		float rad = radius + epsilon;

		float cap_halflen = length * 0.5f;

		dir = math.normalizesafe(dir);
		
		// Upper half of capsule.
		{
			float3 projpos = up_vec * (cap_halflen - rad);

			float dot = math.dot(dir * (cap_halflen - rad), projpos);
			supvec = projpos * dot + dir * rad;
		}
		
		return supvec + pos;
	}
	
	public float3
	GetTopSphereCenter(float3 ref_pos)
	{
		var halflen = length * 0.5f;
		return new float3(ref_pos.x, ref_pos.y + halflen - radius, ref_pos.z);
	}
	
	public float3
	GetBottomSphereCenter(float3 ref_pos)
	{
		var halflen = length * 0.5f;
		return new float3(ref_pos.x, ref_pos.y - halflen + radius, ref_pos.z);
	}
	
	public float3
	GetAbsoluteTop(float3 ref_pos)
	{
		var halflen = length * 0.5f;
		return new float3(ref_pos.x, ref_pos.y + halflen, 0f);
	}
	
	public float3
	GetAbsoluteBottom(float3 ref_pos)
	{
		var halflen = length * 0.5f;
		return new float3(ref_pos.x, ref_pos.y - halflen, 0f);
	}
	
	public bool
	IsPointWithinXOfSurace(float3 cap_pos, float3 point, float x)
	{
		float  xz_sqr_dist = math.lengthsq(point.xz - cap_pos.xz);
		float  inner_bounds = (radius - x - bmath.KINDA_SMALL_NUMBER) * (radius - x - bmath.KINDA_SMALL_NUMBER);
		float  outer_bounds = (radius + x + bmath.KINDA_SMALL_NUMBER) * (radius + x + bmath.KINDA_SMALL_NUMBER);
		return xz_sqr_dist >= inner_bounds && xz_sqr_dist <= outer_bounds;
	}
}
	
public struct CMP_SphereShape : IComponentData
{
	public float radius;
	
	public CMP_SphereShape(float radius)
	{
		this.radius = radius;
	}
	
	public float3
	Support(float3 pos, float3 dir)
	{
		return pos + radius * math.normalizesafe(dir);
	}
}
	
public struct CMP_BoxShape : IComponentData
{
	public float3 half_extents;
	
	public CMP_BoxShape(float3 half_extents)
	{
		this.half_extents = half_extents;
	}
	
	public float3
	Support(float3 pos, float3 dir)
	{
		float3 min  = pos - half_extents;
		float3 max  = pos + half_extents;
		float3 step = max - min;

		float  maxdot = float.MinValue;
		float3 sup	  = min;

		FindSupportPoint(ref maxdot, ref sup, min, 			 	dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.nnz(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.xnz(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.xnn(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.nyn(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.nyz(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step, 		dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.xyn(), dir);

		return sup + pos;
	}
	
	public float3
	Support(float3 pos, quaternion rot, float3 dir)
	{
		float3 min  = math.rotate(rot, pos - half_extents);
		float3 max  = math.rotate(rot, pos + half_extents);
		float3 step = max - min;

		float  maxdot = float.MinValue;
		float3 sup	  = min;

		FindSupportPoint(ref maxdot, ref sup, min, 			 	dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.nnz(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.xnz(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.xnn(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.nyn(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.nyz(), dir);
		FindSupportPoint(ref maxdot, ref sup, min + step, 		dir);
		FindSupportPoint(ref maxdot, ref sup, min + step.xyn(), dir);

		return sup + pos;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void
	FindSupportPoint(ref float maxdot, ref float3 sup, float3 pt, float3 dir)
	{
		float dot = math.dot(pt, dir);
		if(dot > maxdot)
		{
			maxdot = dot;
			sup    = pt;
		}
	}
}

public struct CMP_CapsuleCapsuleCollisionPair : IComponentData
{
	public Entity cap0_e,   cap1_e;
	public float3 cap0_vel, cap1_vel;
	public float  cap0_rad, cap1_rad;
	
	public CMP_CapsuleCapsuleCollisionPair(Entity cap0_e, Entity cap1_e, float3 cap0_vel, float3 cap1_vel, float cap0_rad, float cap1_rad)
	{
		this.cap0_e   = cap0_e;
		this.cap1_e   = cap1_e;
		this.cap0_vel = cap0_vel;
		this.cap1_vel = cap1_vel;
		this.cap0_rad = cap0_rad;
		this.cap1_rad = cap1_rad;
	}
}


//====
}
//====
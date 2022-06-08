using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

using EggPI.Common;
using EggPI.Mathematics;


//====
namespace EggPI.KinematicAgent
{
//====

	
public enum MoveMode
{
	GROUND,
	GROUND_DASH,
	AIR,
	AIR_DASH,
}

		
[Serializable]
public struct CapsuleConfig
{
	public float 			half_height;
	public float 			radius;
	public CapsuleCollider	unity_capsule;
	
	public CapsuleConfig(float half_height, float radius, CapsuleCollider unity_capsule)
	{
		this.half_height   = half_height;
		this.radius 	   = radius;
		this.unity_capsule = unity_capsule;
	}
	
	public float3
	GetTopSphereCenter(float3 ref_pos)
	{
		return new float3(ref_pos.x, ref_pos.y + half_height - radius, ref_pos.z);
	}
	
	public float3
	GetBottomSphereCenter(float3 ref_pos)
	{
		return new float3(ref_pos.x, ref_pos.y - half_height + radius, ref_pos.z);
	}
	
	public float3
	GetAbsoluteTop(float3 ref_pos)
	{
		return new float3(ref_pos.x, ref_pos.y + half_height, 0f);
	}
	
	public float3
	GetAbsoluteBottom(float3 ref_pos)
	{
		return new float3(ref_pos.x, ref_pos.y - half_height, 0f);
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
	
public struct CMP_MoveMode : IComponentData
{
	public MoveMode mode;
	
	public CMP_MoveMode(MoveMode mode)
	{
		this.mode = mode;
	}
}
	
public struct CMP_CapsuleCollider : IComponentData
{
	public float half_height;
	public float radius;
	
	public CMP_CapsuleCollider(float half_height, float radius)
	{
		this.half_height = half_height;
		this.radius 		= radius;
	}
}
	
public struct CMP_FloorData : IComponentData
{
	// public readonly Collider collider;
	public float3 impact_pos;
	public float3 col_pos;
	public float3 normal;
	public float  dist;
	public int    found;
	public float  time_left;
	public float  time_landed;
	public int    on_floor;
	public int	  on_walkable_floor;

	public CMP_FloorData(/* Collider collider, */ float3 impact_pos, float3 col_pos, float3 normal, float dist, float time_left, 
		float time_landed, bool found)
	{
		// this.collider = collider;
		this.impact_pos  = impact_pos;
		this.col_pos	 = col_pos;
		this.normal   	 = normal;
		this.dist 	  	 = dist;
		this.found 		 = found ? 1 : 0;
		this.time_left   = time_left;
		this.time_landed = time_landed;
		this.on_floor    = 0;
		this.on_walkable_floor = 0;
	}
}
	
[Serializable]
public struct CMP_FloorMask : IComponentData
{
	public LayerMask mask;
	
	public CMP_FloorMask(LayerMask mask)
	{
		this.mask = mask;
	}
}
	
[Serializable]
public struct CMP_MoveCfg : IComponentData
{
	public float min_ground;
	public float max_ground;
	public float ground_acc;

	public float min_air;
	public float max_air;
	public float air_acc;

	public float min_walkable_y;
	
	public CMP_MoveCfg(float min_ground, float max_ground, float ground_acc, float min_air, float max_air, float air_acc, float min_walkable_y)
	{
		this.min_ground = min_ground;
		this.max_ground = max_ground;
		this.ground_acc = ground_acc;

		this.min_air = min_air;
		this.max_air = max_air;
		this.air_acc = air_acc;

		this.min_walkable_y = min_walkable_y;
	}
}

[Serializable]
public struct CMP_MovementConfig : IComponentData
{
	public int 		 sim_iters;
	public float     min_walkable_y;
	public float     step_height;
	public LayerMask floor_mask;
	public float 	 min_jump_force;
	public float 	 max_jump_force;
	public float 	 time_to_jump_apex;
	public float 	 gravity;
	
	public CMP_MovementConfig(int sim_iters, float min_walkable_y, float step_height, LayerMask floor_mask, float min_jump_force, 
		float max_jump_force, float time_to_jump_apex, float gravity)
	{
		this.sim_iters			= sim_iters;
		this.floor_mask 		= floor_mask;
		this.min_walkable_y 	= min_walkable_y;
		this.step_height 		= step_height;
		this.min_jump_force	 	= min_jump_force;
		this.max_jump_force	   	= max_jump_force;
		this.time_to_jump_apex 	= time_to_jump_apex;
		this.gravity 			= gravity;
	}
}
	
[Serializable]
public struct CMP_GroundMovementConfig : IComponentData
{
	public float acceleration;
	public float braking_speed;
	public float min_speed;
	public float max_speed;

	
	public CMP_GroundMovementConfig(float acceleration, float braking_speed, float min_speed, float max_speed)
	{
		this.acceleration  = acceleration;
		this.braking_speed = braking_speed;
		this.min_speed 	   = min_speed;
		this.max_speed 	   = max_speed;

	}
}
	
[Serializable]
public struct CMP_AirMovementConfig : IComponentData
{
	public float acceleration;
	public float braking_speed;
	public float min_speed;
	public float max_speed;
	public float gravity_mod;
	public float terminal_vel;
	
	public CMP_AirMovementConfig(float acceleration, float braking_speed, float min_speed, float max_speed, float gravity_mod, 
		float terminal_vel)
	{
		this.acceleration  = acceleration;
		this.braking_speed = braking_speed;
		this.min_speed 	   = min_speed;
		this.max_speed 	   = max_speed;
		this.gravity_mod   = gravity_mod;
		this.terminal_vel  = terminal_vel;
	}
}
	
[Serializable]
public struct CMP_Jump : IComponentData
{
	public float time_last_executed;
	public float time_landed;
	public int   num;
	
	public CMP_Jump(float time_last_executed, float time_landed = float.PositiveInfinity)
	{
		this.time_last_executed = time_last_executed;
		this.time_landed = time_landed;
		this.num = 0;
	}
}
	
public struct CMP_JumpBuffer : IComponentData
{
	public float time_req;
}
	
//====
}
//====
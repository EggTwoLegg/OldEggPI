using Unity.Mathematics;
using UnityEngine;

using EggPI.Common;
using EggPI.Collision;


//====
namespace EggPI.KinematicAgent
{
//====


public enum GroundState
{
	AIR,
	GROUND,
	SLIDING_DOWN
}
	
public struct AgentMoveData
{
	public GroundState ground_state;
	public float3	   wallnorm;
	public float3 	   floornorm;
	public Delta  	   delta;

	public int 	  was_grounded_last_tick;
//	public float3 last_walkable_ground_pos;
	
	public float3			move_dir;
	public int		   		colmask;
	public CMP_MoveCfg 		move_cfg;
	public CMP_CapsuleShape cap;

	public float dt;
	
	public AgentMoveData(float3 move_dir, CMP_MoveCfg move_cfg, CMP_CapsuleShape cap, int colmask, float dt)
	{
		ground_state = GroundState.GROUND;
		floornorm	 = float3.zero;
		wallnorm 	 = float3.zero;
		delta		 = new Delta();
		
		was_grounded_last_tick 	 = 0;
//		last_walkable_ground_pos = float3.zero;

		this.move_dir = move_dir;
		this.move_cfg = move_cfg;
		this.colmask  = colmask;
		this.cap	  = cap;
		this.dt		  = dt;
	}
}


//====
}
//====
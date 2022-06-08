using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using EggPI.Common;
using EggPI.KinematicAgent;
using EggPI.Mathematics;


//====
namespace EggPI.KinematicAgent
{
//====


[BurstCompile]
[RequireComponentTag(typeof(CFLAG_KinematicAgent))]
public struct BatchMovecastsJob : IJobProcessComponentDataWithEntity<Position, CMP_Velocity>
{
	[WriteOnly] public NativeArray<CapsulecastCommand> movecasts;
				public NativeArray<AgentMoveData> 	   agent_move_data;
	
	public BatchMovecastsJob(NativeArray<CapsulecastCommand> movecasts, NativeArray<AgentMoveData> agent_move_data)
	{
		this.movecasts 		 = movecasts;
		this.agent_move_data = agent_move_data;
	}

	public void 
	Execute(Entity ent, int i_agent, ref Position pos, ref CMP_Velocity vel)
	{
		var move_data = agent_move_data[i_agent];
		
		// Quit early if there's no movement.
		if(move_data.dt < bmath.KINDA_SMALL_NUMBER) { return; }

		move_data.was_grounded_last_tick = move_data.ground_state == GroundState.GROUND ? 1 : 0;
		
		switch(move_data.ground_state)
		{
			case GroundState.AIR:
				ProcessAir(ref move_data, ref pos, ref vel);
				break;
			case GroundState.GROUND:
				ProcessGrounded(ref move_data, ref pos, ref vel);
				break;
			case GroundState.SLIDING_DOWN:
				ProcessSlideDownSurface(ref move_data, ref pos, ref vel);
				break;
		}
		
		var movedist = math.length(vel.val);

		agent_move_data[i_agent] = move_data;
		
		MoveUtils.SweepCapsuleBatch(movecasts, i_agent, move_data.cap, pos.Value, math.normalizesafe(vel.val), movedist, move_data.colmask);
	}
	
	private void
	ProcessGrounded(ref AgentMoveData move_data, ref Position pos, ref CMP_Velocity vel)
	{
		var cfg = move_data.move_cfg;
		var dt  = move_data.dt;

		var hit_wall = math.lengthsq(move_data.wallnorm) > bmath.KINDA_SMALL_NUMBER;
		
		// If we can't walk on the surface we last hit (e.g. a wall), we need to slide along it.
		if(hit_wall && move_data.wallnorm.y < move_data.move_cfg.min_walkable_y)
		{
			vel.val = MoveUtils.GetSlideVector(move_data.move_dir * cfg.max_ground * dt, move_data.wallnorm, cfg.min_walkable_y);
			move_data.move_dir = vel.val;
			return;
		}
		
		// Calculate our movement along the ramp.
		vel.val = MoveUtils.GetRampVector(move_data.move_dir * cfg.max_ground * dt, move_data.floornorm);
	}
	
	private void
	ProcessAir(ref AgentMoveData move_data, ref Position pos, ref CMP_Velocity vel)
	{
		var cfg = move_data.move_cfg;
		
		// Flag us as in the air.
		move_data.ground_state = GroundState.AIR;
		
		bool was_below_term_vel = vel.val.y > -54f;

		var dt 	    = move_data.dt;
		var movedir = move_data.move_dir;
		
		vel.val = new float3(movedir.x * cfg.max_air * dt, vel.val.y - 0.0981f * dt, movedir.z * cfg.max_air * dt);
	}
	
	private void
	ProcessSlideDownSurface(ref AgentMoveData move_data, ref Position pos, ref CMP_Velocity vel)
	{
		var cfg = move_data.move_cfg;
		
		// Flag us as sliding.
		move_data.ground_state = GroundState.SLIDING_DOWN;
		
		// Apply gravity and slide us down the surface.
		vel.val = -math.up() * 0.0981f * move_data.dt;
		vel.val = MoveUtils.GetSlideDownRampVector(vel.val, move_data.floornorm);
	}
}


//====
}
//====
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using EggPI.Common;
using EggPI.Mathematics;


//====
namespace EggPI.KinematicAgent
{
//====


[BurstCompile]
[RequireComponentTag(typeof(CFLAG_KinematicAgent))]
public struct MoveJob : IJobProcessComponentDataWithEntity<Position, CMP_Velocity>
{
			   public NativeArray<AgentMoveData> agent_move_data;
	[ReadOnly] public NativeArray<RaycastHit> 	 movecast_hits;
	
	public MoveJob(NativeArray<RaycastHit> movecast_hits, NativeArray<AgentMoveData> agent_move_data)
	{
		this.movecast_hits 	 = movecast_hits;
		this.agent_move_data = agent_move_data;
	}
	
	public void 
	Execute(Entity ent, int i_ent, ref Position pos, ref CMP_Velocity vel)
	{
		var move_data = agent_move_data[i_ent];
		
		// Too little time left for it to matter. Quit out early.
		if(move_data.dt < bmath.KINDA_SMALL_NUMBER) { return; }
		
		var hit 	= movecast_hits[i_ent];
		var did_hit	= math.lengthsq(hit.normal) >= (1f - bmath.KINDA_SMALL_NUMBER);
		
		var adj_dist = math.max(0f, hit.distance - MoveUtils.STEP_BACK_DIST);

		move_data.wallnorm = hit.normal;
		
		if(did_hit)
		{
			if(adj_dist > 0f)
			{
				// Determine hit 'time' and scale our delta time by it.
				var vel_len  = math.length(vel.val);
				var hit_time = adj_dist / vel_len;

				move_data.dt *= 1f - hit_time;

				vel.val = math.normalizesafe(vel.val) * adj_dist;
			}
			else
			{
				vel.val = float3.zero;
			}
		}
		else
		{
			// Don't zero y, so that we can continue to accumulate gravity.
			move_data.dt = 0f;
		}

		agent_move_data[i_ent] = move_data;

		pos.Value += vel.val;
	}
	
	private void
	ProcessGrounded(int i_ent, bool did_hit, ref AgentMoveData move_data, RaycastHit move_hit, ref Position pos, ref CMP_Velocity vel)
	{
		var cfg = move_data.move_cfg;
		
		// If we hit a walkable slope, we need to project our movement onto it.
		if(did_hit) 
		{			
			if(move_hit.normal.y >= cfg.min_walkable_y)
			{
				vel.val = MoveUtils.GetRampVector(vel.val, move_hit.normal);
			}
			else
			{
				move_data.ground_state = 0;
				vel.val = float3.zero;
			}
		}
	}
	
	private void
	ProcessSliding(int i_ent, bool did_hit, ref AgentMoveData move_data, RaycastHit move_hit, ref Position pos, ref CMP_Velocity vel)
	{
		var cfg = move_data.move_cfg;
		
		if(did_hit)
		{
			// Can we land on this surface?
			if(move_hit.normal.y >= cfg.min_walkable_y)
			{
				move_data.ground_state = GroundState.GROUND;
				move_data.floornorm    = move_hit.normal;
			}
		}
	}
	
	private void
	ProcessAir(int i_ent, bool did_hit, ref AgentMoveData move_data, RaycastHit move_hit, ref Position pos, ref CMP_Velocity vel)
	{
		var cfg = move_data.move_cfg;
		
		if(did_hit) 
		{
			// Can we land on this surface?
			if(move_hit.normal.y >= cfg.min_walkable_y)
			{
				move_data.ground_state = GroundState.GROUND;
				move_data.floornorm    = move_hit.normal;
				vel.val = MoveUtils.GetRampVector(vel.val, move_hit.normal);
			}
			else
			{
				move_data.ground_state = GroundState.SLIDING_DOWN;
				move_data.floornorm = move_hit.normal;
				vel.val = MoveUtils.GetSlideDownRampVector(vel.val, move_hit.normal);
			}
		}
	}
}

	
//====
}
//====
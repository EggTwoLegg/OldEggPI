using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using EggPI.Mathematics;


//====
namespace EggPI.KinematicAgent
{
//====


[BurstCompile]
[RequireComponentTag(typeof(CFLAG_KinematicAgent))]
public struct SnapToFloorOrFallJob : IJobProcessComponentDataWithEntity<Position>
{
	[ReadOnly] public NativeArray<RaycastHit>	 original_floor_hits;
	[ReadOnly] public NativeArray<RaycastHit>	 correct_floor_hits;
			   public NativeArray<AgentMoveData> agt_move_data;
	
	public SnapToFloorOrFallJob(NativeArray<RaycastHit> original_floor_hits, NativeArray<RaycastHit> correct_floor_hits, 
		NativeArray<AgentMoveData> agt_move_data)
	{
		this.original_floor_hits = original_floor_hits;
		this.correct_floor_hits  = correct_floor_hits;
		this.agt_move_data  	 = agt_move_data;
	}
	
	public void 
	Execute(Entity ent, int i_agt, ref Position pos)
	{
		var move_data = agt_move_data[i_agt];

		move_data.was_grounded_last_tick = move_data.ground_state == GroundState.GROUND ? 1 : 0;
		
		// If the last surface we move onto is walkable, we can quit early and use that as our floor.
		if(move_data.wallnorm.y >= move_data.move_cfg.min_walkable_y)
		{
			move_data.floornorm    = move_data.wallnorm;
			move_data.ground_state = GroundState.GROUND;
			return;
		}

		var floor_dist = original_floor_hits[i_agt].distance;
		var correct_floor_norm = correct_floor_hits[i_agt].normal;

		bool is_grounded = math.lengthsq(correct_floor_norm) >= (1f - bmath.KINDA_SMALL_NUMBER);
			
		if(is_grounded)
		{
			ProcessGrounded(ref move_data, floor_dist, correct_floor_norm, ref pos);
		}
		else
		{
			ProcessAir(ref move_data);
		}

		agt_move_data[i_agt] = move_data;
	}
	
	private void 
	ProcessGrounded(ref AgentMoveData move_data, float floor_dist, float3 floor_norm, ref Position pos)
	{
		move_data.ground_state = GroundState.GROUND;
		move_data.floornorm    = floor_norm;
		
		var half_height_shrink_amt = move_data.cap.length * 0.5f * 0.1f;
		var floordist = floor_dist - half_height_shrink_amt;
			
		// Snap to the floor, if we are too far away from it.
		if((floordist > Constants.MAX_FLOOR_DIST || floordist < Constants.MIN_FLOOR_DIST))
		{
			// Move agent to the point of collision with the floor.
			pos.Value += -math.up() * floordist;
				
			// Constrain the collider between the min and max floor distances.
			floordist = math.clamp(floordist, Constants.MIN_FLOOR_DIST, Constants.MAX_FLOOR_DIST);	
				
			// Adjust the collider to sit just above the ground.
			pos.Value += math.up() * floordist;
		}
		
		// Switch to sliding, if we can't walk on the floor.
		if(floor_norm.y < move_data.move_cfg.min_walkable_y)
		{
			move_data.ground_state = GroundState.SLIDING_DOWN; // Sliding.
		}
	}
	
	private void
	ProcessAir(ref AgentMoveData move_data)
	{
		move_data.ground_state = GroundState.AIR; // Falling.
	}
}


//====
}
//====
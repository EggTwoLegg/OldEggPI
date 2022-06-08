using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;

using EggPI.Common;
using EggPI.Mathematics;
using EggPI.Collision;


//====
namespace EggPI.KinematicAgent
{
//====


public class KinematicAgentMoveSystem : JobComponentSystem
{
	private ComponentGroup inject_group;
	
	private NativeArray<AgentMoveData> 		agent_move_data;
	private NativeArray<CapsulecastCommand> floorcasts;
	private NativeArray<RaycastHit> 		floorcast_hits;
	private NativeArray<CapsulecastCommand> movecasts;
	private NativeArray<RaycastHit> 		movecast_hits;
	private NativeArray<RaycastCommand> 	raycasts;
	private NativeArray<RaycastHit>			raycast_hits;

	private int lastcachelen;
	
	[BurstCompile]
	[RequireComponentTag(typeof(CFLAG_KinematicAgent))]
	private struct PrepAgentDataJob : IJobProcessComponentDataWithEntity<CMP_CapsuleShape, CMP_FloorMask, CMP_MoveCfg, 
		CMP_MoveInput>
	{
		[WriteOnly] public NativeArray<AgentMoveData> agent_move_data;

		public float dt;
		
		public PrepAgentDataJob(NativeArray<AgentMoveData> agent_move_data, float dt)
		{
			this.agent_move_data = agent_move_data;
			this.dt = dt;
		}
		
		public void 
		Execute(Entity ent, int i_agent, [ReadOnly] ref CMP_CapsuleShape cap, [ReadOnly] ref CMP_FloorMask mask, 
				[ReadOnly] ref CMP_MoveCfg cfg, [ReadOnly] ref CMP_MoveInput input)
		{
			agent_move_data[i_agent] = new AgentMoveData(input.val.xnz(), cfg, cap, mask.mask, dt);
		}
	}
	
	[BurstCompile]
	[RequireComponentTag(typeof(CFLAG_KinematicAgent))]
	private struct BatchFloorCastsJob : IJobProcessComponentDataWithEntity<Position>
	{
		[ReadOnly]  public NativeArray<AgentMoveData>  	   agent_move_data;
		[WriteOnly] public NativeArray<CapsulecastCommand> floorcasts;
		
		public BatchFloorCastsJob(NativeArray<CapsulecastCommand> floorcasts, NativeArray<AgentMoveData> agent_move_data)
		{
			this.floorcasts 	 = floorcasts;
			this.agent_move_data = agent_move_data;
		}

		public void 
		Execute(Entity ent, int i_ent, [ReadOnly] ref Position pos)
		{
			var move_data = agent_move_data[i_ent];
			
			// We found a valid surface to walk onto when we last moved in some direction.
			if(move_data.wallnorm.y >= move_data.move_cfg.min_walkable_y)
			{
				floorcasts[i_ent] = new CapsulecastCommand();
				return;
			}
			
			MoveUtils.FindFloorBatch(floorcasts, i_ent, move_data.cap, pos.Value, MoveUtils.FLOOR_CHECK_DIST, move_data.colmask);
		}
	}
	
	[BurstCompile]
	private struct BatchFixFloorNormalsJob : IJobParallelFor
	{
		[ReadOnly]  public NativeArray<AgentMoveData>  agent_move_data;
		[ReadOnly]  public NativeArray<RaycastHit> 	   floorcast_hits;
		[WriteOnly] public NativeArray<RaycastCommand> ray_cmds;
		
		public BatchFixFloorNormalsJob(NativeArray<RaycastHit> floorcast_hits, NativeArray<RaycastCommand> ray_cmds, 
			NativeArray<AgentMoveData> agent_move_data)
		{
			this.floorcast_hits  = floorcast_hits;
			this.ray_cmds 		 = ray_cmds;
			this.agent_move_data = agent_move_data;
		}
		
		public void 
		Execute(int i_hit)
		{
			var hit 	  = floorcast_hits[i_hit];
			var move_data = agent_move_data[i_hit];
			
			if(float3.zero.Equals(hit.normal)) // || move_data.wallnorm.y >= move_data.move_cfg.min_walkable_y)
			{
				ray_cmds[i_hit] = new RaycastCommand();
				return;
			}
			
			ray_cmds[i_hit] = new RaycastCommand(hit.point + new Vector3(0f, MoveUtils.FLOOR_CHECK_DIST, 0f), -math.up(), 
				MoveUtils.FLOOR_CHECK_DIST + MoveUtils.STEP_BACK_DIST, move_data.colmask, 1);
		}
	}
	
	protected override void 
	OnCreateManager()
	{
		inject_group 	= GetComponentGroup(ComponentType.Create<CFLAG_KinematicAgent>());
		agent_move_data	= new NativeArray<AgentMoveData>(8, Allocator.Persistent);
		floorcasts	 	= new NativeArray<CapsulecastCommand>(8, Allocator.Persistent);
		floorcast_hits 	= new NativeArray<RaycastHit>(8, Allocator.Persistent);
		movecasts 	 	= new NativeArray<CapsulecastCommand>(8, Allocator.Persistent);
		movecast_hits 	= new NativeArray<RaycastHit>(8, Allocator.Persistent);
		raycasts	 	= new NativeArray<RaycastCommand>(8, Allocator.Persistent);
		raycast_hits 	= new NativeArray<RaycastHit>(8, Allocator.Persistent);

		lastcachelen = 8;
	}

	protected override JobHandle 
	OnUpdate(JobHandle deps)
	{
		var dt = Time.deltaTime;

		// Determine the number of kinematic character controllers to move.
		var num_e  = inject_group.CalculateLength();

		if(num_e > lastcachelen)
		{
			agent_move_data.Dispose();
			floorcasts.Dispose();
			floorcast_hits.Dispose();
			movecasts.Dispose();
			movecast_hits.Dispose();
			raycasts.Dispose();
			raycast_hits.Dispose();
			
			agent_move_data = new NativeArray<AgentMoveData>(num_e, Allocator.Persistent);
			floorcasts	 	= new NativeArray<CapsulecastCommand>(num_e, Allocator.Persistent);
			floorcast_hits 	= new NativeArray<RaycastHit>(num_e, Allocator.Persistent);
			movecasts 	 	= new NativeArray<CapsulecastCommand>(num_e, Allocator.Persistent);
			movecast_hits 	= new NativeArray<RaycastHit>(num_e, Allocator.Persistent);
			raycasts	 	= new NativeArray<RaycastCommand>(num_e, Allocator.Persistent);
			raycast_hits 	= new NativeArray<RaycastHit>(num_e, Allocator.Persistent);
		}
		lastcachelen = num_e;
		
		// Cache all entity data that will not change.
		var prep_job = new PrepAgentDataJob(agent_move_data, dt);
		var prep_hdl = prep_job.Schedule(this, deps);
		
		var batch_floor_casts_job 	  = new BatchFloorCastsJob(floorcasts, agent_move_data);
		var batch_fix_floor_norms_job = new BatchFixFloorNormalsJob(floorcast_hits, raycasts, agent_move_data);
		var snap_to_floor_or_fall_job = new SnapToFloorOrFallJob(floorcast_hits, raycast_hits, agent_move_data);
		var batch_movecast_job 		  = new BatchMovecastsJob(movecasts, agent_move_data);
		var apply_move_job 			  = new MoveJob(movecast_hits, agent_move_data);
		
		// Sweep to detect floor(s) beneath the agent. Also need to correct the normals due to how unity handles swept capsules.
		var batch_floor_casts_hdl 	  = batch_floor_casts_job.Schedule(this, prep_hdl);
		var floorcast_hdl 		  	  = CapsulecastCommand.ScheduleBatch(floorcasts, floorcast_hits, 8, batch_floor_casts_hdl);		
		var batch_fix_floor_norms_hdl = batch_fix_floor_norms_job.Schedule(num_e, 64, floorcast_hdl);
		var fix_floor_norms_hdl 	  = RaycastCommand.ScheduleBatch(raycasts, raycast_hits, 8, batch_fix_floor_norms_hdl);
		
		// Land, begin to slide, or begin to fall.
		var snap_to_floor_or_fall_hdl = snap_to_floor_or_fall_job.Schedule(this, fix_floor_norms_hdl);
		
		// After landing or starting to fall, sweep in the direction of the user's desired input to find what is hit, if anything.
		var batch_movecast_hdl = batch_movecast_job.Schedule(this, snap_to_floor_or_fall_hdl);
		var movecast_hdl	   = CapsulecastCommand.ScheduleBatch(movecasts, movecast_hits, 8, batch_movecast_hdl);

		// Apply movement, after detecting potential obstacles.
		var apply_move_hdl = apply_move_job.Schedule(this, movecast_hdl);
		
		// Sweep to detect floor(s) beneath the agent and correct normals (2nd time).
		batch_floor_casts_hdl     = batch_floor_casts_job.Schedule(this, apply_move_hdl);
		floorcast_hdl             = CapsulecastCommand.ScheduleBatch(floorcasts, floorcast_hits, 8, batch_floor_casts_hdl);	
		batch_fix_floor_norms_hdl = batch_fix_floor_norms_job.Schedule(num_e, 64, floorcast_hdl);
		fix_floor_norms_hdl       = RaycastCommand.ScheduleBatch(raycasts, raycast_hits, 8, batch_fix_floor_norms_hdl);
		
		// Land, begin to slide, or begin to fall (2nd time).
		snap_to_floor_or_fall_hdl = snap_to_floor_or_fall_job.Schedule(this, fix_floor_norms_hdl);
		
		// Sweep in the direction of the user's input (2nd time).
		batch_movecast_hdl = batch_movecast_job.Schedule(this, snap_to_floor_or_fall_hdl);
		movecast_hdl       = CapsulecastCommand.ScheduleBatch(movecasts, movecast_hits, 8, batch_movecast_hdl);
		
		// Apply movement (2nd time).
		apply_move_hdl = apply_move_job.Schedule(this, movecast_hdl);

		return apply_move_hdl;
	}

	protected override void 
	OnDestroyManager()
	{
		agent_move_data.Dispose();
		floorcasts.Dispose();
		floorcast_hits.Dispose();
		movecasts.Dispose();
		movecast_hits.Dispose();
		raycasts.Dispose();
		raycast_hits.Dispose();
	}
}


//====
}
//====
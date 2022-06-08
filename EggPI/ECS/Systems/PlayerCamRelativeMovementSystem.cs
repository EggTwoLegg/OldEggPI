using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using EggPI.Common;
using EggPI.Mathematics;


//=====
namespace EggPI.Common
{
//====


[UpdateAfter(typeof(PollInputSystem<CMP_BasePlayerInput>))]
public unsafe class PlayerCamRelativeMovementSystem : JobComponentSystem
{
	private ComponentGroup cam_group;
	private ComponentGroup player_input_group;
	
	[BurstCompile]
	private struct GetPlayerInputJob : IJobProcessComponentData<CMP_BasePlayerInput>
	{
		[WriteOnly] public NativeArray<float2> out_input;
		
		public GetPlayerInputJob(NativeArray<float2> out_input)
		{
			this.out_input = out_input;
		}
		
		public void 
		Execute([ReadOnly] ref CMP_BasePlayerInput input)
		{
			out_input[0] = math.normalizesafe(input.move_axes);
		}
	}
	
	[BurstCompile]
	[RequireComponentTag(typeof(CFLAG_Player))]
	private struct MoveCamRelJob : IJobProcessComponentData<CMP_MoveInput>
	{
		[DeallocateOnJobCompletion]
		public NativeArray<float2> player_ipt;
		
		public float3 fwd, rgt;
		
		public MoveCamRelJob(NativeArray<float2> player_ipt, float3 fwd, float3 rgt)
		{
			this.player_ipt = player_ipt;
			
			this.fwd = fwd;
			this.rgt = rgt;
		}
		
		public void 
		Execute(ref CMP_MoveInput move_input)
		{
			var mv = math.normalizesafe(player_ipt[0]);
			if (mv.Equals(float2.zero))
			{
				move_input.val = float2.zero;
				return;
			}
	
			move_input.val = math.normalizesafe(rgt * mv.x + fwd * mv.y).xz;
		}
	}
	
	protected override void 
	OnCreateManager()
	{
		cam_group 		   = GetComponentGroup(typeof(CMP_Camera), ComponentType.Create<Transform>());
		player_input_group = GetComponentGroup(typeof(CMP_BasePlayerInput));
	}

	protected override JobHandle 
	OnUpdate(JobHandle deps)
	{
		if(cam_group.CalculateLength() < 1 || player_input_group.CalculateLength() < 1) { return deps; }

		var cam_trans = cam_group.GetTransformAccessArray()[0];
		
		var player_input = new NativeArray<float2>(1, Allocator.TempJob);
		
		var get_player_input_job = new GetPlayerInputJob(player_input);
		var get_player_input_hdl = get_player_input_job.Schedule(this, deps);
		
		var move_cam_rel_job = new MoveCamRelJob(player_input, ((float3)cam_trans.forward).xnz(), ((float3)cam_trans.right).xnz());
		var move_cam_rel_hdl = move_cam_rel_job.Schedule(this, get_player_input_hdl);

		return move_cam_rel_hdl;
	}
}


//====
}
//====
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;


//====
namespace EggPI.Common
{
//====
	
	
[DisableAutoCreation]
[UpdateBefore(typeof(PreLateUpdate))]
public class CameraControlSystem<TCMP_Input> : JobComponentSystem where TCMP_Input : struct, IComponentData, ICommonInput
{
	private const float HORIZONTAL_CAMERA_SPEED    = 128.0f;
	private const float FP_HORIZONTAL_CAMERA_SPEED = 128.0f;
	private const float VERTICAL_CAMERA_SPEED      = 96.0f;
	private const float FP_VERTICAL_CAMERA_SPEED   = 96.0f;

	private ComponentGroup player_input_group;
	private ComponentGroup cam_group;
	
	// Using CDFE instead of ACCT, because we alias the native memory when we try to use both at once, and we need to be able to grab
	// the position from the camera's target entity, which can be stored in any chunk. We can still maintain linear access if we operate
	// fully on an entity in a chunk, before switching to the next.
	[BurstCompile]
	private struct ControlJob<TCMP_Input> : IJob where TCMP_Input : struct, IComponentData, ICommonInput
	{
		[DeallocateOnJobCompletion]
		public NativeArray<ArchetypeChunk> cam_chunks;
		
		[ReadOnly, DeallocateOnJobCompletion] 
		public NativeArray<ArchetypeChunk> player_input_chunks;
		
		[ReadOnly] public ArchetypeChunkEntityType				   acet;
				   public ComponentDataFromEntity<Position>   	   cdfe_pos;
				   public ComponentDataFromEntity<Rotation>   	   cdfe_rot;
				   public ComponentDataFromEntity<CMP_Camera> 	   cdfe_cam;
		[ReadOnly] public ComponentDataFromEntity<TCMP_Input> 	   cdfe_player_ipt;
				   public float dt;
		
		public ControlJob
		(
			float dt,
			NativeArray<ArchetypeChunk> 			 cam_chunks,
			NativeArray<ArchetypeChunk> 			 player_input_chunks,
			ArchetypeChunkEntityType				 acet,
			ComponentDataFromEntity<Position> 		 cdfe_pos,
			ComponentDataFromEntity<Rotation> 		 cdfe_rot,
			ComponentDataFromEntity<CMP_Camera> 	 cdfe_cam,
			ComponentDataFromEntity<TCMP_Input> 	 cdfe_player_ipt
		)
		{
			this.dt = dt;
			
			this.cam_chunks 		 = cam_chunks;
			this.player_input_chunks = player_input_chunks;
			this.acet				 = acet;
			this.cdfe_pos 			 = cdfe_pos;
			this.cdfe_rot 			 = cdfe_rot;
			this.cdfe_cam 			 = cdfe_cam;
			this.cdfe_player_ipt 	 = cdfe_player_ipt;
			this.cdfe_pos			 = cdfe_pos;
		}
		
		public void 
		Execute()
		{
			var ipt_ent = player_input_chunks[0].GetNativeArray(acet)[0];
			var cam_ent = cam_chunks[0].GetNativeArray(acet)[0];

			var look_vel = cdfe_player_ipt[ipt_ent].GetLookAxes();

			var cam = cdfe_cam[cam_ent];
			var pos = cdfe_pos[cam_ent];
			var rot = cdfe_rot[cam_ent];
			
			var tgt_pos = cam.target_ent != Entity.Null ? cdfe_pos[cam.target_ent].Value : pos.Value;
			
			if(cam.first_person == 1)
			{
				if(cam.lock_vertical == 0)   { cam.x_rot = math.clamp(cam.x_rot + -look_vel.y * FP_VERTICAL_CAMERA_SPEED * dt, -80f, 80f); }
				if(cam.lock_horizontal == 0) { cam.y_rot = (cam.y_rot + look_vel.x * FP_HORIZONTAL_CAMERA_SPEED * dt) % 360f; }
			
				pos.Value = tgt_pos + cam.cam_offset;
				rot.Value = quaternion.Euler(math.radians(cam.x_rot), math.radians(cam.y_rot), 0f, math.RotationOrder.XYZ);

				// Update chunk data.
				cdfe_cam[cam_ent] = cam;
				cdfe_pos[cam_ent] = pos;
				cdfe_rot[cam_ent] = rot;
			
				return;
			}
			
			if(cam.lock_vertical == 0)   { cam.x_rot = math.clamp(cam.x_rot + -look_vel.y * VERTICAL_CAMERA_SPEED * dt, -80f, 80f); }
			if(cam.lock_horizontal == 0) { cam.y_rot = (cam.y_rot + look_vel.x * HORIZONTAL_CAMERA_SPEED * dt) % 360f; }
		
			float  	   zoom_dist = cam.zoom_dist;
			quaternion orbit_rot = Quaternion.Euler(cam.x_rot, cam.y_rot, 0f);

			// Orbit camera.
			pos.Value = math.mul(orbit_rot, new float3(0f, 0f, -zoom_dist)) + tgt_pos;
		
			// Look at target.
			float3 cam_fwd = tgt_pos - pos.Value;
			//quaternion look_rot = quaternion.lookRotation(cam_fwd, new float3(0f, 1f, 0f));
			quaternion look_rot = Quaternion.LookRotation(cam_fwd, Vector3.up);
			rot.Value = look_rot;

			pos.Value += cam.cam_offset;
			
			// Update chunk data.
			cdfe_cam[cam_ent] = cam;
			cdfe_pos[cam_ent] = pos;
			cdfe_rot[cam_ent] = rot;
		}
	}

	protected override void 
	OnCreateManager()
	{
		cam_group 		   = GetComponentGroup(typeof(CMP_Camera));
		player_input_group = GetComponentGroup(typeof(TCMP_Input));
	}

	protected override JobHandle 
	OnUpdate(JobHandle deps)
	{
		if(player_input_group.CalculateLength() < 1 || cam_group.CalculateLength() < 1) { return deps; }

		var ctrl_job = new ControlJob<TCMP_Input>
		(
			Time.deltaTime,
			cam_group.CreateArchetypeChunkArray(Allocator.TempJob),
			player_input_group.CreateArchetypeChunkArray(Allocator.TempJob),
			GetArchetypeChunkEntityType(),
			GetComponentDataFromEntity<Position>(),
			GetComponentDataFromEntity<Rotation>(),
			GetComponentDataFromEntity<CMP_Camera>(),
			GetComponentDataFromEntity<TCMP_Input>()
		);

		return ctrl_job.Schedule(deps);
	}
}

	
//====
}
//====
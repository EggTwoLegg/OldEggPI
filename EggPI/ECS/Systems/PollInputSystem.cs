using System.Diagnostics;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Experimental.PlayerLoop;


//====
namespace EggPI.Common
{
//====


[DisableAutoCreation]
[UpdateBefore(typeof(Update))]
public class PollInputSystem<TCMP_PlayerInput> : JobComponentSystem 
	where TCMP_PlayerInput : struct, IComponentData, ICommonInput
{
	private const float BUFFER_TIME = 0.1f;

	private ComponentGroup input_group;

	protected override void 
	OnCreateManager()
	{
		input_group = GetComponentGroup(typeof(TCMP_PlayerInput), typeof(CBF_InputBuffer<TCMP_PlayerInput>));
	}

	[BurstCompile]
	private struct SetInputJob : IJobChunk
	{
		private ArchetypeChunkComponentType<TCMP_PlayerInput> 				acct_player_input;
		private ArchetypeChunkBufferType<CBF_InputBuffer<TCMP_PlayerInput>> acbt_player_input;
		
		public float2 look_input;
		public float2 move_input;
		public float2 mpos;
		
		public SetInputJob
		(
			float2 look_input,
			float2 move_input,
			float2 mpos,
			ArchetypeChunkComponentType<TCMP_PlayerInput> 				acct_player_input,
			ArchetypeChunkBufferType<CBF_InputBuffer<TCMP_PlayerInput>> acbt_player_input
		)
		{
			this.look_input = look_input;
			this.move_input = move_input;
			this.mpos	   	= mpos;
			this.acct_player_input = acct_player_input;
			this.acbt_player_input = acbt_player_input;
		}
		
		public void 
		Execute(ArchetypeChunk chunk, int i_chunk, int i_something)
		{
			// No input entities...
			if(chunk.Count < 1) { return; }
			
			var input_arr = chunk.GetNativeArray(acct_player_input);
			var input_buf = chunk.GetBufferAccessor(acbt_player_input)[0];
			
			var input = input_arr[0];
			
			input.SetLookAxes(look_input);
			input.SetMoveAxes(move_input);
			input.SetMouseScreenPos(mpos);
			input_arr[0] 	= input;

			// Insert at the front of the buffer.
			input_buf.Insert(0, new CBF_InputBuffer<TCMP_PlayerInput>(input));
			input_buf.RemoveAt(Constants.USER_COMMAND_BUFFER_LEN);
		}
	}

	protected override JobHandle 
	OnUpdate(JobHandle deps)
	{		
		var set_input_job = new SetInputJob
		(
			new float2(UnityEngine.Input.GetAxis("LookHor"), UnityEngine.Input.GetAxis("LookVert")), // Look input
			new float2(UnityEngine.Input.GetAxis("MoveHor"), UnityEngine.Input.GetAxis("MoveVert")), // Move input
			math.max(float2.zero, ((float3) (UnityEngine.Input.mousePosition)).xy),	  // Mouse pos
			GetArchetypeChunkComponentType<TCMP_PlayerInput>(),
			GetArchetypeChunkBufferType<CBF_InputBuffer<TCMP_PlayerInput>>()
		);
		var set_input_hdl = set_input_job.Schedule(input_group, deps);

		return set_input_hdl;
	}
}


//====
}
//====
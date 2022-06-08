using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;


//====
namespace EggPI.Common
{
//====


public struct CMP_Camera : IComponentData
{
	public float 	x_rot;
	public float 	y_rot;
	public float 	zoom_dist;
	public float3 	cam_offset;
	public int 		first_person;
	public int 		lock_horizontal;
	public int 		lock_vertical;
	public Entity	target_ent;
}
	
public struct CMP_BasePlayerInput : IComponentData, ICommonInput
{
	public float2 move_axes;
	public float2 look_axes;
	public int	  clicked;
	public float2 mouse_pos;

	public CMP_BasePlayerInput(float2 move_axes, float2 look_axes)
	{
		this.move_axes = move_axes;
		this.look_axes = look_axes;
		this.clicked   = 0;
		mouse_pos 	   = new float2(0f, 0f);
	}
	
	public float2 
	GetLookAxes()
	{
		return look_axes;
	}
	
	public void
	SetLookAxes(float2 val)
	{
		look_axes = val;
	}
	
	public float2
	GetMoveAxes()
	{
		return move_axes;
	}
	
	public void
	SetMoveAxes(float2 val)
	{
		move_axes = val;
	}
	
	public float2
	GetMouseScreenPos()
	{
		return mouse_pos;
	}
	
	public void
	SetMouseScreenPos(float2 val)
	{
		mouse_pos = val;
	}
}

[InternalBufferCapacity(Constants.USER_COMMAND_BUFFER_LEN)]
public struct CBF_InputBuffer<TInput> : IBufferElementData where TInput : struct, IComponentData, ICommonInput
{
	public TInput input;

	public CBF_InputBuffer(TInput input)
	{
		this.input = input;
	}
}
	
public struct CMP_Velocity : IComponentData
{
	public float3 val;

	public CMP_Velocity(float3 val)
	{
		this.val = val;
	}
}
	
public struct CMP_MoveInput : IComponentData
{
	public float2 val;

	public CMP_MoveInput(float2 val)
	{
		this.val = val;
	}
}
	
public struct CMP_RootMotion : IComponentData
{
	public float3 motion;
	public int additive;
	public int apply_gravity;
	
	public CMP_RootMotion(float3 motion, bool apply_gravity, bool additive)
	{
		this.motion 	   = motion;
		this.apply_gravity = apply_gravity ? 1 : 0;
		this.additive 	   = additive ? 1 : 0;
	}
}


//====
}
//====
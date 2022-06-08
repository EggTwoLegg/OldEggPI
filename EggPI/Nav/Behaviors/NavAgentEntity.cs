using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using EggPI.Common;
using EggPI.Collision;


//====
namespace EggPI.Nav
{
//====


public class NavAgentEntity : GameObjectEntity
{
	public float move_speed;
	
	void
	Start()
	{
		EntityManager.AddSharedComponentData(Entity, new CFLAG_NavAgent());
		EntityManager.AddComponentData(Entity, new CMP_NavAgent(0, move_speed));
		EntityManager.AddComponentData(Entity, new Position() { Value = transform.position });
		EntityManager.AddComponentData(Entity, new Rotation() { Value = quaternion.LookRotation(transform.forward, math.up())});
		EntityManager.AddComponentData(Entity, new CMP_MoveInput());
		EntityManager.AddComponentData(Entity, new CMP_Velocity());
		EntityManager.AddComponentData(Entity, new CMP_CapsuleShape(0.5f, 2f));
	}
}


//====
}
//====
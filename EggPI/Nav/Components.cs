using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Experimental.AI;

//====
namespace EggPI.Nav
{
//====


public struct CMP_NavmeshPath : ISharedComponentData
{
	public NativeArray<NavMeshLocation> path;
	
	public CMP_NavmeshPath(NativeArray<NavMeshLocation> path)
	{
		this.path = path;
	}
}

	
public struct CMP_NavAgent : IComponentData
{
	public int i_navmesh;
	public float speed;
	
	public CMP_NavAgent(int i_navmesh, float speed)
	{
		this.i_navmesh = i_navmesh;
		this.speed 	   = speed;
	}
}
	

//====
}
//====
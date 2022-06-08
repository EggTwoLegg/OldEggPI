using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Experimental.AI;


//====
namespace EggPI.Nav
{
//====


public struct CFLAG_NavAgent : ISharedComponentData {}
public struct CFLAG_FailedToFindNavmeshPath : ISharedComponentData {}
public struct CFLAG_RequestedNavmeshPath : ISharedComponentData {}
	
public struct CFLAG_NavmeshPathFound : ISharedComponentData
{
//	public NavMeshQuerySystem.PathQueryResults results;
//	
//	public CFLAG_NavmeshPathFound(NavMeshQuerySystem.PathQueryResults results)
//	{
//		this.results = results;
//	}
}
	
public struct CFLAG_PartialNavmeshPathFound : ISharedComponentData {}


//====
}
//====
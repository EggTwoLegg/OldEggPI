using Unity.Entities;
using Unity.Jobs;
using UnityEngine;


//====
namespace EggPI.Collision
{
//====


public class MultiboxPruneSystem : JobComponentSystem
{
	protected override JobHandle 
	OnUpdate(JobHandle input_deps)
	{
		return input_deps;
	}
}


//====
}
//====
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

using EggPI.Common;


//====
namespace EggPI.Nav
{
//====


public class RandomAgentMovement : MonoBehaviour
{
	private GameObjectEntity goe;
	private Random rand;

	private float time_last_change;
	
	private void
	Start()
	{
		goe = GetComponent<GameObjectEntity>();
		
		rand = new Random((uint)gameObject.GetInstanceID());
	}

	private void 
	Update()
	{
		if(Time.time - time_last_change < 0.5f) { return; }

		time_last_change = Time.time;
		
		var randmove = new CMP_MoveInput(math.normalizesafe(new float2(rand.NextFloat(-1f, 1f), rand.NextFloat(-1f, 1f))));
		
		goe.EntityManager.SetComponentData(goe.Entity, randmove);
	}
}


//====
}
//====
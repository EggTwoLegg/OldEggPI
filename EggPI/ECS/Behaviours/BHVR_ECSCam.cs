using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using EggPI.Common;
using UnityEngine;


//====
namespace EggPI.Common
{
//====



[RequireComponent(typeof(Camera))]
public class BHVR_ECSCam : MonoBehaviour
{
	public float 	x_rot;
	public float 	y_rot;
	public float  	zoom_dist;
	public float3 	cam_offset;
	public bool 	first_person;
	public bool 	lock_horizontal;
	public bool 	lock_vertical;

	public GameObjectEntity target_ent;
	
	private void 
	Start()
	{
		GameObjectEntity goe = GetComponent<GameObjectEntity>();
		
		if(!goe) { return; }

		EntityManager e_man = goe.EntityManager;
		Entity e = goe.Entity;

		CMP_Camera cam_cmp = new CMP_Camera()
		{
			x_rot = x_rot,
			y_rot = y_rot,
			zoom_dist = zoom_dist,
			cam_offset = cam_offset,
			first_person = (first_person) ? 1 : 0,
			lock_horizontal = (lock_horizontal) ? 1 : 0,
			lock_vertical = (lock_vertical) ? 1 : 0,
			target_ent = target_ent != null ? target_ent.Entity : Entity.Null
		};
		
		e_man.AddComponentData(e, cam_cmp);
		e_man.AddComponentData(e, new Position() { Value = transform.position });
		e_man.AddComponentData(e, new Rotation() { Value = transform.rotation });
		
		e_man.RemoveComponent<BHVR_ECSCam>(e);
		
		Destroy(this);
	}
	
	public bool
	PointerPick(float2 ptr_s_pos, out GameObject hit_go)
	{
		hit_go = null;
		return false;
//		Ray pick_ray = cam.ScreenPointToRay(new float3(ptr_s_pos.xy, 0f));
//
//		if(Physics.Raycast(pick_ray, out var hit, 100_000f))
//		{
//			hit_go = hit.collider.gameObject;
//			return true;
//		}
//
//		hit_go = null;
//		return false;
	}
}

	
//====
}
//====
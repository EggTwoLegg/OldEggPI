using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

using EggPI.Common;
using EggPI.Mathematics;
using EggPI.Collision;


//====
namespace EggPI.KinematicAgent
{
//====
	
	
public static class MoveUtils
{
	public const float STEP_BACK_DIST 	  = 0.01f;
	public const float ALMOST_ONE 	   	  = 1.0f - 1.175494351e-38F;
	public const int   MAX_DEPEN_ATTEMPTS = 8;
	public const float FLOOR_CHECK_DIST   = STEP_BACK_DIST * 16f;

	private static Collider[] overlap_test_colliders = new Collider[128];
	
	public static bool 
	Move(ref CapsuleConfig cap_cfg, float3 start_pos, Delta delta, int col_mask, out HitData hit_results, out float3 end_pos)
	{
		end_pos = start_pos;

//		// If we can't depenetrate, we can't move.
//		if(!Depenetrate(ref cap_cfg, start_pos, col_mask, out var depen_pos))
//		{
//			hit_results = new HitData(0f, null, new float3(0f), new float3(0f), new float3(0f), 0f, false, true, true);
//			return false;
//		}
//		
//		// We depenetrated (or were never initially in penetration), so set our out impact_pos to the adjusted impact_pos.
//		end_pos = depen_pos;
		
		// Sweep collider in direction of movement.
		bool hit_something = SweepCapsule(ref cap_cfg, end_pos, delta, col_mask, out hit_results);
		
		end_pos = SetCapsulePos(ref cap_cfg, end_pos, hit_results.col_point, delta * (1.0f - hit_results.time), hit_results.normal, col_mask);

		return !end_pos.Equals(start_pos);
	}
	
	private static bool
	Depenetrate(ref CapsuleConfig cap_cfg, float3 start_pos, int col_mask, out float3 end_pos)
	{
		end_pos = start_pos;
		
		// Attempt depenetration, if we are stuck in an initial overlap.
		for(int i_depen_attempt = 0; i_depen_attempt < MAX_DEPEN_ATTEMPTS; i_depen_attempt++)
		{
			int num_overlapped = Physics.OverlapCapsuleNonAlloc(cap_cfg.GetTopSphereCenter(end_pos), cap_cfg.GetBottomSphereCenter(end_pos), 
				cap_cfg.radius, overlap_test_colliders, col_mask, QueryTriggerInteraction.Ignore);
		
			// No penetration; quit early.
			if(num_overlapped == 0)
			{
				return true;
			}
			
			for(int i_overlapped = 0; i_overlapped < num_overlapped; i_overlapped++)
			{
				Collider overlapped_col = overlap_test_colliders[i_overlapped];

				if(Physics.ComputePenetration(cap_cfg.unity_capsule, end_pos, Quaternion.identity, overlapped_col,
					overlapped_col.transform.position, overlapped_col.transform.rotation, out var depen_dir, out var depen_dist))
				{
					end_pos += (float3) depen_dir * depen_dist;
				}
			}
		}

		return !Physics.CheckCapsule(cap_cfg.GetTopSphereCenter(end_pos), cap_cfg.GetBottomSphereCenter(end_pos), cap_cfg.radius, col_mask, 
			QueryTriggerInteraction.Ignore);
	}
	
	public static float3
	SetCapsulePos(ref CapsuleConfig cap_cfg, float3 start_pos, float3 desired_pos, Delta delta, float3 hit_normal, int col_mask)
	{		
		float3 adjusted_pos = desired_pos + (hit_normal * STEP_BACK_DIST);
		
		if(float.IsNaN(adjusted_pos.x) || float.IsNaN(adjusted_pos.y) || float.IsNaN(adjusted_pos.z))
		{
			Debug.LogError($"NaN error on adjusted pos! start_pos:{start_pos} | desired_pos:{desired_pos} | hit_normal:{hit_normal}");
			return start_pos;
		}
		
		// End impact_pos has initial overlap after adjusting for the surface normal, so attempt to nudge back just before the hit location.
		if(Physics.CheckCapsule(cap_cfg.GetTopSphereCenter(adjusted_pos), cap_cfg.GetBottomSphereCenter(adjusted_pos), cap_cfg.radius, 
			col_mask, QueryTriggerInteraction.Ignore))
		{			
			delta.len -= STEP_BACK_DIST;
			
			// Move too small after adjustment, just return the starting impact_pos.
			if(delta.len <= STEP_BACK_DIST) { return start_pos; }

			adjusted_pos = start_pos + delta.dir * delta.len;

			// See if we overlap after adjusting back before the original surface hit.
			if(Physics.CheckCapsule(cap_cfg.GetTopSphereCenter(adjusted_pos), cap_cfg.GetBottomSphereCenter(adjusted_pos), cap_cfg.radius, 
				col_mask, QueryTriggerInteraction.Ignore))
			{
				// Unable to adjust back, we're technically stuck.
				// Depenetration logic in movement systems *should* free us.
				return start_pos;
			}

			return adjusted_pos;
		}

		// No overlap at adjusted impact_pos; we're good!
		return adjusted_pos;
	}
	
	public static bool
	SweepCapsule(ref CapsuleConfig cap_cfg, float3 start_pos, Delta delta, int col_mask, out HitData hit_data)
	{
		if(Physics.CapsuleCast(cap_cfg.GetTopSphereCenter(start_pos), cap_cfg.GetBottomSphereCenter(start_pos), cap_cfg.radius, delta.dir, 
			out var hit, delta.len, col_mask, QueryTriggerInteraction.Ignore))
		{			
			hit_data = new HitData
			(
				hit.distance / delta.len,
				hit.collider,
				hit.point,
				start_pos + (delta.dir * hit.distance),
				hit.normal,
				hit.distance,
				true,
				false,
				false
			);

			return true;
		}
		
		// Nothing was hit.
		hit_data = new HitData
		(
			1.0f,
			null,
			start_pos + delta.dir * delta.len,
			start_pos + delta.dir * delta.len,
			new float3(0f, 0f, 0f),
			delta.len,
			false,
			false,
			false
		);

		return false;
	}
	
	public static void
	SweepCapsuleBatch(NativeArray<CapsulecastCommand> casts, int i_batch, CMP_CapsuleShape cap, float3 start_pos, float3 dir, float dist, 
		int mask)
	{		
		casts[i_batch] = new CapsulecastCommand
		(
			cap.GetTopSphereCenter(start_pos), cap.GetBottomSphereCenter(start_pos), cap.radius, dir, dist, mask
		);
	}
	
	public static bool
	FindFloor(CapsuleConfig cap_cfg, float3 start_pos, float query_dist, int floor_mask, out CMP_FloorData floor_data)
	{
		// Shrink capsule height to avoid precision issues when we have an initial overlap with the surface.
		float half_height_shrink_amt = cap_cfg.half_height * 0.1f;
		cap_cfg.half_height -= half_height_shrink_amt;
		cap_cfg.radius -= STEP_BACK_DIST;
		
		// Increase query distance by the amount we've shrunk our capsule by.
		query_dist += half_height_shrink_amt;
		
		float3 sweep_dir = new float3(0f, -1f, 0f);
		
		if(query_dist > 0f)
		{
			RaycastHit hit;
			if(Physics.CapsuleCast(cap_cfg.GetTopSphereCenter(start_pos), cap_cfg.GetBottomSphereCenter(start_pos), cap_cfg.radius, 
				sweep_dir, out hit, query_dist, floor_mask, QueryTriggerInteraction.Ignore))
			{				
				// Since we shrunk our capsule to handle initial overlaps and then added double the shrink amount to our query distance,
				// we need to undo that by subtracting it from the distance reported by the cast. If this value ends up negative, it will
				// denote that we've started in overlap and push us upwards by the appropriate amount.
				float true_dist = hit.distance - half_height_shrink_amt;
				
				// Trace a ray from above the contact point down to get the true surface normal
				if(hit.collider.Raycast(new Ray(hit.point + new Vector3(0f, query_dist, 0f), new float3(0f, -1f, 0f)), out var ray_hit, 100f))
				{
					hit.normal = ray_hit.normal;
				}
				
				floor_data = new CMP_FloorData(hit.point, start_pos + sweep_dir * true_dist, hit.normal, true_dist, 0f, 0f, true);
				return true;
			}
		}
		
		floor_data = new CMP_FloorData(/*null,*/ start_pos, start_pos, new float3(0f, 1f, 0f), 0f, 0f, 0f, false);
		return false;
	}
	
	public static void 
	FindFloorBatch(NativeArray<CapsulecastCommand> capcasts, int i_batch, CMP_CapsuleShape cap, float3 start_pos, float query_dist, int mask)
	{
		// Shrink capsule height to avoid precision issues when we have an initial overlap with the surface.
		var half_len = cap.length * 0.5f;
		var half_height_shrink_amt = half_len * 0.1f;
		cap.length -= half_height_shrink_amt  * 2f;
		cap.radius -= STEP_BACK_DIST;

		// Increase query distance by the amount we've shrunk our capsule by.
		query_dist += half_height_shrink_amt;

		float3 sweep_dir = -math.up();
		
		if(query_dist > 0f)
		{
			capcasts[i_batch] = new CapsulecastCommand
			(
				cap.GetTopSphereCenter(start_pos), cap.GetBottomSphereCenter(start_pos), cap.radius, sweep_dir, query_dist, mask
			);

			return;
		}
		
		capcasts[i_batch] = new CapsulecastCommand();
	}
	
	public static Delta
	GetRampDelta(Delta delta, float3 normal, bool preserve_length = false)
	{		
		if(normal.y < ALMOST_ONE && normal.y > bmath.KINDA_SMALL_NUMBER)
		{
			float3 delta_vec = delta.AsVector();
			float delta_dot_normal = math.dot(delta_vec, normal);
			Delta ramp_delta = new Delta(new float3(delta_vec.x, -delta_dot_normal / normal.y, delta_vec.z));
			
			if(preserve_length)
			{
				return new Delta(math.normalize(ramp_delta.AsVector()) * delta.len);
			}
			
			return ramp_delta;
		}

		return delta;
	}
	
	public static float3
	GetRampVector(float3 delta, float3 normal)
	{		
		if(normal.y < ALMOST_ONE && normal.y > bmath.KINDA_SMALL_NUMBER)
		{
			// Project movement onto ramp.
			float delta_dot_normal = math.dot(delta, normal);
			return new float3(delta.x, -delta_dot_normal / normal.y, delta.z);
		}

		return delta;
	}
	
	public static float3
	GetSlideDownRampVector(float3 delta, float3 normal)
	{
		if(normal.y < ALMOST_ONE && normal.y > bmath.KINDA_SMALL_NUMBER)
		{
			// Project movement onto plane formed by ramp.
			// Project movement onto ramp.
			float delta_dot_normal = math.dot(delta, normal);

			return delta - (delta_dot_normal * normal);
		}

		return delta;
	}
	
	
	public static bool
	SlideOnSurface(ref CapsuleConfig cap_cfg, float3 start_pos, Delta delta, float3 surface_norm, float3 floor_normal, bool on_floor, 
		bool force_slide_down, float min_walkable_y, int col_mask, out float3 end_pos, out HitData hit_results)
	{
		end_pos = start_pos;
		
		// Avoid sliding up a surface that we can't walk on.
		if(surface_norm.y < min_walkable_y && surface_norm.y > 0f)
		{
			Debug.Log("Avoiding sliding up an unwalkable surface...");
			surface_norm.y = 0f;
			surface_norm = math.normalizesafe(surface_norm);
		}
		else if(surface_norm.y < 0f && on_floor)  // Don't push down into the floor.
		{
			bool moving_into_floor = (math.dot(surface_norm, delta.dir) < math.FLT_MIN_NORMAL) && floor_normal.y < ALMOST_ONE;
			if(moving_into_floor)
			{	
				surface_norm = floor_normal;
			}
			
			surface_norm.y = 0f;
			surface_norm = math.normalizesafe(surface_norm);
		}	
		
		// Slide along surface.
		Delta slide_delta = GetSlideDelta(delta, surface_norm, min_walkable_y, on_floor);
		
		// Don't slide back towards where we started moving from.
		if(math.dot(delta.dir, slide_delta.dir) > 0f) 
		{
			// Move along slide delta.
			bool did_move = Move(ref cap_cfg, end_pos, slide_delta, col_mask, out hit_results, out end_pos);
			
			// Unable to move --- maybe stuck in penetration somehow?
			if(!did_move)
			{
				return false;
			}

			slide_delta *= 1f - hit_results.time;
			
			// Second wall was hit.
			if(hit_results.valid_hit && hit_results.time < ALMOST_ONE)
			{
				// Adjust for when we hit two walls.
				slide_delta = GetTwoSurfaceSlideDelta(slide_delta, hit_results.normal, surface_norm, on_floor, min_walkable_y);
				
				// Don't make a tiny move or a move in a direction away from our desired direction.
				if(slide_delta.len > math.FLT_MIN_NORMAL && math.dot(slide_delta.dir, delta.dir) > math.FLT_MIN_NORMAL)
				{
					Move(ref cap_cfg, end_pos, slide_delta, col_mask, out hit_results, out end_pos);
				}
			}

			return true;
		}

		hit_results = HitData.Zero();
		return false;
	}
	
	private static float3 
	GetTwoSurfaceSlideVector(Delta delta, float3 first_surface_norm, float3 second_surface_norm, bool on_floor, 
		float min_walkable_y)
	{		
		float3 slide_delta = delta.AsVector();
		
		Debug.Log("Two surface");
		
		// Hit a corner that was 90 degrees or less. Use the cross product for direction moved.
		if(math.dot(first_surface_norm, second_surface_norm) <= 0f)
		{
			float3 new_dir = math.cross(first_surface_norm, second_surface_norm);
			new_dir = math.normalizesafe(new_dir);

			slide_delta = math.dot(delta.dir, new_dir) * new_dir;
				
			// Don't go back where we came, reverse output delta.
			if(math.dot(delta.dir, slide_delta) < 0f)
			{
				slide_delta *= -1.0f;
			}
			
			//Debug.Log($"90 or less: {slide_delta}");
		}
		else
		{
			float3 new_dir = GetSlideVector(delta, first_surface_norm, min_walkable_y, on_floor);
			
			if(math.dot(new_dir, delta.dir) <= 0f) // Don't go backwards.
			{
				slide_delta = new float3(0f, 0f, 0f);
			}
			else if(math.abs(math.dot(first_surface_norm, second_surface_norm) - 1.0f) < STEP_BACK_DIST) // Hit same wall, nudge away.
			{
				slide_delta += first_surface_norm * STEP_BACK_DIST;
			}
			
			//Debug.Log($"Greater than 90: {slide_delta}");
		}
		
//		// Keep the same ground movement speed when moving on slopes.
//		if(min_walkable_y > 0f && slide_delta.y >= min_walkable_y)
//		{			
//			// Make output slide delta match the length of our input delta parameter.
//			float3 scaled_slide_delta = math_experimental.normalizeSafe(slide_delta) * delta.len;
//			
//			Debug.Log($"Scaled delta: {scaled_slide_delta} : {first_surface_norm.y}");
//			
//			// Scale the vertical velocity upwards so as to keep us moving horizontally at the same rate.
//			slide_delta = new float3(delta.dir.x, scaled_slide_delta.y / first_surface_norm.y, delta.dir.z);
//			
//			// Debug.Log($"Scaled delta: {slide_delta}");
//		}
		
		if(slide_delta.y < 0f && on_floor) // Don't push into the floor.
		{
			slide_delta.y = 0f;
		}
		
		return slide_delta;
	}
	
	private static Delta
	GetTwoSurfaceSlideDelta(Delta delta, float3 first_surface_norm, float3 second_surface_norm, bool on_floor, float min_walkable_y)
	{
		return new Delta(GetTwoSurfaceSlideVector(delta, first_surface_norm, second_surface_norm, on_floor, min_walkable_y));
	}
	
	public static float3
	GetSlideVector(Delta delta, float3 surface_norm, float min_walkable_y, bool on_ground)
	{		
		// Project delta along a plane formed by the surface normal.
		float3 slide_delta = (delta.AsVector() - (surface_norm * math.dot(delta.AsVector(), surface_norm)));
		
		// Prevent going up slopes that we can't walk on.
		if(slide_delta.y < min_walkable_y && on_ground)
		{
			float3 planar_dir    = math.normalizesafe(new float3(delta.dir.x, 0f, delta.dir.z)); // Remove vertical.
			float3 planar_normal = math.normalizesafe(new float3(surface_norm.x, 0f, surface_norm.z));
			
			// Project planar_delta onto the plane formed by plane_normal
			slide_delta = (planar_dir - (planar_normal * math.dot(planar_dir, planar_normal))) * delta.len;
		}
		
		return slide_delta;
	}
	
	public static Delta
	GetSlideDelta(Delta delta, float3 surface_norm, float min_walkable_y, bool on_ground)
	{
		return new Delta(GetSlideVector(delta, surface_norm, min_walkable_y, on_ground));
	}
	
	public static float3
	GetSlideVector(float3 delta, float3 hit_norm, float min_walkable_y = float.MinValue)
	{
		// Project delta along a plane formed by the surface normal.
		float3 slide_delta = delta - (hit_norm * math.dot(delta, hit_norm));
		
		// Prevent going up slopes that we can't walk on.
		if(slide_delta.y < min_walkable_y)
		{
			var delta_dir 		 = math.normalizesafe(delta);
			float3 planar_dir    = math.normalizesafe(new float3(delta_dir.x, 0f, delta_dir.z)); // Remove vertical.
			float3 planar_normal = math.normalizesafe(new float3(hit_norm.x, 0f, hit_norm.z));
			
			// Project planar_delta onto the plane formed by plane_normal
			slide_delta = (planar_dir - (planar_normal * math.dot(planar_dir, planar_normal))) * math.length(delta);
		}
		
		return slide_delta;
	}
		
	public static bool
	StepUp(ref CapsuleConfig cap_cfg, float3 start_pos, Delta delta, float step_height, float min_walkable_y, int col_mask, out float3 end_pos)
	{
		end_pos = start_pos;
		float  ledge_scan_dist = 2f * STEP_BACK_DIST - math.FLT_MIN_NORMAL;
		float3 ledge_scan_fwd  = (math.normalize(new float3(delta.dir.x, 0f, delta.dir.y)) * ledge_scan_dist);
		float  sweep_down_dist = step_height + STEP_BACK_DIST;
		float3 down_vec        = new float3(0f, -1f, 0f);
		float3 ledge_test_pos  =
			cap_cfg.GetAbsoluteBottom(start_pos) + ledge_scan_fwd + new float3(0f, step_height + cap_cfg.radius + STEP_BACK_DIST, 0f);

		// Sweep a capsule down by our step height to see what is hit.
		Physics.CapsuleCast(ledge_test_pos, ledge_test_pos, cap_cfg.radius, down_vec, out var hit, sweep_down_dist, col_mask, 
			QueryTriggerInteraction.Ignore);

		float diff_y                     = hit.point.y - start_pos.y;
		bool  normal_opposed_to_movement = math.dot(delta.dir, hit.normal) < 0;
		
		// Hit something, see if we have clearance to stand there.
		if(hit.collider != null && (hit.normal.y >= min_walkable_y || !normal_opposed_to_movement) && diff_y <= step_height)
		{
			// @TODO: What the fuck was I thinking when I made this? Fix this line below.
			float3 adjusted_hit_point = start_pos + ledge_scan_fwd * 4f + new float3(0f, math.abs(hit.point.y - start_pos.y) + STEP_BACK_DIST, 0f);
			
			Debug.Log(hit.point.y);
			
			// Enough clearance to stand here, so this ledge passes and we adjust our capsule to this location.
			if(!Physics.CheckCapsule(cap_cfg.GetTopSphereCenter(adjusted_hit_point), cap_cfg.GetBottomSphereCenter(adjusted_hit_point), 
				cap_cfg.radius, col_mask, QueryTriggerInteraction.Ignore))
			{
				end_pos = adjusted_hit_point;
				return true;
			}

			// Debug.Log("No room to stand here.");
			
			return false;
		}

		return false;
	}
	
	public static void
	FixCapsuleSweepNormal(Vector3 position, ref RaycastHit hit, int mask)
	{
		if(hit.collider.Raycast(new Ray(position, (hit.point - position)), out var new_hit, 100f))
		{
			hit.normal = new_hit.normal;
		}
		else
		{
			Debug.Log("Nothing hit in correction");
		}
	}
	
	public static void
	LandOnFloor(ref CMP_FloorData floor_data, ref HitData hit_data, float time)
	{
		floor_data = new CMP_FloorData(hit_data.impact_point, hit_data.col_point, hit_data.normal, hit_data.dist, floor_data.time_left, time,
			true);
	}
	
	public static void
	LandOnFloor(ref CMP_FloorData floor_data, ref HitData hit_data, ref CMP_Jump jump, float time)
	{
		floor_data = new CMP_FloorData(hit_data.impact_point, hit_data.col_point, hit_data.normal, hit_data.dist, floor_data.time_left, time,
			true);

		jump.num = 0;
	}
	
	public static float3
	ClampInputLength(float3 unclamped_input, float min_len, float max_len)
	{
		float3 res 	  = unclamped_input;
		float sqr_len = math.lengthsq(unclamped_input);
		
		if(sqr_len < min_len * min_len)
		{
			res = math.normalizesafe(res) * min_len;
		}
		else if(sqr_len > max_len * max_len)
		{
			res = math.normalizesafe(res) * max_len;
		}

		return res;
	}
}
	
	
//====
}
//====
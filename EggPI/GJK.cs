using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

using EggPI.Mathematics;


//====
namespace EggPI.Collision
{
//====


public struct ConvexCastHit
{
	public float3 pt;
	public float  time;
	public float3 norm;
	public float  pen_amt;
	
	public ConvexCastHit(float3 pt, float time, float3 norm)
	{
		this.pt	  = pt;
		this.time = time;
		this.norm = norm;
		
		pen_amt = 0f;
	}
}
	

public struct GJK
{
	private const int   MAX_GJK_ITERS = 32;
	public  const float SWEEP_EPSILON = 1e-4f;

	public float3 cached_p0, cached_p1;

	[NativeDisableContainerSafetyRestriction] private Simplex simplex;
	
	// http://www.dtecta.com/papers/unpublished04raycast.pdf
	// http://www.kuffner.org/james/software/dynamics/mirtich/mirtichThesis.pdf
	public bool
	SweepCapsuleCapsule
	(
		CMP_CapsuleShape cap0, 
		float3 cap0_start, float3 cap0_end, 
		quaternion cap0_startrot, quaternion cap0_endrot, 
		CMP_CapsuleShape cap1, 
		float3 cap1_start, float3 cap1_end, 
		quaternion cap1_startrot, quaternion cap1_endrot, 
		out ConvexCastHit hit
	)
	{
		simplex.Clear();
		
		float3 cap0_pos = cap0_start;
		float3 cap1_pos = cap1_start;
		
		float3 linvel0 = cap0_end - cap0_start;
		float3 linvel1 = cap1_end - cap1_start;

		float3 relvel = linvel0 - linvel1;
		
		hit = new ConvexCastHit(float3.zero, 1f, float3.zero);

		float3 sup0 = cap0.Support(cap0_pos, -relvel);
		float3 sup1 = cap1.Support(cap1_pos,  relvel);
		
		float3 closest_pt = sup0 - sup1;
		float  sqrdist    = 0f;

		// Push initial minknowski diff point to simplex.
		if(!pushPoint(closest_pt, sup0, sup1, ref closest_pt, ref sqrdist) || sqrdist <= SWEEP_EPSILON) { return false; }

		int num_iters = 0;
		
		float3 mdiff_pt = float3.zero;

		hit.time = 0f;
		
		// Perform conservative advancement.
		while(sqrdist > SWEEP_EPSILON && num_iters++ < MAX_GJK_ITERS)
		{
			sup0 = cap0.Support(cap0_pos, -closest_pt);
			sup1 = cap1.Support(cap1_pos,  closest_pt);

			mdiff_pt = sup0 - sup1;

			float cdotdiff = math.dot(closest_pt, mdiff_pt);
			
			// Don't advance away from the origin.
			if(cdotdiff > 0f)
			{
				float cdotrvel = math.dot(closest_pt, relvel);
				
				if(cdotrvel >= -SWEEP_EPSILON) { return false; }

				hit.time -= cdotdiff / cdotrvel;
				
				if(hit.time > 1.0f || hit.time < 0.0f) { return false; }
				
				// Interpolate cap positions by lambda.
				cap0_pos = cap0_start + hit.time * linvel0;
				cap1_pos = cap1_start + hit.time * linvel1;

				hit.norm = closest_pt;
			}

			// Only add the vert to the simplex if it's not already there, otherwise we can run into NaN / divide by zero / precision issues.
			if(!pushPoint(mdiff_pt, sup0, sup1, ref closest_pt, ref sqrdist)) { break; }
		}

		hit.pt = cached_p1;
		
		// Normal is projected onto other capsule's axis and points out to the point of impact. This is necessary, as applying the standard
		// GJK-based-raycast algorithm will result in a hit normal that is dependent upon the angle of impact and not the actual surface
		// (this is okay for capsule to sphere tests, though).
		float3 cap_axis    = cap1.up_vec * ((cap1.length * 0.5f) - cap1.radius);
		float3 adj_hit_pos = cached_p1 - cap1_pos; // "Zero" the hit point.
		float  dothitcapup = math.dot(cap_axis, adj_hit_pos) / math.lengthsq(cap_axis);
		float3 axis_hit    = cap_axis * dothitcapup; // Project hit point onto capsule up axis.
		hit.norm = math.normalizesafe(adj_hit_pos - axis_hit);

		hit.pen_amt = math.sqrt(SWEEP_EPSILON - sqrdist);

		return true;

		bool 
		pushPoint(float3 pt, float3 sup0, float3 sup1, ref float3 closest_pt, ref float sqrdist)
		{
			// Only add the vert to the simplex if it's not already there, otherwise we can run into NaN / divide by zero / precision issues.
			if(!simplex.ContainsPt(mdiff_pt)) { simplex.Push(pt, sup0, sup1); }
			
			bool res = GJKClosestPoints(ref closest_pt, sqrdist);
			
			if(res)
			{
				sqrdist = math.lengthsq(closest_pt);
				return true;
			}

			return false;
		}
	}
	
	public bool 
	OverlapCapsuleCapsule(ref CMP_CapsuleShape cap0, float3 cap0_pos, ref CMP_CapsuleShape cap1, float3 cap1_pos)
	{
		int num_iters = 0;
		
		simplex.Clear();
		
		float3 dir   = new float3(1f, 0f, 0f);
		float3 sup0  = cap0.Support(cap0_pos,  dir);
		float3 sup1  = cap1.Support(cap0_pos, -dir);
		float3 suppt = sup0 - sup1;

		// Prevent invalid direction (will cause a zero vector for a future support point).
		if(math.abs(math.dot(dir, suppt)) >= math.length(suppt) * 0.8f)
		{
			dir = new float3(0f, 1f, 0f);
			suppt = cap0.Support(cap0_pos, dir) - cap1.Support(cap1_pos, -dir);
		}
		
		simplex.Push(suppt);

		dir = -suppt;

		int status = 2;
		
		//GJK algorithm --- iteratively build a simplex that attempts to enclose the origin.
		while((status = GJKIntersection(ref dir, ref suppt, ref cap0, cap0_pos, ref cap1, cap1_pos)) == 2 && num_iters++ < MAX_GJK_ITERS)
		{
		}
		
		if(num_iters >= MAX_GJK_ITERS)
		{
			Debug.LogWarning("GJK didn't converge optimally."); 
			return false; 
		}

		return status == 1;
	}
	
	private int
	GJKIntersection(ref float3 dir, ref float3 suppt, ref CMP_CapsuleShape cap0, float3 cap0_pos, ref CMP_CapsuleShape cap1, float3 cap1_pos)
	{
		// FPU error.
		if(math.lengthsq(dir) < 0.0001f) { return 0; }

		float3 sup0 = cap0.Support(cap0_pos,  dir);
		float3 sup1 = cap1.Support(cap1_pos, -dir);
		suppt = sup0 - sup1;
		
		// Support point did not go beyond the origin, thus no intersection.
		if(math.dot(suppt, dir) < 0) { return 0; }

		simplex.Push(suppt);

		float3 ao = -simplex[0]; // pt a to origin pt.
		float3 ab = (simplex[1] - simplex[0]);
		
		if(simplex.num == 2) // Line
		{
			// Origin lies between the first two points that comprise the line.
			// Set search dir to be coplanar with ao and perpendicular to ab.
			dir = math.cross(ab, math.cross(ao, ab)); // ab x ao x ab
			return 2; // Line can't contain simplex in either the 2d or 3d case, so we skip to the next iter.
		}
		
		float3 ac = (simplex[2] - simplex[0]);
		float3 abc = math.cross(ab, ac); // -triangle normal.
		
		if(simplex.num == 3) // Triangle
		{				
			// Origin lies within the triangle or in some voronoi region (edge) bounded by the triangle.
			
			if(math.dot(ao, math.cross(ab, abc)) >= 0) // Voronoi region of ab. (ao | ab x abc) > 0
			{
				simplex.Set(simplex[0], simplex[1]); 	  // Reset to line ab.
				dir = math.cross(ab, math.cross(ab, ao)); // perp to ab and coplanar with ao.
				return 2;
			}
			
			if(math.dot(ao, math.cross(abc, ac)) >= 0) // Voronoi region of ac. (ao | abc x ac) > 0
			{
				simplex.Set(simplex[0], simplex[2]); 	  // Reset to line ac.
				dir = math.cross(ac, math.cross(ac, ao)); // perp to ac and coplanar with ao.
				return 2;
			}
			
			// Origin somewhere in triangular prism bounded by triangle (above or below triangle).
			
			if(math.dot(ao, abc) >= 0) // Origin above (or within) triangle.
			{
				dir = abc; // Direction towards interior of triangular prism bounded by triangle.
				return 2;
			}
			
			// Origin below triangle.
			simplex.Set(simplex[0], simplex[2], simplex[1]);
			dir = -abc; // Direction set to triangle face normal.
			return 2;
		}
		
		// Tetrahedron case. Determine if simplex is within tetrahedron or within the voronoi regions bounded by the faces.
		
		if(math.dot(ao, math.cross(ab, ac)) >= 0) // Origin infront of ABC.
		{
			TetrahedronTest(ref dir, ao);
			
			return 2;
		}

		float3 ad = simplex[3] - simplex[0];
		
		if(math.dot(ao, math.cross(ac, ad)) >= 0) // Origin infront of triangle ACD.
		{
			simplex.Set(simplex[0], simplex[2], simplex[3]);
			
			TetrahedronTest(ref dir, ao);
			
			return 2;
		}
		
		if(math.dot(ao, math.cross(ad, ab)) >= 0) // Origin infront of triangle ADB.
		{
			simplex.Set(simplex[0], simplex[3], simplex[1]);
			
			TetrahedronTest(ref dir, ao);
			
			return 2;
		}

		// Intersection found.
		return 1;
	}
	
	private bool
	GJKClosestPoints(ref float3 closest_pt_to_origin, float last_sqrdist)
	{	
		if(simplex.num == 0) { return false; }
		
		if(simplex.num == 1) // Closest point from origin to single point on CSO.
		{
			cached_p0 = simplex.ps[0];
			cached_p1 = simplex.qs[0];
			closest_pt_to_origin = simplex[0];
			return true; // Always true, since a single point can't be 'degenerate' or 'invalid.'
		}
		
		if(simplex.num == 2) // Closest point from origin to line on CSO.
		{
			float3 from = simplex[0];
			float3 to   = simplex[1];

			float3 diff = float3.zero - from;
			float3 dir	= to - from;
			
			float t = math.dot(dir, diff);
			
			if(t > 0)
			{
				float vdotv = math.dot(dir, dir);
				
				if(t < vdotv)
				{
					t /= vdotv;
					simplex.Set(0, 1);
				}
				else
				{
					t = 1;
					simplex.Set(1); // Reduce to vert b.
				}
			}
			else
			{
				t = 0;
				simplex.Set(0); // Reduce to vert a.
			}

			cached_p0 = simplex.ps[0] + t * (simplex.ps[1] - simplex.ps[0]);
			cached_p1 = simplex.qs[0] + t * (simplex.qs[1] - simplex.qs[0]);
			
			closest_pt_to_origin = from + t * dir;

			return 1f - t >= 0f && t >= 0f;
		}
		
		if(simplex.num == 3) // Closest point on origin to triangle on CSO.
		{
			float3 a = simplex[0];
			float3 b = simplex[1];
			float3 c = simplex[2];

			closest_pt_to_origin = ClosestPointOnTriangleToPoint(a, b, c, 0, 1, 2, float3.zero, out var barycentric_coords);

			cached_p0 = simplex.ps[0] * barycentric_coords[0] +
						simplex.ps[1] * barycentric_coords[1] +
						simplex.ps[2] * barycentric_coords[2];

			cached_p1 = simplex.qs[0] * barycentric_coords[0] +
						simplex.qs[1] * barycentric_coords[1] +
						simplex.qs[2] * barycentric_coords[2];

			return barycentric_coords.x >= 0f && barycentric_coords.y >= 0f && barycentric_coords.z >= 0f;
		}
		
		// Closest point on origin to tetrahedron CSO.
		float3 ao  = -simplex[0]; // pt a to origin pt.
		float3 ab  = simplex[1] - simplex[0];
		float3 ac  = simplex[2] - simplex[0];
		float3 ad  = simplex[3] - simplex[0];
		float3 bd  = simplex[3] - simplex[1];
		float3 bc  = simplex[2] - simplex[1];
		float3 bdc = math.cross(bd, bc);
		
		if(math.dot(ao, bdc) < 0) // Origin below bottom triangle face.
		{
			closest_pt_to_origin = ClosestPointOnTriangleToPoint(simplex[0], simplex[2], simplex[1], 0, 2, 1, float3.zero, 
				out var barycentric_coords);
			
			cached_p0 = simplex.ps[0] * barycentric_coords[0] +
						simplex.ps[2] * barycentric_coords[1] +
						simplex.ps[1] * barycentric_coords[2];

			cached_p1 = simplex.qs[0] * barycentric_coords[0] +
						simplex.qs[2] * barycentric_coords[1] +
						simplex.qs[1] * barycentric_coords[2];
			
			return barycentric_coords.x >= 0f && barycentric_coords.y >= 0f && barycentric_coords.z >= 0f;
		}
		
		if(math.dot(ao, math.cross(ab, ac)) >= 0) // Origin infront of ABC.
		{
			closest_pt_to_origin = ClosestPointOnTriangleToPoint(simplex[0], simplex[1], simplex[2], 0, 1, 2, float3.zero, 
				out var barycentric_coords);
			
			cached_p0 = simplex.ps[0] * barycentric_coords[0] +
						simplex.ps[1] * barycentric_coords[1] +
						simplex.ps[2] * barycentric_coords[2];

			cached_p1 = simplex.qs[0] * barycentric_coords[0] +
						simplex.qs[1] * barycentric_coords[1] +
						simplex.qs[2] * barycentric_coords[2];
			
			return barycentric_coords.x >= 0f && barycentric_coords.y >= 0f && barycentric_coords.z >= 0f;
		}

		if(math.dot(ao, math.cross(ac, ad)) >= 0) // Origin infront of triangle ACD.
		{
			closest_pt_to_origin = ClosestPointOnTriangleToPoint(simplex[0], simplex[2], simplex[3], 0, 2, 3, float3.zero, 
				out var barycentric_coords);
			
			cached_p0 = simplex.ps[0] * barycentric_coords[0] +
						simplex.ps[2] * barycentric_coords[1] +
						simplex.ps[3] * barycentric_coords[2];

			cached_p1 = simplex.qs[0] * barycentric_coords[0] +
						simplex.qs[2] * barycentric_coords[1] +
						simplex.qs[3] * barycentric_coords[2];
			
			return barycentric_coords.x >= 0f && barycentric_coords.y >= 0f && barycentric_coords.z >= 0f;
		}
		
		if(math.dot(ao, math.cross(ad, ab)) >= 0) // Origin infront of triangle ADB.
		{
			closest_pt_to_origin = ClosestPointOnTriangleToPoint(simplex[0], simplex[3], simplex[1], 0, 3, 1, float3.zero, 
				out var barycentric_coords);
			
			cached_p0 = simplex.ps[0] * barycentric_coords[0] +
						simplex.ps[3] * barycentric_coords[1] +
						simplex.ps[1] * barycentric_coords[2];

			cached_p1 = simplex.qs[0] * barycentric_coords[0] +
						simplex.qs[3] * barycentric_coords[1] +
						simplex.qs[1] * barycentric_coords[2];
			
			return barycentric_coords.x >= 0f && barycentric_coords.y >= 0f && barycentric_coords.z >= 0f;
		}
		
		// Origin within tetrahedron, this means we've penetrated the CSO.
		closest_pt_to_origin = simplex[0];
		return math.lengthsq(simplex[0]) < last_sqrdist;
	}
	
	private void 
	AddPtToSimplex(float3 pt, float3 ptsup0, float3 ptsup1)
	{
		simplex.Push(pt, ptsup0, ptsup1);
	}
	
	private void
	TetrahedronTest(ref float3 dir, float3 ao)
	{
		// Simplex is one of the triangles that the origin is in front of. We know the origin can't be below the triangle at this point.
		// This is almost the same test as the n = 3 case.
		
		float3 ab  = simplex[1] - simplex[0];
		float3 ac  = simplex[2] - simplex[0];
		float3 abc = math.cross(ab, ac);

		if(math.dot(ao, math.cross(ab, abc)) > 0) // Voronoi region of edge ab.
		{
			simplex.Set(simplex[0], simplex[1]);	  // Reset to line ab.
			dir = math.cross(ab, math.cross(ab, ao));
			return;
		}
		
		if(math.dot(ao, math.cross(abc, ac)) > 0) // Voronoi region of edge ac.
		{
			simplex.Set(simplex[0], simplex[2]);	  // Reset to line ac.
			dir = math.cross(ac, math.cross(ac, ao));
			return;
		}
		
		simplex.Set(simplex[0], simplex[1], simplex[2]);
		dir = abc;
	}
	
	public static float3 
	SphereSupport(ref CMP_SphereShape sph, float3 dir)
	{
		return dir * sph.radius;
	}
	
	// Refer to 'Real Time Collision Detection' by Christer Ericson, chapter 5.1, page 141.
	private float3
	ClosestPointOnTriangleToPoint(float3 t0, float3 t1, float3 t2, int i_t0, int i_t1, int i_t2, float3 pt, out float3 barycentric_coords)
	{
		// Check if p is in vertex region outside A
		var ab = t1 - t0;
		var ac = t2 - t0;
		var ap = pt - t0;

		var d1 = math.dot(ab, ap);
		var d2 = math.dot(ac, ap);

		float u, v, w;

		// Barycentric coordinates (1,0,0)
		if (d1 <= 0 && d2 <= 0)
		{
			simplex.Set(i_t0);
			barycentric_coords = new float3(1f, 0f, 0f);
			return t0;
		}

		// Check if p is in vertex region outside B
		var bp = pt - t1;
		var d3 = math.dot(ab, bp);
		var d4 = math.dot(ac, bp);

		// Barycentric coordinates (0,1,0)
		if (d3 >= 0 && d4 <= d3)
		{
			simplex.Set(i_t1);
			barycentric_coords = new float3(0f, 1f, 0f);
			return t1;
		}

		// Check if p is in edge region outside AB, if so return a projection of p onto AB
		var vc = (d1 * d4) - (d3 * d2);
		if (vc <= 0 && d1 >= 0 && d3 <= 0)
		{
			simplex.Set(i_t0, i_t1);
			
			// Barycentric coordinates (1-v, v, 0)
			v = d1 / (d1 - d3);
			
			barycentric_coords = new float3(1f - v, v, 0f);
			
			return t0 + (ab * v);
		}

		// Check if p is in vertex region outside C
		var cp = pt - t2;
		var d5 = math.dot(ab, cp);
		var d6 = math.dot(ac, cp);

		// Barycentric coordinates (0,0,1)
		if (d6 >= 0 && d5 <= d6)
		{
			simplex.Set(i_t2);
			barycentric_coords = new float3(0f, 0f, 1f);
			return t2;
		}

		// Check if p is in edge region of AC, if so return a projection of p onto AC
		var vb = (d5 * d2) - (d1 * d6);
		if (vb <= 0 && d2 >= 0 && d6 <= 0) 
		{
			simplex.Set(i_t0, i_t2);
			
			// Barycentric coordinates (1-v, 0, v)
			v = d2 / (d2 - d6);

			barycentric_coords = new float3(1f - v, 0f, v);
			
			return t0 + (ac * v);
		}

		// Check if p is in edge region of BC, if so return projection of p onto BC
		var va = (d3 * d6) - (d5 * d4);
		if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) 
		{
			simplex.Set(i_t1, i_t2);
			
			v = (d4 - d3) / ((d4 - d3) + (d5 - d6));

			barycentric_coords = new float3(0f, 1f - v, v);
			
			return t1 + (t2 - t1) * v;
		
		}
		
		// Pt is inside face region.
		simplex.Set(i_t0, i_t1, i_t2);
		
		float denom = 1.0f / (va + vb + vc);
		v = vb * denom;
		w = vc * denom;
		u = 1f - v - w;

		barycentric_coords = new float3(u, v, w);

		return t0 + ab * v + ac * w;
	}
	
	private unsafe struct Simplex
	{
		private InnerPts pts;
		public  InnerPts ps;
		public  InnerPts qs;
		
		public int num;
		
		public float3 this[int i] => pts[i];
		
		public bool
		ContainsPt(float3 v)
		{
			for(int i_pt = 0; i_pt < num; i_pt++)
			{
				if(math.lengthsq(pts[i_pt] - v) <= SWEEP_EPSILON)
				{
					return true;
				}
			}
			
			return false;
		}
		
		public void
		Push(float3 pt, float3 p, float3 q)
		{
			num = math.min(4, num + 1);
			
			for(int i_vert = num - 1; i_vert > 0; i_vert--)
			{
				pts[i_vert] = pts[i_vert - 1];
				ps[i_vert]  =  ps[i_vert - 1];
				qs[i_vert]  =  qs[i_vert - 1];
			}

			pts[0] = pt;
			ps[0]  = p;
			qs[0]  = q;
		}
		
		public void
		Push(float3 pt)
		{
			num = math.min(4, num + 1);
			
			for(int i_vert = num - 1; i_vert > 0; i_vert--)
			{
				pts[i_vert] = pts[i_vert - 1];
			}

			pts[0] = pt;
		}
		
		public void
		Set(float3 a, float3 b, float3 c, float3 d)
		{
			pts[0] = a;
			pts[1] = b;
			pts[2] = c;
			pts[3] = d;

			num = 4;
		}
		
		public void
		Set(int i_a, int i_b, int i_c, int i_d)
		{
			var ta = pts[i_a];
			var tb = pts[i_b];
			var tc = pts[i_c];
			var td = pts[i_d];
			pts[0] = ta;
			pts[1] = tb;
			pts[2] = tc;
			pts[3] = td;

			ta = ps[i_a];
			tb = ps[i_b];
			tc = ps[i_c];
			td = ps[i_d];
			ps[0] = ta;
			ps[1] = tb;
			ps[2] = tc;
			ps[3] = td;

			ta = qs[i_a];
			tb = qs[i_b];
			tc = qs[i_c];
			td = qs[i_d];
			qs[0] = ta;
			qs[1] = tb;
			qs[2] = tc;
			qs[3] = td;
			
			num = 4;
		}
		
		public void
		Set(float3 a, float3 b, float3 c)
		{
			pts[0] = a;
			pts[1] = b;
			pts[2] = c;

			num = 3;
		}
		
		public void
		Set(int i_a, int i_b, int i_c)
		{
			var ta = pts[i_a];
			var tb = pts[i_b];
			var tc = pts[i_c];
			pts[0] = ta;
			pts[1] = tb;
			pts[2] = tc;

			ta = ps[i_a];
			tb = ps[i_b];
			tc = ps[i_c];
			ps[0] = ta;
			ps[1] = tb;
			ps[2] = tc;

			ta = qs[i_a];
			tb = qs[i_b];
			tc = qs[i_c];
			qs[0] = ta;
			qs[1] = tb;
			qs[2] = tc;
			
			num = 3;
		}
		
		public void
		Set(float3 a, float3 b)
		{
			pts[0] = a;
			pts[1] = b;

			num = 2;
		}
		
		public void
		Set(int i_a, int i_b)
		{
			var ta = pts[i_a];
			var tb = pts[i_b];
			pts[0] = ta;
			pts[1] = tb;

			ta = ps[i_a];
			tb = ps[i_b];
			ps[0] = ta;
			ps[1] = tb;

			ta = qs[i_a];
			tb = qs[i_b];
			qs[0] = ta;
			qs[1] = tb;
			
			num = 2;
		}
		
		public void
		Set(float3 a)
		{
			pts[0] = a;

			num = 1;
		}
		
		public void
		Set(int i_a)
		{
			var ta = pts[i_a];
			pts[0] = ta;

			ta = ps[i_a];
			ps[0] = ta;

			ta = qs[i_a];
			qs[0] = ta;
			
			num = 1;
		}
		
		public void
		Clear()
		{
			num = 0;
		}
		
		public struct InnerPts
		{
			private float3 p0, p1, p2, p3;
			
			public float3 this[int i]
			{
				get
				{
					switch(i)
					{
						default:return p0;
						case 1: return p1;
						case 2: return p2;
						case 3: return p3;
					}
				}
				
				set
				{
					switch(i)
					{
						default: p0 = value; break;
						case 1:  p1 = value; break;
						case 2:  p2 = value; break;
						case 3:  p3 = value; break;
					}
				}
			}
		}
	}
}


//====
}
//====
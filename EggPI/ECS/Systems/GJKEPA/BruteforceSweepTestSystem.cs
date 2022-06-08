using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;

using EggPI.KinematicAgent;
using EggPI.Common;
using EggPI.Mathematics;
using EggPI.Nav;


//====
namespace EggPI.Collision
{
//====


[UpdateBefore(typeof(FixedUpdate))]
public unsafe class BruteforceSweepTestSystem : JobComponentSystem
{
	private NativeArrayOfLists<Entity> spatial_buckets;
	private NativeArrayOfLists<Entity> contact_pairs;
	
	private EntityArchetypeQuery agent_data_query;
	private EntityArchetypeQuery colworld_query;

	private const int MAX_PER_ENTITY_PAIR_TESTS = 256;
	
	[BurstCompile]
	private struct BucketPartitioningJob : IJobProcessComponentDataWithEntity<CMP_NavAgent, Position, CMP_CapsuleShape, CMP_MoveInput>
	{
		[ReadOnly]  private CMP_CollisionWorldSettings			   world_settings;
		[WriteOnly] private NativeArrayOfLists<Entity>.Concurrent buckets;
		[ReadOnly]  private int3 bucket_dims;

		[ReadOnly]  private float dt;
		
		public BucketPartitioningJob(CMP_CollisionWorldSettings world_settings, NativeArrayOfLists<Entity>.Concurrent buckets, 
			int3 bucket_dims, float dt)
		{
			this.world_settings = world_settings;
			this.buckets 	    = buckets;
			this.bucket_dims	= bucket_dims;
			this.dt = dt;
		}
		
		public void 
		Execute(Entity ent, int i_ent, ref CMP_NavAgent agt, ref Position pos, ref CMP_CapsuleShape cap, ref CMP_MoveInput input)
		{
			float3 start_pos = math.clamp(pos.Value - cap.radius, world_settings.min, world_settings.max);
			float3 end_pos	 = math.clamp(pos.Value + input.val.xnz() * agt.speed * dt + cap.radius, world_settings.min, world_settings.max);

			int3 bmin = (int3)math.floor((start_pos - world_settings.min) / world_settings.partition_size);
			int3 bmax = (int3)math.floor((end_pos   - world_settings.min) / world_settings.partition_size);

			int3 min = math.min(bmin, bmax);
			int3 max = math.max(bmin, bmax);
			
			for(int y = min.y; y <= max.y; y++)
			for(int z = min.z; z <= max.z; z++)
			for(int x = min.x; x <= max.x; x++)
			{
				if(y >= bucket_dims.y || x >= bucket_dims.x || z >= bucket_dims.z)
				{
					continue;
				}
				
				int i_arr = (x + (bucket_dims.x * z)) + (y * bucket_dims.x * bucket_dims.z);
					
				buckets.Add(i_arr, ent);
			}
		}
	}
	
	[BurstCompile]
	private struct GenerateContactPairsJob : IJobProcessComponentDataWithEntity<CMP_NavAgent, Position, CMP_CapsuleShape, CMP_MoveInput>
	{
		[ReadOnly] 
		private NativeArrayOfLists<Entity> buckets;

		private int3 bucket_dims;
		
		[ReadOnly]  private CMP_CollisionWorldSettings world_settings;
		[WriteOnly] private NativeArrayOfLists<Entity>.Concurrent pairs;
		[ReadOnly]  private float dt;
		
		[ReadOnly, NativeDisableContainerSafetyRestriction] private ComponentDataFromEntity<CMP_NavAgent>  	  agt_data;
		[ReadOnly, NativeDisableContainerSafetyRestriction] private ComponentDataFromEntity<Position> 	  	  pos_data;
		[ReadOnly, NativeDisableContainerSafetyRestriction] private ComponentDataFromEntity<CMP_MoveInput> 	  input_data;
		[ReadOnly, NativeDisableContainerSafetyRestriction] private ComponentDataFromEntity<CMP_CapsuleShape> cap_data;
		
		public GenerateContactPairsJob(NativeArrayOfLists<Entity> buckets, int3 bucket_dims, CMP_CollisionWorldSettings world_settings, 
			ComponentDataFromEntity<CMP_NavAgent> agt, ComponentDataFromEntity<Position> pos, ComponentDataFromEntity<CMP_MoveInput> input, 
			ComponentDataFromEntity<CMP_CapsuleShape> cap, NativeArrayOfLists<Entity>.Concurrent pairs, float dt)
		{
			this.buckets 	 = buckets;
			this.bucket_dims = bucket_dims;
			this.pairs   	 = pairs;
			this.dt	     	 = dt;
			this.agt_data 	 = agt;
			this.pos_data 	 = pos;
			this.input_data  = input;
			this.cap_data 	 = cap;
			
			this.world_settings = world_settings;
		}
		
		public void 
		Execute(Entity ent, int i_ent, ref CMP_NavAgent agt, ref Position pos, ref CMP_CapsuleShape cap, ref CMP_MoveInput input)
		{
			float3 start_pos = math.clamp(pos.Value - cap.radius, world_settings.min, world_settings.max);
			float3 end_pos	 = math.clamp(pos.Value + input.val.xnz() * agt.speed * dt + cap.radius, world_settings.min, world_settings.max);

			int3 bmin = (int3)math.floor((start_pos - world_settings.min) / world_settings.partition_size);
			int3 bmax = (int3)math.floor((end_pos   - world_settings.min) / world_settings.partition_size);
			
			int3 min = math.min(bmin, bmax);
			int3 max = math.max(bmin, bmax);
			
			var my_pos = pos.Value;
			var my_end = pos.Value + input.val.xnz() * agt.speed * dt + cap.radius;
			
			for(int y = min.y; y <= max.y; y++)
			for(int z = min.z; z <= max.z; z++)
			for(int x = min.x; x <= max.x; x++)
			{
				if(y >= bucket_dims.y || x >= bucket_dims.x || z >= bucket_dims.z) { continue; }
				
				int i_arr = (x + (bucket_dims.x * z)) + (y * bucket_dims.x * bucket_dims.z);

				var innerlen = math.min(MAX_PER_ENTITY_PAIR_TESTS, buckets.GetListLength(i_arr));
				for(int i_inner = 0; i_inner < innerlen; i_inner++)
				{
					var other_e   = buckets[i_arr, i_inner];
					var other_pos = pos_data[other_e].Value;
					
					// Don't test against self or if already penetrating the other capsule (undefined GJK sweep in that case).
					if(math.lengthsq(other_pos - pos.Value) < (cap.radius * cap.radius) || ent == other_e) { continue; } 
					
					var other_cap   = cap_data[other_e];
					var other_input = input_data[other_e].val.xnz();
					var other_agt   = agt_data[other_e];
					var other_end   = other_pos + other_input + other_agt.speed * dt + other_cap.radius;
					
					// Shitty test to cull collisions easily: Conjugate the position of the other capsule, expand it by its velocity and my
					// cap's velocity and test square distance <=
					var expanded_rad = cap.radius + other_cap.radius + math.lengthsq(my_pos - my_end) + math.lengthsq(other_pos - other_end);
				
					if(math.lengthsq(my_pos - other_pos) <= expanded_rad)
					{
						pairs.Add(i_ent, other_e);
					}
				}
			}
		}
	}
	
	[BurstCompile]
	private struct SweepContactsJob : IJobProcessComponentDataWithEntity<CMP_NavAgent, Position, CMP_MoveInput, CMP_CapsuleShape>
	{
		[ReadOnly] 
		private NativeArrayOfLists<Entity> pairs;
		
		[WriteOnly] private NativeArray<ConvexCastHit> hit_res;
		
		private float dt;

		[ReadOnly, NativeDisableContainerSafetyRestriction] private ComponentDataFromEntity<CMP_NavAgent>  	  agt_data;
		[ReadOnly, NativeDisableContainerSafetyRestriction] private ComponentDataFromEntity<Position> 	  	  pos_data;
		[ReadOnly, NativeDisableContainerSafetyRestriction] private ComponentDataFromEntity<CMP_MoveInput> 	  input_data;
		[ReadOnly, NativeDisableContainerSafetyRestriction] private ComponentDataFromEntity<CMP_CapsuleShape> cap_data;
		
		public SweepContactsJob(ComponentDataFromEntity<CMP_NavAgent> agt, 
			ComponentDataFromEntity<Position> pos, ComponentDataFromEntity<CMP_MoveInput> input, ComponentDataFromEntity<CMP_CapsuleShape> cap, 
			NativeArrayOfLists<Entity> pairs, NativeArray<ConvexCastHit> hit_res, float dt)
		{
			this.agt_data 	 = agt;
			this.pos_data 	 = pos;
			this.input_data  = input;
			this.cap_data 	 = cap;
			
			this.pairs   = pairs;
			this.hit_res = hit_res;
			this.dt		 = dt;
		}

		public void 
		Execute(Entity ent, int i_ent, ref CMP_NavAgent my_agt, ref Position my_pos, ref CMP_MoveInput my_input, ref CMP_CapsuleShape my_cap)
		{
			var closest_hit = new ConvexCastHit(float3.zero, 1f, float3.zero);
			var my_end 		= my_pos.Value + my_agt.speed * dt * my_input.val.xnz();

			var pairlen = pairs.GetListLength(i_ent);
			for(int i_pair = 0; i_pair < pairlen; i_pair++)
			{
				var other_e   = pairs[i_ent, i_pair];
				var other_pos = pos_data[other_e].Value;
				var other_cap = cap_data[other_e];
				
				// Don't test against self.
				if(math.lengthsq(other_pos - my_pos.Value) <= (my_cap.radius * my_cap.radius) || ent == other_e)
				{
					continue;
				}
					
				var other_input = input_data[other_e].val.xnz();
				var other_agt   = agt_data[other_e];
				var other_end   = other_pos + other_input * other_agt.speed * dt;
					
				var gjk = new GJK();
			
				if(gjk.SweepCapsuleCapsule(my_cap, my_pos.Value, my_end, other_cap, other_pos, other_end, out var hit))
				{					
					if(hit.time < closest_hit.time)
					{
						closest_hit = hit;
					}		
				}
			}
			
			hit_res[i_ent] = closest_hit;
		}
	}
	
	[BurstCompile]
	private struct MoveJob : IJobProcessComponentDataWithEntity<CMP_MoveInput, Position, CMP_NavAgent, CMP_CapsuleShape>
	{
		[ReadOnly, DeallocateOnJobCompletion] 
		private NativeArray<ConvexCastHit> hits;

		private CMP_CollisionWorldSettings world_settings;
		
		private float dt;
		
		public MoveJob(CMP_CollisionWorldSettings world_settings, NativeArray<ConvexCastHit> hits, float dt)
		{
			this.world_settings = world_settings;
			this.hits = hits;
			this.dt	  = dt;
		}
		
		public void 
		Execute(Entity ent, int i_ent, ref CMP_MoveInput input, ref Position pos, ref CMP_NavAgent agt, ref CMP_CapsuleShape cap)
		{
			if(math.lengthsq(input.val) < GJK.SWEEP_EPSILON) { return; }

			var hit = hits[i_ent];
			
			// Take move input, "step back" by the closest hit position + the normal at the position multiplied by an epsilon.
			float3 final_pos = pos.Value + (input.val.xnz() * agt.speed * dt * hit.time) + hit.norm * hit.pen_amt;
		
			final_pos = math.clamp(final_pos, world_settings.min, world_settings.max);

			pos.Value = final_pos;
		}
	}
	
	[BurstCompile]
	public struct ClearBuffersJob : IJob
	{
		private NativeArrayOfLists<Entity> spatial_buckets, contact_pairs;
		[DeallocateOnJobCompletion] private NativeArray<ArchetypeChunk> chunks;
		
		public ClearBuffersJob(NativeArrayOfLists<Entity> spatial_buckets, NativeArrayOfLists<Entity> contact_pairs, 
			NativeArray<ArchetypeChunk> chunks)
		{
			this.spatial_buckets = spatial_buckets;
			this.contact_pairs	 = contact_pairs;
			this.chunks			 = chunks;
		}
		
		public void 
		Execute()
		{
			spatial_buckets.Clear();
			contact_pairs.Clear();
		}
	}

	protected override void 
	OnCreateManager()
	{
		agent_data_query = new EntityArchetypeQuery()
		{
			None = Array.Empty<ComponentType>(),
			Any = Array.Empty<ComponentType>(),
			All = new[] 
			{ 
				typeof(CMP_NavAgent), typeof(Position), typeof(CMP_CapsuleShape), typeof(CMP_MoveInput), ComponentType.Create<CFLAG_NavAgent>() 
			}
		};

		colworld_query = new EntityArchetypeQuery()
		{
			None = Array.Empty<ComponentType>(),
			Any  = Array.Empty<ComponentType>(),
			All  = new[]
			{
				ComponentType.Create<CMP_CollisionWorldSettings>()
			}
		};
		
		spatial_buckets = new NativeArrayOfLists<Entity>(128, 128, Allocator.Persistent);
		contact_pairs   = new NativeArrayOfLists<Entity>(16384, 128, Allocator.Persistent);
	}

	protected override JobHandle 
	OnUpdate(JobHandle input_deps)
	{
//		var colchunks = EntityManager.CreateArchetypeChunkArray(colworld_query, Allocator.TempJob);
//		if(colchunks.Length < 1)
//		{
//			colchunks.Dispose();
//			return input_deps;
//		}
		
		
//		var world_settings = colchunks[0].GetNativeArray(EntityManager.GetArchetypeChunkComponentType<CMP_CollisionWorldSettings>(true))[0];
//		
//		colchunks.Dispose();
		
		var world_settings = new CMP_CollisionWorldSettings(new float3(-80f, 0f, -80f), new float3(80f, 10f, 80f), 4f);
		
		var agt_data   = GetComponentDataFromEntity<CMP_NavAgent>();
		var pos_data   = GetComponentDataFromEntity<Position>();
		var input_data = GetComponentDataFromEntity<CMP_MoveInput>();
		var cap_data   = GetComponentDataFromEntity<CMP_CapsuleShape>();
		
		var chunks = EntityManager.CreateArchetypeChunkArray(agent_data_query, Allocator.TempJob);
		
		var num_e  = ArchetypeChunkArray.CalculateEntityCount(chunks);

		var bucket_dims = math.max(new int3(1), (int3)math.ceil((world_settings.max - world_settings.min) / world_settings.partition_size));
		var num_buckets = bucket_dims.x * bucket_dims.y * bucket_dims.z;
		
		if(spatial_buckets.Length < num_buckets)
		{
			spatial_buckets.Dispose();
			spatial_buckets = new NativeArrayOfLists<Entity>(bucket_dims.x * bucket_dims.y * bucket_dims.z, 128, Allocator.Persistent);	
		}
		
		if(contact_pairs.Length < num_e)
		{
			contact_pairs.Dispose();
			contact_pairs = new NativeArrayOfLists<Entity>(num_e, 128, Allocator.Persistent);
		}

		var hits = new NativeArray<ConvexCastHit>(num_e, Allocator.TempJob);

		float dt = Time.deltaTime;
		
		var cull_job  = new BucketPartitioningJob(world_settings, spatial_buckets.ToConcurrent(), bucket_dims, dt);
		var pair_job  = new GenerateContactPairsJob(spatial_buckets, bucket_dims, world_settings, agt_data, pos_data, input_data, cap_data,
			contact_pairs.ToConcurrent(), dt);
		var sweep_job = new SweepContactsJob(agt_data, pos_data, input_data, cap_data, contact_pairs, hits, dt);
		var movejob   = new MoveJob(world_settings, hits, dt);
		var cleanjob  = new ClearBuffersJob(spatial_buckets, contact_pairs, chunks);

		var cull_job_handle  = cull_job.Schedule(this, input_deps);
		var pair_job_handle  = pair_job.Schedule(this, cull_job_handle);
		var sweep_job_handle = sweep_job.Schedule(this, pair_job_handle);
		var move_job_handle  = movejob.Schedule(this, sweep_job_handle);

		return cleanjob.Schedule(move_job_handle);
	}

	protected override void 
	OnDestroyManager()
	{
		spatial_buckets.Dispose();
		contact_pairs.Dispose();
	}
}


//====
}
//====
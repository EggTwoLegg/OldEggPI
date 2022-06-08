//#define DEBUG_HML
 
#if DEBUG_HML
using UnityEngine;
#endif
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;


//====
namespace E7.Native
{
//====
	
	
/// <summary>
/// A hybrid of NativeHashMap and NativeList. You can both use int indexer or use the key at the same time.
/// Consumes more memory and slightly costly remove operation.
/// </summary>
public unsafe struct NativeHashMapList<TKey, TValue> : IDisposable
	where TKey : struct, IEquatable<TKey>
	where TValue : struct
{
	/// <summary>
	/// Maps a hashed key to an index that then get the data from `NativeList`
	/// </summary>
	private NativeHashMap<TKey, int> hashMap;

	/// <summary>
	/// The data storage, allows us to use `int` to enumerate it.
	/// </summary>
	private NativeList<TValue> list;

	/// <summary>
	/// For backward referencing to the hash map when removing by index.
	/// </summary>
	private NativeList<TKey> keyList;

	public Allocator allocator { get; }

	public NativeHashMapList(int capacity, Allocator label)
	{
		hashMap = new NativeHashMap<TKey, int>(capacity, label);
		list = new NativeList<TValue>(capacity, label);
		keyList = new NativeList<TKey>(capacity, label);
		allocator = label;
	}

	public void Dispose()
	{
		hashMap.Dispose();
		list.Dispose();
		keyList.Dispose();
	}

	public void Clear()
	{
		hashMap.Clear();
		list.Clear();
		keyList.Clear();
	}

	/// <summary>
	/// index is an int as in NativeList not the key as in NativeHashMap sense.
	/// For key use TryGetValue/TrySetValue.
	/// </summary>
	public TValue this[int index]
	{
		get => list[index];
		set => list[index] = value;
	}

	public int Length => list.Length;

	public bool TryGetValue(TKey key, out TValue value)
	{
		if (hashMap.TryGetValue(key, out int outIndex))
		{
#if DEBUG_HML
			Debug.Log($"Key {key} at {outIndex} !");
#endif
			value = list[outIndex];
#if DEBUG_HML
			Debug.Log($"Value is {value} !");
#endif
			return true;
		}
		value = default;
		return false;
	}

	public bool TrySetValue(TKey key, TValue value)
	{
#if DEBUG_HML
		Debug.Log($"Try set {key} to {value}");
#endif
		if (hashMap.TryGetValue(key, out int outIndex))
		{
#if DEBUG_HML
			Debug.Log($"Setting at index {outIndex}");
#endif
			list[outIndex] = value;
			return true;
		}
		return false;
	}

	public bool TryAdd(TKey key, TValue value)
	{
		if (hashMap.TryAdd(key, list.Length))
		{
			list.Add(value);
			keyList.Add(key);
#if DEBUG_HML
			Debug.Log($"Added {value} and key {key}");
#endif
			return true;
		}
		return false;
	}

	/// <summary>
	/// Change one key to other while keeping the same destination data.
	/// </summary>
	public bool TryChangeKey(TKey oldKey, TKey newKey)
	{
		if (hashMap.TryGetValue(oldKey, out int remember))
		{
#if DEBUG_HML
			Debug.Log($"Changing key {oldKey} {remember}");
#endif
			hashMap.Remove(oldKey);
			if (hashMap.TryAdd(newKey, remember))
			{
#if DEBUG_HML
				Debug.Log($"Changing to key {newKey} {remember}");
#endif
				//Now update the corresponding keyList entry as well
				keyList[remember] = newKey;
				return true;
			}
		}
		return false;
	}

	public void RemoveByKey(TKey key)
	{
		int backIndex = list.Length - 1;

		if (!hashMap.TryGetValue(key, out int indexToRemove))
		{
			throw new System.Exception($"{key} does not exist in NativeHashMapList!");
		}

		bool swapUpdateNecessary = indexToRemove != backIndex;
		TKey backKey = swapUpdateNecessary ? keyList[backIndex] : default;

#if DEBUG_HML
		Debug.Log($"Removing by key {key} index {indexToRemove}");
#endif

		list.RemoveAtSwapBack(indexToRemove);
		keyList.RemoveAtSwapBack(indexToRemove);
		hashMap.Remove(key);

		if (swapUpdateNecessary)
		{
#if DEBUG_HML
			Debug.Log($"Swapping back key {backKey} from {backIndex} to {indexToRemove}");
#endif

			//Need to update the dict entry that replace the swapped item.
			hashMap.Remove(backKey);
			//If remove success, TryAdd must success.
			if (!hashMap.TryAdd(backKey, indexToRemove))
			{
				throw new System.Exception($"Why can RemoveByKey fail here???");
			}
		}
	}

	public void 
	RemoveAtSwapBack(int index)
	{
		TKey backKey = keyList[list.Length - 1];
		TKey removingKey = keyList[index];

		list.RemoveAtSwapBack(index);
		keyList.RemoveAtSwapBack(index);
		hashMap.Remove(removingKey);

		//Need to update the dict entry that results in the just-swapped index.
		hashMap.Remove(backKey);
		//If remove success, TryAdd must success.
		if (!hashMap.TryAdd(backKey, index))
		{
			throw new System.Exception($"Why can RemoveAtSwapBack fail here???");
		}
	}
	
	public NativeArray<TValue>
	GetValues()
	{
		return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<TValue>(list.GetUnsafePtr(), Length, allocator);
	}
	
	[NativeContainer]
	public struct Concurrent
	{
		
	}
}
	
	
//====
}
//====
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI
{
//====


public static class NativeListExtensions
{
	public static void 
	Push<T>(this NativeList<T> list, T elem) where T : struct
	{
		for(int i_elem = list.Length; i_elem > 0; i_elem--)
		{
			list[i_elem] = list[i_elem--];
		}

		list[0] = elem;
	}
	
	public static void 
	Push<T>(this NativeList<T> list, T elem, int i_max) where T : struct
	{
		int max = math.min(i_max, list.Length);
		
		for(int i_elem = max; i_elem > 0; i_elem--)
		{
			list[i_elem] = list[i_elem--];
		}

		list[0] = elem;
	}
}


//====
}
//====
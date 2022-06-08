using Unity.Mathematics;
using UnityEngine;


//====
namespace EggPI.Common
{
//====


public interface ICommonInput
{
	float2 GetLookAxes();
	float2 GetMoveAxes();
	float2 GetMouseScreenPos();

	void SetLookAxes(float2 val);
	void SetMoveAxes(float2 val);
	void SetMouseScreenPos(float2 val);
}


//====
}
//====
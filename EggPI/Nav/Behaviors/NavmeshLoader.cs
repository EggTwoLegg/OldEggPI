using System.IO;
using UnityEngine;


//====
namespace EggPI.Nav
{
//====


public class NavmeshLoader : MonoBehaviour
{
	[SerializeField] private string mesh_uri;
	[SerializeField, Range(0, 8)] private int navmesh_id;
	
	private void
	Start()
	{
		string fullpath = Application.dataPath + "/" + mesh_uri;
		
		if(!File.Exists(fullpath))
		{
			throw new FileNotFoundException($"The Navmesh data requested at {fullpath} does not exist!");
		}		

		Navmesh.RegisterWithId(Navmesh.LoadFromDisk(fullpath), navmesh_id);
	}
}


//====
}
//====
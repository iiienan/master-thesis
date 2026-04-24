using UnityEngine;

public class Plane : MonoBehaviour {

	public Vector3 getLengths() {
		Bounds b =  GetComponent<MeshFilter> ().sharedMesh.bounds;
		float x = transform.localScale.x*b.size.x;
		float y = transform.localScale.y*b.size.y;
		float z = transform.localScale.z*b.size.z;
		Vector3 ret = new Vector3 (x, y, z);
		return ret;
	}
}

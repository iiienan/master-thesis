using UnityEngine;
using System.Collections;

public class CustomNodeLined : CustomNode {

	public override Vector3 getTargetPoint(Vector3 origin, int agentID = 0) {

		Vector3 A = transform.TransformPoint(new Vector3 (0.5f, 0, 0));
		Vector3 B = transform.TransformPoint (new Vector3 (-0.5f, 0, 0));
		Vector3 AB = B - A;
		float dis = Vector3.Dot ((origin - A), AB) / AB.sqrMagnitude;
		if (dis < 0) {
			return A;
		} else if (dis > 1) {
			return B;
		} else {
			return A + AB * dis;
		}
	}
}

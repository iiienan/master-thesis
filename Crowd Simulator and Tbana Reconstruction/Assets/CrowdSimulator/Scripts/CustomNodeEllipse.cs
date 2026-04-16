using UnityEngine;


public class CustomNodeEllipse : CustomNode
{
    public override bool IsAgentInsideArea(Vector3 agentPosition)
	{
        Vector3 localPos = transform.InverseTransformPoint(agentPosition);
		float x = localPos.x;
		float z = localPos.z;

		return (x * x) / (0.5f * 0.5f) + (z * z) / (0.5f * 0.5f) <= 1f;
	}
    
    public override Vector3 getTargetPoint(Vector3 origin, int agentID)
    {
        float scaleX = transform.lossyScale.x;
        float scaleZ = transform.lossyScale.z;

        Vector3 localOrigin = transform.InverseTransformPoint(origin);

        Vector3 dir;
        if (scaleX >= scaleZ)
        {
            dir = new Vector3(1, 0, 0);
        }
        else
        {
            dir = new Vector3(0, 0, 1);
        }

        float offset = (Mathf.Abs(agentID * 31) % 1000) / 1000f - 0.5f;
        
        Vector3 target = dir * offset;
        return transform.TransformPoint(target);
    }
    
}

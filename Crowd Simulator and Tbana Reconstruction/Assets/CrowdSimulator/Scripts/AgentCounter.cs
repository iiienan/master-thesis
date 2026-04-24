using UnityEngine;

public class AgentCounter : MonoBehaviour
{
    public Main main;
    public WaitingAreaController waitingAreaController;
    // Start is called before the first frame update

    private void OnDrawGizmos()
	{
        if(main.agentList == null || waitingAreaController.waitingAgents == null)
        {
            return;
        }
        int nAgents = main.agentList.Count;
		UnityEditor.Handles.color = Color.red;
		UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, nAgents.ToString());
		
	}
}

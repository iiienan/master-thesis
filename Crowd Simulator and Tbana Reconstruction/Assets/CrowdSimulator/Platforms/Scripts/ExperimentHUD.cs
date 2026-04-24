using UnityEngine;
using TMPro; // Remove if using standard UI Text

public class ExperimentHUD : MonoBehaviour
{
    public TextMeshProUGUI hudText;
    
    private float simulationTime = 0f;
    internal float realTimeStart;
    internal float smoothedFPS = 60f;

    void Start()
    {
        realTimeStart = Time.realtimeSinceStartup;
    }

    public void RegisterSimTick()
    {
        simulationTime += SimulationGrid.instance.dt;
    }

    void Update()
    {
        // Real World Time
        float elapsedRealTime = Time.realtimeSinceStartup - realTimeStart;

        // Frame Rate
        float fps = 1.0f / Time.unscaledDeltaTime;
        smoothedFPS = Mathf.Lerp(smoothedFPS, fps, 0.05f);

        hudText.text = string.Format(
            "Sim Time: {0:F0}s\n" +
            "Real Time: {1:F0}s\n" +
            "FPS: {2:F0}",
            simulationTime, elapsedRealTime, smoothedFPS);
            
        if (elapsedRealTime > simulationTime + 0.5f) 
        {
            hudText.color = Color.yellow;
        }
        else
        {
            hudText.color = Color.white;
        }
    }
}
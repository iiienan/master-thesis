using System.Collections;
using UnityEngine;
using System.Globalization;

public class LOSVisualizer : MonoBehaviour
{
    private Vector2 areaSize;
    public float cellSize = 1.0f; 
    private GameObject[,] gridCells;
    private Main mainScript;
    public Gradient losGradient;
    private const float maxDensity = 5.26f;
    private float[,] densityMap;
    public int smoothingRadius = 1; // 1 = 3x3, 2 = 5x5

    void Start()
    {
        mainScript = FindObjectOfType<Main>();
        if(mainScript == null)
        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }
        areaSize = new Vector2(mainScript.planeSizeX*10, mainScript.planeSizeZ*10);
        CreateGrid();
    }

    void CreateGrid()
    {
        int cols = Mathf.CeilToInt(areaSize.x / cellSize);
        int rows = Mathf.CeilToInt(areaSize.y / cellSize);
        gridCells = new GameObject[cols, rows];
        densityMap = new float[cols, rows];

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Vector3 gridOrigin = new Vector3(-areaSize.x / 2f, 0.01f, -areaSize.y / 2f);
                cell.transform.position = new Vector3(
                    gridOrigin.x + x * cellSize + cellSize / 2f,
                    0.01f,
                    gridOrigin.z + y * cellSize + cellSize / 2f
                );
                cell.transform.localScale = new Vector3(cellSize, cellSize, 1);
                cell.transform.rotation = Quaternion.Euler(90, 0, 0); // Face up
                Destroy(cell.GetComponent<Collider>());
                gridCells[x, y] = cell;
                cell.transform.SetParent(transform);
                cell.GetComponent<Renderer>().enabled = false;
            }
        }
    }

public void UpdateLOS()
    {
        int cols = gridCells.GetLength(0);
        int rows = gridCells.GetLength(1);
        System.Array.Clear(densityMap, 0, densityMap.Length);

        foreach (Agent agent in mainScript.agentList)
        {
            Vector3 pos = agent.tr.position;
            int x = Mathf.FloorToInt((pos.x + areaSize.x / 2f) / cellSize);
            int y = Mathf.FloorToInt((pos.z + areaSize.y / 2f) / cellSize);

            if (x >= 0 && x < cols && y >= 0 && y < rows)
            {
                densityMap[x, y] += 1.0f;
            }
        }
        
        // Assign gradient colors based on smoothed density
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                float smoothedDensity = GetSmoothedDensity(x, y, cols, rows);
                float normalized = Mathf.Clamp01(smoothedDensity / maxDensity);
                Color color = losGradient.Evaluate(normalized);
                gridCells[x, y].GetComponent<Renderer>().material.color = color;
            }
        }
    }

    public void takeScreenshot()
    {
        SetGridVisibility(true);

        string fileName = mainScript.logger.scenarioPrefix + "_"
                        + "_"
                        + mainScript.simulationTime.ToString("F2", CultureInfo.InvariantCulture)
                        + ".png";

        string fullPath     = System.IO.Path.Combine(Application.persistentDataPath, fileName);

        string dir = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        ScreenCapture.CaptureScreenshot(fullPath);
        StartCoroutine(HideAfterFrame());
    }

    float GetSmoothedDensity(int cx, int cy, int cols, int rows)
    {
        float total = 0;
        float weightSum = 0;
        float sigma = smoothingRadius / 2f;

        for (int dx = -smoothingRadius; dx <= smoothingRadius; dx++)
        {
            for (int dy = -smoothingRadius; dy <= smoothingRadius; dy++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (x >= 0 && x < cols && y >= 0 && y < rows)
                {
                    float weight = Mathf.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                    total += densityMap[x, y] * weight;
                    weightSum += weight;
                }
            }
        }

        return (total / weightSum) / (cellSize * cellSize);
    }

    public void SetGridVisibility(bool isVisible)
    {
        
        if (gridCells == null) return;

        int cols = gridCells.GetLength(0);
        int rows = gridCells.GetLength(1);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                if (gridCells[x, y] != null)
                {
                    gridCells[x, y].GetComponent<Renderer>().enabled = isVisible;
                }
            }
        }
    }

    private IEnumerator HideAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        SetGridVisibility(false);
    }

}

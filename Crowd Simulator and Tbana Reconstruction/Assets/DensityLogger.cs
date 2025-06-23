// This script will measure and log the density of specified platform areas
// based on the current platform typology.
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text; // For StringBuilder
using System.Globalization;

// --- IMPORTANT: Placeholder for user's existing classes/enums ---
// If your TrainController and Main classes are not static, you might need to adjust
// how they are accessed (e.g., using a singleton pattern).
// Ensure your Agent class has a 'transform' property to access its position.

// Example of what your TrainController might look like:
// public static class TrainController
// {
//     public enum PlatformType { Central, Mixed, Side }
//     public static PlatformType platformType { get; set; } = PlatformType.Central; // Set your initial platform type here
// }

// Example of what your Agent class might look like:
// public class Agent : MonoBehaviour // Or just a plain class if it holds a Transform
// {
//     // Assuming agent has a transform component attached to its GameObject
//     // If Agent is just a class, ensure it holds a reference to a Transform
//     // public Transform transform; // Example if Agent is not a MonoBehaviour
//     // Other agent properties...
// }

// Example of what your Main class might look like:
// public static class Main
// {
//     public static List<Agent> agentList = new List<Agent>(); // Populate this list with your active agent instances
// }

public class DensityLogger : MonoBehaviour
{
    // --- Configuration ---
    [Header("Logging Settings")]
    [Tooltip("Name of the CSV file. Will be stored in Application.persistentDataPath.")]
    public string fileName = "PlatformDensityLog.csv";
    [Tooltip("How often to log data (in seconds).")]
    public float logInterval = 1.0f; // Log data every 1 second
    public TrainController trainController;
    public Main main;

    // Platform dimensions (fixed as per user request)
    private const float PlatformLength = 150f;
    private const float PlatformZMin = -75f; // Assuming platform is centered at Z=0
    private const float PlatformZMax = 75f;  // so it spans from -75 to +75

    // Internal state
    private string filePath;
    private float timer;

    // Helper class to define and manage each measurement area
    private class PlatformMeasurementArea
    {
        public string Name { get; private set; }
        public float MinX { get; private set; }
        public float MaxX { get; private set; }
        public float AreaSqMeters { get; private set; }
        public int CurrentPassengerCount { get; set; }

        public PlatformMeasurementArea(string name, float minX, float maxX, float length)
        {
            Name = name;
            MinX = minX;
            MaxX = maxX;
            AreaSqMeters = Mathf.Abs(maxX - minX) * length; // Calculate area based on width and length
            CurrentPassengerCount = 0;
        }

        public float GetDensity()
        {
            if (AreaSqMeters <= 0) return 0f;
            return (float)CurrentPassengerCount / AreaSqMeters;
        }

        // Determines LOS grade based on density thresholds from the paper
        public string GetLOSGrade()
        {
            float density = GetDensity();
            if (density < 0.83f) return "A";
            else if (density < 1.08f) return "B";
            else if (density < 1.54f) return "C";
            else if (density < 3.57f) return "D";
            else if (density < 5.26f) return "E";
            else return "F";
        }

        public void ResetCount()
        {
            CurrentPassengerCount = 0;
        }
    }

    private List<PlatformMeasurementArea> currentAreas;

    void Awake()
    {
        // Construct the full file path
        filePath = Path.Combine(Application.persistentDataPath, fileName);
        Debug.Log($"Logging data to: {filePath}");

        // Write header only if the file doesn't exist or is empty
        // This ensures the header is written once per simulation run if file is new or cleared
        if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
        {
            WriteHeader();
        }

        // Initialize platform areas based on the current platform type
        InitializePlatformAreas();
    }

    void Update()
    {
        // Increment timer and log data when interval is met
        timer += Time.deltaTime;
        if (timer >= logInterval)
        {
            LogPlatformDensities();
            timer = 0f; // Reset timer for next interval
        }
    }

    private void WriteHeader()
    {
        try
        {
            // Use 'false' in StreamWriter to overwrite the file and write a new header
            using (StreamWriter sw = new StreamWriter(filePath, false))
            {
                sw.WriteLine("Timestamp,AreaName,PassengerCount,Density,LOSGrade");
            }
            Debug.Log("CSV Header written successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error writing CSV header: {e.Message}");
        }
    }

    private void InitializePlatformAreas()
    {
        currentAreas = new List<PlatformMeasurementArea>();

        // Get the current platform type from TrainController
        // IMPORTANT: Ensure TrainController.platformType is accessible here.
        // If TrainController is a singleton, you might need TrainController.Instance.platformType
        TrainController.PlatformType type = trainController.platformType; 

        Debug.Log($"Initializing areas for Platform Type: {type}");

        // Define areas based on the specified X-coordinates and platform type
        switch (type)
        {
            case TrainController.PlatformType.Central:
                currentAreas.Add(new PlatformMeasurementArea("Central_Line1", 0f, 9f, PlatformLength));
                currentAreas.Add(new PlatformMeasurementArea("Central_Line2", -9f, 0f, PlatformLength));
                break;
            case TrainController.PlatformType.Mixed:
                currentAreas.Add(new PlatformMeasurementArea("Mixed_Line1", 6f, 12f, PlatformLength));
                currentAreas.Add(new PlatformMeasurementArea("Mixed_Line2", -12f, -6f, PlatformLength));
                currentAreas.Add(new PlatformMeasurementArea("Mixed_Middle", -3f, 3f, PlatformLength));
                break;
            case TrainController.PlatformType.Side:
                currentAreas.Add(new PlatformMeasurementArea("Side_Line1", 3f, 12f, PlatformLength));
                currentAreas.Add(new PlatformMeasurementArea("Side_Line2", -12f, -3f, PlatformLength));
                break;
            default:
                Debug.LogWarning("Unknown Platform Type. No areas initialized. Check TrainController.platformType.");
                break;
        }

        // Log the details of the initialized areas for verification in the Unity Console
        foreach (var area in currentAreas)
        {
            Debug.Log($"Initialized Area: {area.Name}, X Range: [{area.MinX}, {area.MaxX}], Area: {area.AreaSqMeters} m²");
        }
    }

    private void LogPlatformDensities()
    {
        // Reset passenger counts for all areas at the start of each logging interval
        foreach (var area in currentAreas)
        {
            area.ResetCount();
        }

        // Get the list of agents from Main.agentList
        // IMPORTANT: Ensure Main.agentList is accessible and populated
        if (main.agentList == null || main.agentList.Count == 0)
        {
            // Debug.Log("No agents found in Main.agentList or list is null. Skipping density logging.");
            return;
        }

        // Iterate through all agents and assign them to the correct platform area
        foreach (var agent in main.agentList)
        {
            // Basic null check for agent and its transform
            if (agent == null || agent.transform == null)
            {
                // Debug.LogWarning("Encountered a null agent or an agent without a transform. Skipping.");
                continue;
            }

            Vector3 agentPos = agent.transform.position;

            // Check if agent's Z position is within the defined platform length
            if (agentPos.z >= PlatformZMin && agentPos.z <= PlatformZMax)
            {
                // Iterate through defined areas to find where the agent belongs
                foreach (var area in currentAreas)
                {
                    // Check if agent's X position falls within the current area's X-range
                    // We handle both min <= x <= max and max <= x <= min scenarios
                    // Mathf.Min and Mathf.Max ensure correct range comparison regardless of minX/maxX order
                    if (agentPos.x >= Mathf.Min(area.MinX, area.MaxX) && agentPos.x <= Mathf.Max(area.MinX, area.MaxX))
                    {
                        area.CurrentPassengerCount++;
                        break; // Agent found in an area, move to the next agent
                    }
                }
            }
        }

        // Write the collected data for the current interval to the CSV file
        try
        {
            // Use 'true' in StreamWriter to append data to the existing file
            using (StreamWriter sw = new StreamWriter(filePath, true))
            {
                string timestamp = Time.time.ToString("F2"); // Current simulation time, formatted

                foreach (var area in currentAreas)
                {
                    StringBuilder line = new StringBuilder();
                    line.Append(timestamp).Append(",");
                    line.Append(area.Name).Append(",");
                    line.Append(area.CurrentPassengerCount).Append(",");
                    line.Append(area.GetDensity().ToString("F3", CultureInfo.InvariantCulture)).Append(",");
                    line.Append(area.GetLOSGrade());

                    sw.WriteLine(line.ToString());
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error logging platform densities: {e.Message}");
        }
    }

    // This method is called when the application quits or stops playing in the editor.
    // It's good practice to ensure all file operations are completed.
    void OnApplicationQuit()
    {
        Debug.Log("Application quitting. Data logging complete and file flushed.");
    }
}
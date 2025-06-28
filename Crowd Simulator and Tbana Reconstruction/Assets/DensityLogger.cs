
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization; // Required for CultureInfo.InvariantCulture

// --- IMPORTANT: Placeholder for user's existing classes/enums ---
// If your TrainController and Main classes are not static, you might need to adjust
// how they are accessed (e.g., using a singleton pattern).
// Ensure your Agent class has a 'transform' property to access its position.

// Example of what your TrainController might look like:
// public static class TrainController
// {
//     public enum PlatformType { Central, Mixed, Side }
//     public static PlatformType platformType { get; set; } = PlatformType.Central; // Set your initial platform type here for testing
// }

// Example of what your Agent class might look like:
// public class Agent : MonoBehaviour // Or just a plain class if it holds a reference to a Transform
// {
//     // Assuming agent has a transform component attached to its GameObject
//     // If Agent is just a class, ensure it holds a reference to a Transform, e.g.:
//     // public Transform transform; 
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
    public string fileName = "PlatformDensityLog";
    [Tooltip("How often to log data (in seconds).")]
    public float logInterval = 1.0f; // Log data every 1 second
    public TrainController trainController; // Reference to the TrainController to get platform type
    public Main main; // Reference to the Main class to access agent list

    // Platform dimensions (fixed as per user request)
    private const float PlatformLength = 150f;
    private const float PlatformZMin = -75f; // Assuming platform is centered at Z=0
    private const float PlatformZMax = 75f;  // so it spans from -75 to +75

    // Stair Access Point dimensions
    // Z-positions where stairs start
    private static readonly float[] StairZPositions = { -50f, -20f, 20f, 50f };
    // Z-depth for each stair access area (as requested: 3 meters in Z-direction)
    private const float StairAccessZDepth = 3f; 

    // Train Door dimensions
    private const float TrainDoorWidth = 1.5f; // This remains the physical Z-dimension of the door
    // New constants for the *measurement area* dimensions around each train door
    private const float DoorMeasurementXWidth = 2.0f; // As requested: 2 meters in X-direction
    private const float DoorMeasurementZDepth = 3.0f; // As requested: 3 meters in Z-direction

    private const float FirstDoorCenterZ = -67.5f;
    private const float LastDoorCenterZ = 67.5f;
    private const float TrainDoorZSpacing = 5.0f; // Distance between door centers
    // Note: Given FirstDoorCenterZ, LastDoorCenterZ, and TrainDoorZSpacing, there are 28 doors per side.
    // This is consistent with the user's clarification that 56 doors is counting both sides of the train.


    // Internal state
    private string filePath;
    private float timer;
    private StreamWriter writer;

    // Helper class to define and manage each measurement area
    private class PlatformMeasurementArea
    {
        public string Name { get; private set; }
        public float MinX { get; private set; }
        public float MaxX { get; private set; }
        public float MinZ { get; private set; }
        public float MaxZ { get; private set; }
        public float AreaSqMeters { get; set; } // Changed to public set to allow adjustment
        public int CurrentPassengerCount { get; set; }

        // Updated constructor to include Z-range
        public PlatformMeasurementArea(string name, float minX, float maxX, float minZ, float maxZ)
        {
            Name = name;
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            // Area calculation now uses both X (width) and Z (depth/length) ranges
            AreaSqMeters = Mathf.Abs(maxX - minX) * Mathf.Abs(maxZ - minZ);
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
            return GetLOSGradeForDensity(density);
        }

        // Helper method to get LOS grade for a given density value
        public string GetLOSGradeForDensity(float density)
        {
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
        StringBuilder sb = new StringBuilder();
        sb.Append(trainController.platformType.ToString());
        sb.Append(trainController.flow.ToString());
        sb.Append(trainController.nAgents.ToString());

        if(trainController.alightBeforeBoarding)
        {
            sb.Append("AB");
        }

        fileName = fileName + sb.ToString() + ".csv";
        // Construct the full file path
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            writer = new StreamWriter(filePath, false); // Overwrite the file
            WriteHeader(); // Write column names once
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open log file: {e.Message}");
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
        if (writer == null)
        {
            Debug.LogError("Writer is not initialized. Cannot write header.");
            return;
        }

        writer.WriteLine("Timestamp,AreaName,PassengerCount,Density,LOSGrade");
    }

    private void InitializePlatformAreas()
    {
        currentAreas = new List<PlatformMeasurementArea>();

        // Get the current platform type from TrainController
        // IMPORTANT: Ensure trainController is assigned in the Inspector or via script
        if (trainController == null)
        {
            Debug.LogError("TrainController reference is not set in DensityLogger. Cannot initialize areas.");
            return;
        }
        TrainController.PlatformType type = trainController.platformType; 

        // Calculate half dimension for Z-depth of door measurement areas
        float halfDoorMeasurementZDepth = DoorMeasurementZDepth / 2f; // 1.5f

        // Define areas based on the specified X-coordinates and platform type
        switch (type)
        {
            case TrainController.PlatformType.Central:
                // Add stair access areas first (more specific)
                foreach (float zPos in StairZPositions)
                {
                    float minZ, maxZ;
                    // Determine Z-range based on direction (outwards/inwards)
                    if (zPos == -50f) // Outwards (away from 0)
                    {
                        minZ = zPos - StairAccessZDepth;
                        maxZ = zPos;
                    }
                    else if (zPos == 50f) // Outwards (away from 0)
                    {
                        minZ = zPos;
                        maxZ = zPos + StairAccessZDepth;
                    }
                    else if (zPos == -20f) // Inwards (towards 0)
                    {
                        minZ = zPos;
                        maxZ = zPos + StairAccessZDepth;
                    }
                    else if (zPos == 20f) // Inwards (towards 0)
                    {
                        minZ = zPos - StairAccessZDepth;
                        maxZ = zPos;
                    }
                    else // Fallback for unexpected Z positions
                    {
                        minZ = zPos - StairAccessZDepth / 2f;
                        maxZ = zPos + StairAccessZDepth / 2f;
                        Debug.LogWarning($"Unexpected stair Z position {zPos}. Using default centered depth for stair access.");
                    }
                    // Stair access X-range remains -3f to 3f (6m wide) for Central platform
                    currentAreas.Add(new PlatformMeasurementArea($"StairAccess_Central_Z{zPos}", -3f, 3f, minZ, maxZ));
                }
                
                // Add Train Door areas for Central Platform based on X positions -9 and 9
                float[] doorXPositions_Central = {-9f, 9f}; // -9 for Line 2, 9 for Line 1

                for (float zCenter = FirstDoorCenterZ; zCenter <= LastDoorCenterZ + 0.001f; zCenter += TrainDoorZSpacing)
                {
                    float doorMinZ = zCenter - halfDoorMeasurementZDepth;
                    float doorMaxZ = zCenter + halfDoorMeasurementZDepth;
                    
                    // Declare these variables here to ensure they are in scope for currentAreas.Add
                    float doorXMin_L1, doorXMax_L1;
                    float doorXMin_L2, doorXMax_L2;

                    // Line 2 doors (X=-9): "toward 0" (from -9 to -7)
                    doorXMin_L2 = doorXPositions_Central[0]; // -9f
                    doorXMax_L2 = doorXPositions_Central[0] + DoorMeasurementXWidth; // -7f
                    currentAreas.Add(new PlatformMeasurementArea($"TrainDoor_Central_Line2_Z{zCenter}", doorXMin_L2, doorXMax_L2, doorMinZ, doorMaxZ));

                    // Line 1 doors (X=9): "toward 0" (from 7 to 9)
                    doorXMin_L1 = doorXPositions_Central[1] - DoorMeasurementXWidth; // 7f
                    doorXMax_L1 = doorXPositions_Central[1]; // 9f
                    currentAreas.Add(new PlatformMeasurementArea($"TrainDoor_Central_Line1_Z{zCenter}", doorXMin_L1, doorXMax_L1, doorMinZ, doorMaxZ));
                }

                // Then add main platform areas (more general)
                // These extend up to the train door X-positions, ensuring they do not extend "into the train" beyond the platform
                PlatformMeasurementArea centralLine1 = new PlatformMeasurementArea("Central_Line1", 0f, 9f, PlatformZMin, PlatformZMax);
                centralLine1.AreaSqMeters = Mathf.Max(0, centralLine1.AreaSqMeters - 180f); // Subtract 180 sq m
                currentAreas.Add(centralLine1);

                PlatformMeasurementArea centralLine2 = new PlatformMeasurementArea("Central_Line2", -9f, 0f, PlatformZMin, PlatformZMax);
                centralLine2.AreaSqMeters = Mathf.Max(0, centralLine2.AreaSqMeters - 180f); // Subtract 180 sq m
                currentAreas.Add(centralLine2);
                break;

            case TrainController.PlatformType.Mixed:
                // Add stair access areas first (more specific)
                foreach (float zPos in StairZPositions)
                {
                    float minZ, maxZ;
                    // Determine Z-range based on direction (outwards/inwards)
                    if (zPos == -50f) // Outwards (away from 0)
                    {
                        minZ = zPos - StairAccessZDepth;
                        maxZ = zPos;
                    }
                    else if (zPos == 50f) // Outwards (away from 0)
                    {
                        minZ = zPos;
                        maxZ = zPos + StairAccessZDepth;
                    }
                    else if (zPos == -20f) // Inwards (towards 0)
                    {
                        minZ = zPos;
                        maxZ = zPos + StairAccessZDepth;
                    }
                    else if (zPos == 20f) // Inwards (towards 0)
                    {
                        minZ = zPos - StairAccessZDepth;
                        maxZ = zPos;
                    }
                    else // Fallback for unexpected Z positions
                    {
                        minZ = zPos - StairAccessZDepth / 2f;
                        maxZ = zPos + StairAccessZDepth / 2f;
                        Debug.LogWarning($"Unexpected stair Z position {zPos}. Using default centered depth for stair access.");
                    }
                    // Stair access X-ranges for Mixed platform (already 2m or 3m wide, specific to design)
                    currentAreas.Add(new PlatformMeasurementArea($"StairAccess_Mixed_Z{zPos}_L", -12f, -10f, minZ, maxZ)); // 2m wide
                    currentAreas.Add(new PlatformMeasurementArea($"StairAccess_Mixed_Z{zPos}_M", -1f, 1f, minZ, maxZ));     // 2m wide
                    currentAreas.Add(new PlatformMeasurementArea($"StairAccess_Mixed_Z{zPos}_R", 10f, 12f, minZ, maxZ));    // 2m wide
                }

                // Add Train Door areas for Mixed Platform based on X positions -6, -3, 3, 6
                float[] doorXPositions_L1_Mixed = {3f, 6f}; // For Line 1 (positive X side)
                float[] doorXPositions_L2_Mixed = {-6f, -3f}; // For Line 2 (negative X side)

                for (float zCenter = FirstDoorCenterZ; zCenter <= LastDoorCenterZ + 0.001f; zCenter += TrainDoorZSpacing)
                {
                    float doorMinZ = zCenter - halfDoorMeasurementZDepth;
                    float doorMaxZ = zCenter + halfDoorMeasurementZDepth;
                    
                    // Declare these variables here to ensure they are in scope for currentAreas.Add
                    float doorXMin, doorXMax;

                    // Line 1 doors
                    foreach(float xPos in doorXPositions_L1_Mixed)
                    {
                        if (xPos == 3f) // X=3: "toward 0" (from 1 to 3)
                        {
                            doorXMin = xPos - DoorMeasurementXWidth; // 1f
                            doorXMax = xPos; // 3f
                        }
                        else // xPos == 6f: "outward" (from 6 to 8)
                        {
                            doorXMin = xPos; // 6f
                            doorXMax = xPos + DoorMeasurementXWidth; // 8f
                        }
                        currentAreas.Add(new PlatformMeasurementArea($"TrainDoor_Mixed_Line1_Z{zCenter}_X{xPos}", doorXMin, doorXMax, doorMinZ, doorMaxZ));
                    }
                    // Line 2 doors
                    foreach(float xPos in doorXPositions_L2_Mixed)
                    {
                        if (xPos == -3f) // X=-3: "toward 0" (from -3 to -1)
                        {
                            doorXMin = xPos; // -3f
                            doorXMax = xPos + DoorMeasurementXWidth; // -1f
                        }
                        else // xPos == -6f: "outward" (from -8 to -6)
                        {
                            doorXMin = xPos - DoorMeasurementXWidth; // -8f
                            doorXMax = xPos; // -6f
                        }
                        currentAreas.Add(new PlatformMeasurementArea($"TrainDoor_Mixed_Line2_Z{zCenter}_X{xPos}", doorXMin, doorXMax, doorMinZ, doorMaxZ));
                    }
                }

                // Then add main platform areas (more general)
                PlatformMeasurementArea mixedLine1 = new PlatformMeasurementArea("Mixed_Line1", 6f, 12f, PlatformZMin, PlatformZMax);
                mixedLine1.AreaSqMeters = Mathf.Max(0, mixedLine1.AreaSqMeters - 120f); // Subtract 120 sq m for "right"
                currentAreas.Add(mixedLine1);

                PlatformMeasurementArea mixedLine2 = new PlatformMeasurementArea("Mixed_Line2", -12f, -6f, PlatformZMin, PlatformZMax);
                mixedLine2.AreaSqMeters = Mathf.Max(0, mixedLine2.AreaSqMeters - 120f); // Subtract 120 sq m for "left"
                currentAreas.Add(mixedLine2);
                
                PlatformMeasurementArea mixedMiddle = new PlatformMeasurementArea("Mixed_Middle", -3f, 3f, PlatformZMin, PlatformZMax);
                mixedMiddle.AreaSqMeters = Mathf.Max(0, mixedMiddle.AreaSqMeters - 120f); // Subtract 120 sq m for "middle"
                currentAreas.Add(mixedMiddle);
                break;

            case TrainController.PlatformType.Side:
                // Add stair access areas first (more specific)
                foreach (float zPos in StairZPositions)
                {
                    float minZ, maxZ;
                    // Determine Z-range based on direction (outwards/inwards)
                    if (zPos == -50f) // Outwards (away from 0)
                    {
                        minZ = zPos - StairAccessZDepth;
                        maxZ = zPos;
                    }
                    else if (zPos == 50f) // Outwards (away from 0)
                    {
                        minZ = zPos;
                        maxZ = zPos + StairAccessZDepth;
                    }
                    else if (zPos == -20f) // Inwards (towards 0)
                    {
                        minZ = zPos;
                        maxZ = zPos + StairAccessZDepth;
                    }
                    else if (zPos == 20f) // Inwards (towards 0)
                    {
                        minZ = zPos - StairAccessZDepth;
                        maxZ = zPos;
                    }
                    else // Fallback for unexpected Z positions
                    {
                        minZ = zPos - StairAccessZDepth / 2f;
                        maxZ = zPos + StairAccessZDepth / 2f;
                        Debug.LogWarning($"Unexpected stair Z position {zPos}. Using default centered depth for stair access.");
                    }
                    // Stair access X-ranges for Side platform (already 3m wide, specific to design)
                    currentAreas.Add(new PlatformMeasurementArea($"StairAccess_Side_Z{zPos}_L", -12f, -9f, minZ, maxZ)); // 3m wide
                    currentAreas.Add(new PlatformMeasurementArea($"StairAccess_Side_Z{zPos}_R", 9f, 12f, minZ, maxZ));   // 3m wide
                }

                // Add Train Door areas for Side Platform based on X positions -3 and 3
                float[] doorXPositions_Side = {-3f, 3f}; // -3 for Line 2, 3 for Line 1

                for (float zCenter = FirstDoorCenterZ; zCenter <= LastDoorCenterZ + 0.001f; zCenter += TrainDoorZSpacing)
                {
                    float doorMinZ = zCenter - halfDoorMeasurementZDepth;
                    float doorMaxZ = zCenter + halfDoorMeasurementZDepth;
                    
                    // Declare these variables here to ensure they are in scope for currentAreas.Add
                    float doorXMin, doorXMax;

                    // Line 2 doors (X=-3): "outward" (from -5 to -3)
                    doorXMin = doorXPositions_Side[0] - DoorMeasurementXWidth; // -5f
                    doorXMax = doorXPositions_Side[0]; // -3f
                    currentAreas.Add(new PlatformMeasurementArea($"TrainDoor_Side_Line2_Z{zCenter}", doorXMin, doorXMax, doorMinZ, doorMaxZ));

                    // Line 1 doors (X=3): "outward" (from 3 to 5)
                    doorXMin = doorXPositions_Side[1]; // 3f
                    doorXMax = doorXPositions_Side[1] + DoorMeasurementXWidth; // 5f
                    currentAreas.Add(new PlatformMeasurementArea($"TrainDoor_Side_Line1_Z{zCenter}", doorXMin, doorXMax, doorMinZ, doorMaxZ));
                }

                // Then add main platform areas (more general)
                PlatformMeasurementArea sideLine1 = new PlatformMeasurementArea("Side_Line1", 3f, 12f, PlatformZMin, PlatformZMax);
                sideLine1.AreaSqMeters = Mathf.Max(0, sideLine1.AreaSqMeters - 180f); // Subtract 180 sq m
                currentAreas.Add(sideLine1);

                PlatformMeasurementArea sideLine2 = new PlatformMeasurementArea("Side_Line2", -12f, -3f, PlatformZMin, PlatformZMax);
                sideLine2.AreaSqMeters = Mathf.Max(0, sideLine2.AreaSqMeters - 180f); // Subtract 180 sq m
                currentAreas.Add(sideLine2);
                break;

            default:
                Debug.LogWarning("Unknown Platform Type. No areas initialized. Check TrainController.platformType.");
                break;
        }

        // Log the details of the initialized areas for verification in the Unity Console
        foreach (var area in currentAreas)
        {
            //Debug.Log($"Initialized Area: {area.Name}, X Range: [{area.MinX}, {area.MaxX}], Z Range: [{area.MinZ}, {area.MaxZ}], Area: {area.AreaSqMeters} m²");
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
        // IMPORTANT: Ensure main is assigned in the Inspector or via script
        if (main == null)
        {
            Debug.LogError("Main reference is not set in DensityLogger. Skipping density logging.");
            return;
        }

        if (main.agentList == null || main.agentList.Count == 0)
        {
            // Debug.Log("No agents found in Main.agentList or list is null. Skipping density logging.");
            return;
        }

        // Separate areas into stair and non-stair for conditional counting
        List<PlatformMeasurementArea> stairAreas = new List<PlatformMeasurementArea>();
        List<PlatformMeasurementArea> nonStairAreas = new List<PlatformMeasurementArea>();

        foreach (var area in currentAreas)
        {
            if (area.Name.Contains("StairAccess"))
            {
                stairAreas.Add(area);
            }
            else
            {
                nonStairAreas.Add(area);
            }
        }

        // Iterate through all agents
        foreach (var agent in main.agentList)
        {
            if (agent == null || agent.transform == null)
            {
                continue;
            }

            Vector3 agentPos = agent.transform.position;
            bool isAgentInStairArea = false;

            // First, count agent in any stair access areas
            foreach (var stairArea in stairAreas)
            {
                if (agentPos.x >= Mathf.Min(stairArea.MinX, stairArea.MaxX) && agentPos.x <= Mathf.Max(stairArea.MinX, stairArea.MaxX) &&
                    agentPos.z >= Mathf.Min(stairArea.MinZ, stairArea.MaxZ) && agentPos.z <= Mathf.Max(stairArea.MinZ, stairArea.MaxZ))
                {
                    stairArea.CurrentPassengerCount++;
                    isAgentInStairArea = true;
                    // If an agent can realistically be in multiple stair areas and should only count once for overall 'in stair',
                    // then we might need to break here or use a set of counted agents for stair areas.
                    // For typical non-overlapping stair regions, this is fine.
                }
            }

            // If the agent is NOT in any stair access area, then count them in other platform areas (main platforms and train doors)
            if (!isAgentInStairArea)
            {
                foreach (var otherArea in nonStairAreas)
                {
                    if (agentPos.x >= Mathf.Min(otherArea.MinX, otherArea.MaxX) && agentPos.x <= Mathf.Max(otherArea.MinX, otherArea.MaxX) &&
                        agentPos.z >= Mathf.Min(otherArea.MinZ, otherArea.MaxZ) && agentPos.z <= Mathf.Max(otherArea.MinZ, otherArea.MaxZ))
                    {
                        otherArea.CurrentPassengerCount++;
                    }
                }
            }
        }

        // --- Aggregate Train Door Densities ---
        int totalPassengersLine1Doors = 0;
        float totalAreaLine1Doors = 0f;
        int totalPassengersLine2Doors = 0;
        float totalAreaLine2Doors = 0f;

        // Create a temporary instance of PlatformMeasurementArea to call GetLOSGradeForDensity
        PlatformMeasurementArea tempAreaForLOS = new PlatformMeasurementArea("Temp", 0, 0, 0, 0);

        // Iterate through all areas, including those in nonStairAreas
        foreach (var area in currentAreas)
        {
            if (area.Name.Contains("TrainDoor_") && area.Name.Contains("Line1"))
            {
                totalPassengersLine1Doors += area.CurrentPassengerCount;
                totalAreaLine1Doors += area.AreaSqMeters;
            }
            else if (area.Name.Contains("TrainDoor_") && area.Name.Contains("Line2"))
            {
                totalPassengersLine2Doors += area.CurrentPassengerCount;
                totalAreaLine2Doors += area.AreaSqMeters;
            }
        }

        float densityLine1Doors = (totalAreaLine1Doors > 0) ? (float)totalPassengersLine1Doors / totalAreaLine1Doors : 0f;
        string losGradeLine1Doors = (totalAreaLine1Doors > 0) ? tempAreaForLOS.GetLOSGradeForDensity(densityLine1Doors) : "N/A";
        
        float densityLine2Doors = (totalAreaLine2Doors > 0) ? (float)totalPassengersLine2Doors / totalAreaLine2Doors : 0f;
        string losGradeLine2Doors = (totalAreaLine2Doors > 0) ? tempAreaForLOS.GetLOSGradeForDensity(densityLine2Doors) : "N/A";


        // Write the collected data for the current interval to the CSV file
        try
        {
            if (writer == null)
            {
                Debug.LogError("Writer is not initialized. Cannot log platform densities.");
                return;
            }

            string timestamp = Time.time.ToString("F2", CultureInfo.InvariantCulture); // Current simulation time, formatted

            // Log only individual non-TrainDoor areas, treating them as regular entries
            // This now specifically filters out the raw TrainDoor_ areas and ensures StairAccess areas are included
            foreach (var area in currentAreas)
            {
                if (!area.Name.Contains("TrainDoor_")) // Only log if not a specific train door area (individual door)
                {
                    StringBuilder line = new StringBuilder();
                    line.Append(timestamp).Append(",");
                    line.Append(area.Name).Append(",");
                    line.Append(area.CurrentPassengerCount).Append(",");
                    line.Append(area.GetDensity().ToString("F3", CultureInfo.InvariantCulture)).Append(","); // Density, formatted to 3 decimal places with invariant culture
                    line.Append(area.GetLOSGrade());

                    writer.WriteLine(line.ToString());
                }
            }

            // Log aggregated train door data as regular area entries
            // They will now use the general "PassengerCount", "Density", "LOSGrade" columns
            StringBuilder aggregateLine1 = new StringBuilder();
            aggregateLine1.Append(timestamp).Append(",");
            aggregateLine1.Append("TrainDoors_Line1_Total").Append(","); // AreaName
            aggregateLine1.Append(totalPassengersLine1Doors).Append(","); // PassengerCount
            aggregateLine1.Append(densityLine1Doors.ToString("F3", CultureInfo.InvariantCulture)).Append(","); // Density
            aggregateLine1.Append(losGradeLine1Doors); // LOSGrade

            writer.WriteLine(aggregateLine1.ToString());

            StringBuilder aggregateLine2 = new StringBuilder();
            aggregateLine2.Append(timestamp).Append(",");
            aggregateLine2.Append("TrainDoors_Line2_Total").Append(","); // AreaName
            aggregateLine2.Append(totalPassengersLine2Doors).Append(","); // PassengerCount
            aggregateLine2.Append(densityLine2Doors.ToString("F3", CultureInfo.InvariantCulture)).Append(","); // Density
            aggregateLine2.Append(losGradeLine2Doors); // LOSGrade

            writer.WriteLine(aggregateLine2.ToString());
            
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
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            Debug.Log("Density log file flushed and closed.");
        }
    }

    // --- Editor Visualization ---
    void OnDrawGizmosSelected()
    {
        // Ensure areas are initialized, especially when the script is first added or recompiles in editor
        if (currentAreas == null || currentAreas.Count == 0)
        {
            InitializePlatformAreas(); 
            // After initialization, if currentAreas is still null or empty (e.g., if trainController was null), exit.
            if (currentAreas == null || currentAreas.Count == 0) return; 
        }

        foreach (var area in currentAreas)
        {
            // Set a distinct color for the gizmo
            if (area.Name.Contains("StairAccess"))
                Gizmos.color = Color.yellow; // Stairs in yellow
            else if (area.Name.Contains("TrainDoor"))
                Gizmos.color = Color.red; // Train doors in red
            else
                Gizmos.color = Color.cyan; // Main platform areas in cyan

            // Calculate the center of the area for the gizmo cube
            Vector3 center = new Vector3(
                (area.MinX + area.MaxX) / 2f,
                0.05f, // A small height above Y=0 for visibility in a typical 2D platform setup
                (area.MinZ + area.MaxZ) / 2f
            );

            // Calculate the size of the area for the gizmo cube
            Vector3 size = new Vector3(
                Mathf.Abs(area.MaxX - area.MinX),
                0.1f, // A small height for the gizmo cube itself
                Mathf.Abs(area.MaxZ - area.MinZ)
            );

            // Draw a wireframe cube to represent the area in the Scene view
            Gizmos.DrawWireCube(center, size);
        }
    }
}
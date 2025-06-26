using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization; // Required for CultureInfo.InvariantCulture

public class Logger : MonoBehaviour
{
    // --- Configuration ---
    [Header("Logging Settings")]
    [Tooltip("Name of the CSV file. Will be stored in Application.persistentDataPath.")]
    public string fileNameTravelTime = "TravelTimeLog.csv";
    public string fileNameBAT = "BATlog.csv";
    public string fileNameSimulation = "SimulationLog.csv";
    public TrainController trainController;
    public Main main;

    // Internal state
    private string filePathTravelTime;
    private string filePathBAT;
    private string filePathSimulation;

    void Awake()
    {
        main = FindObjectOfType<Main>();
        if (main == null)
        {
            Debug.LogError("Logger did not find main script.");
        }
        // Construct the full file path
        filePathTravelTime = Path.Combine(Application.persistentDataPath, fileNameTravelTime);

        // Write header only if the file doesn't exist or is empty
        if (!File.Exists(filePathTravelTime) || new FileInfo(filePathTravelTime).Length == 0)
        {
            WriteHeaderTravelTime();
        }

        filePathBAT = Path.Combine(Application.persistentDataPath, fileNameBAT);

        filePathSimulation = Path.Combine(Application.persistentDataPath, fileNameSimulation);
        if (!File.Exists(filePathSimulation) || new FileInfo(filePathSimulation).Length == 0)
        {
            WriteHeaderSimulation();
        }
    }

    private void WriteHeaderSimulation()
    {
        try
        {
            // Use 'false' in StreamWriter to overwrite the file and write a new header
            using (StreamWriter sw = new StreamWriter(filePathSimulation, false))
            {
                // The header will now only contain the common columns for all entries
                StringBuilder header = new StringBuilder("TimeStamp,Event");
                sw.WriteLine(header.ToString());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error writing CSV header for simulation: {e.Message}");
        }
    }

    private void WriteHeaderTravelTime()
    {
        try
        {
            // Use 'false' in StreamWriter to overwrite the file and write a new header
            using (StreamWriter sw = new StreamWriter(filePathTravelTime, false))
            {
                // The header will now only contain the common columns for all entries
                StringBuilder header = new StringBuilder("PassengerType,TravelTime");
                sw.WriteLine(header.ToString());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error writing CSV header for travel time: {e.Message}");
        }
    }

    // true boarding, false alighting
    public void LogTravelTime(float travelTime, bool passengerTypeBoarding)
    {
        try
        {
            using (StreamWriter sw = new StreamWriter(filePathTravelTime, true)) // 'true' to append
            {
                StringBuilder line = new StringBuilder();

                if (passengerTypeBoarding)
                {
                    line.Append("Boarding,");
                }
                else
                {
                    line.Append("Alighting,");
                }
                line.Append(travelTime.ToString("F2", CultureInfo.InvariantCulture));
                sw.WriteLine(line.ToString());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error logging travel time: {e.Message}");
        }
    }

    public void LogEvent(string eventDescription)
    {
        try
        {
            using (StreamWriter sw = new StreamWriter(filePathSimulation, true)) // 'true' to append
            {
                StringBuilder line = new StringBuilder();
                line.Append(main.simulationTime.ToString("F2", CultureInfo.InvariantCulture));
                line.Append(",");
                line.Append(eventDescription);
                sw.WriteLine(line.ToString());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error logging event: {e}");
        }
    }

}

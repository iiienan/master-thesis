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
    public TrainController trainController;
    public Main main;

    // Internal state
    private string filePathTravelTime;

    void Awake()
    {
        // Construct the full file path
        filePathTravelTime = Path.Combine(Application.persistentDataPath, fileNameTravelTime);

        // Write header only if the file doesn't exist or is empty
        if (!File.Exists(filePathTravelTime) || new FileInfo(filePathTravelTime).Length == 0)
        {
            WriteHeaderTravelTime();
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
}

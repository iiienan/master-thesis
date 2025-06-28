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
    public string fileNameTravelTime = "TravelTimeLog";
    public string fileNameSimulation = "SimulationLog";
    public string fileNameYellowLine = "YellowLineLog";
    private Main main;
    private TrainController trainController;

    // Internal state
    private string filePathTravelTime;
    private string filePathSimulation;
    private string filePathYellowLine;

    private StreamWriter travelTimeWriter;
    private StreamWriter yellowLineWriter;

    void Awake()
    {
        main = FindObjectOfType<Main>();
        if (main == null)
        {
            Debug.LogError("Logger did not find main script.");
        }

        trainController = FindObjectOfType<TrainController>();
        if (trainController == null)    
        {
            Debug.LogError("Logger did not find TrainController script.");
        }

        StringBuilder sb = new StringBuilder();
        sb.Append(trainController.platformType.ToString());
        sb.Append(trainController.flow.ToString());
        sb.Append(trainController.nAgents.ToString());
        
        if(trainController.alightBeforeBoarding)
        {
            sb.Append("AB");
        }
        

        fileNameTravelTime = fileNameTravelTime + sb.ToString() + ".csv";
        // Construct the full file path
        filePathTravelTime = Path.Combine(Application.persistentDataPath, fileNameTravelTime);

        try
        {
            travelTimeWriter = new StreamWriter(filePathTravelTime, false); // Overwrite the file
            WriteHeaderTravelTime(); // Write column names once
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open travel time log file: {e.Message}");
        }

        fileNameSimulation = fileNameSimulation + sb.ToString() + ".csv";
        filePathSimulation = Path.Combine(Application.persistentDataPath, fileNameSimulation);

        WriteHeaderSimulation();

        fileNameYellowLine = fileNameYellowLine + sb.ToString() + ".csv";
        filePathYellowLine = Path.Combine(Application.persistentDataPath, fileNameYellowLine);

        try
        {
            yellowLineWriter = new StreamWriter(filePathYellowLine, false); // Overwrite the file
            WriteHeaderYellowLine(); // Write column names once
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open travel time log file: {e.Message}");
        }

        
    }

    private void WriteHeaderYellowLine()
    {
        if (yellowLineWriter == null)
        {
            Debug.LogError("Yellow line writer is not initialized.");
            return;
        }

        StringBuilder header = new StringBuilder("TimeStamp,PositionX,PositionZ");
        yellowLineWriter.WriteLine(header.ToString());
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
        if (travelTimeWriter == null)
        {
            Debug.LogError("Travel time writer is not initialized.");
            return;
        }

        StringBuilder header = new StringBuilder("PassengerType,TravelTime,Start,End");
        travelTimeWriter.WriteLine(header.ToString());
    }

    // true boarding, false alighting
    public void LogTravelTime(float travelTime, bool passengerTypeBoarding, float start)
    {
        if (travelTimeWriter == null)
        {
            Debug.LogError("Travel time writer is not initialized.");
            return;
        }
           
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
        line.Append(",");
        line.Append(start.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(main.simulationTime.ToString("F2", CultureInfo.InvariantCulture));
        travelTimeWriter.WriteLine(line.ToString());
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

    public void LogYellowLineViolation(Vector3 position)
    {
        if (yellowLineWriter == null)
        {
            Debug.LogError("Yellow line writer is not initialized.");
            return;
        }

        StringBuilder line = new StringBuilder();
        line.Append(main.simulationTime.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(position.x.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(position.z.ToString("F2", CultureInfo.InvariantCulture));
        yellowLineWriter.WriteLine(line.ToString());
    }

    void OnApplicationQuit()
    {
        if (travelTimeWriter != null)
        {
            travelTimeWriter.Flush();
            travelTimeWriter.Close();
            Debug.Log("Travel time log file flushed and closed.");
        }
        if (yellowLineWriter != null)
        {
            yellowLineWriter.Flush();
            yellowLineWriter.Close();
            Debug.Log("Yellow line log file flushed and closed.");
        }
    }

}

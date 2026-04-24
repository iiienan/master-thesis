using UnityEngine;
using System.IO;
using System.Text;
using System.Globalization; // Required for CultureInfo.InvariantCulture

public class Logger : MonoBehaviour
{
    // --- Configuration ---
    [Header("Logging Settings")]
    [Tooltip("Name of the CSV file. Will be stored in Application.persistentDataPath.")]
    internal string fileNameTravelTime = "TravelTimeLog";
    internal string fileNameSimulation = "SimulationLog";
    internal string fileNameYellowLine = "YellowLineLog";
    internal string fileNameTravelDistance = "TravelDistanceLog";
    internal string fileNameDensity = "DensityLog";
    private Main main;
    private TrainController trainController;
    private TestController testController;

    // Internal state
    private string filePathTravelTime;
    private string filePathSimulation;
    private string filePathYellowLine;
    private string filePathTravelDistance;
    private string filePathDensity;

    private StreamWriter travelTimeWriter;
    private StreamWriter yellowLineWriter;
    private StreamWriter travelDistanceWriter;
    private StreamWriter densityWriter;

    void Start()
    {
        Debug.Log(Application.persistentDataPath);
        main = FindObjectOfType<Main>();
        if (main == null)
        {
            Debug.LogError("Main script not found in the scene.");
            return;
        }
        trainController = FindObjectOfType<TrainController>();
        if (trainController == null)        {
            Debug.LogError("TrainController not found in the scene.");
            return;
        }
        testController = FindObjectOfType<TestController>();
        if (testController == null)
        {
            Debug.LogError("Logger did not find TestController script.");
            return;
        }

        fileNameTravelTime = testController.BuildLogFileName("TravelTime");
        Debug.Log($"Travel time log file name: {fileNameTravelTime}");
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

        fileNameSimulation = testController.BuildLogFileName("Simulation");
        Debug.Log($"Simulation log file name: {fileNameSimulation}");
        filePathSimulation = Path.Combine(Application.persistentDataPath, fileNameSimulation);

        WriteHeaderSimulation();

        fileNameYellowLine = testController.BuildLogFileName("YellowLine");
        Debug.Log($"Yellow line log file name: {fileNameYellowLine}");
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

        fileNameTravelDistance = testController.BuildLogFileName("TravelDistance");
        Debug.Log($"Travel distance log file name: {fileNameTravelDistance}");
        filePathTravelDistance = Path.Combine(Application.persistentDataPath, fileNameTravelDistance);

        try
        {
            travelDistanceWriter = new StreamWriter(filePathTravelDistance, false); // Overwrite the file
            WriteHeaderTravelDistance(); // Write column names once
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open travel distance log file: {e.Message}");
        }

        fileNameDensity = testController.BuildLogFileName("Density");
        filePathDensity = Path.Combine(Application.persistentDataPath, fileNameDensity);
        try
        {
            densityWriter = new StreamWriter(filePathDensity, false); // Overwrite the file
            WriteHeaderDensity();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open density log file: {e.Message}");
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

    private void WriteHeaderTravelDistance()
    {
        if (travelDistanceWriter == null)
        {
            Debug.LogError("Travel distance writer is not initialized.");
            return;
        }

        StringBuilder header = new StringBuilder("PassengerType,TrainLine,TravelDistance,AverageSpeed");
        travelDistanceWriter.WriteLine(header.ToString());
    }

    private void WriteHeaderTravelTime()
    {
        if (travelTimeWriter == null)
        {
            Debug.LogError("Travel time writer is not initialized.");
            return;
        }

        StringBuilder header = new StringBuilder("PassengerType,TrainLine,TravelTime,Start,End");
        travelTimeWriter.WriteLine(header.ToString());
    }

    private void WriteHeaderDensity()
    {
        if (densityWriter == null)
        {
            Debug.LogError("Density writer is not initialized.");
            return;
        }

        StringBuilder header;
        switch (trainController.platformType)
        {
            case TrainController.PlatformType.Central:
                header = new StringBuilder("TimeStamp,Platform1,Platform2");
                densityWriter.WriteLine(header.ToString());
                break;
            case TrainController.PlatformType.Side:
                header = new StringBuilder("TimeStamp,Platform1,Platform2");
                densityWriter.WriteLine(header.ToString());
                break;
            case TrainController.PlatformType.Mixed:
                header = new StringBuilder("TimeStamp,Platform1,Platform2,MiddlePlatform");
                densityWriter.WriteLine(header.ToString());
                break;
            default:
                Debug.LogError("Unknown platform type in Logger.");
                break;
        }
    }

    // true boarding, false alighting
    public void LogTravelTime(float travelTime, bool passengerTypeBoarding, int trainLine, float start)
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
        line.Append(trainLine.ToString());
        line.Append(",");
        line.Append(travelTime.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(start.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(main.simulationTime.ToString("F2", CultureInfo.InvariantCulture));
        travelTimeWriter.WriteLine(line.ToString());
    }

    public void LogTravelDistance(float travelDistance, float travelDistanceTest,bool passengerTypeBoarding, int trainLine, float averageSpeed, float AverageSpeedTest)
    {
        if (travelDistanceWriter == null)
        {
            Debug.LogError("Travel distance writer is not initialized.");
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
        line.Append(trainLine.ToString());
        line.Append(",");
        line.Append(travelDistance.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(travelDistanceTest.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(averageSpeed.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(AverageSpeedTest.ToString("F2", CultureInfo.InvariantCulture));
        travelDistanceWriter.WriteLine(line.ToString());
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

    public void LogDensity(string density)
    {
        if (densityWriter == null)
        {
            Debug.LogError("Density writer is not initialized.");
            return;
        }

        StringBuilder line = new StringBuilder();
        line.Append(main.simulationTime.ToString("F2", CultureInfo.InvariantCulture));
        line.Append(",");
        line.Append(density);
        densityWriter.WriteLine(line.ToString());
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
        if (travelDistanceWriter != null)
        {
            travelDistanceWriter.Flush();
            travelDistanceWriter.Close();
            Debug.Log("Travel distance log file flushed and closed.");
        }
        if (densityWriter != null)
        {
            densityWriter.Flush();
            densityWriter.Close();
            Debug.Log("Density log file flushed and closed.");  
        }
    }

}

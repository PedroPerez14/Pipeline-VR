using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Threading;
using UnityEngine.Video;
using PupilLabs;

public class EyeDataLogger : MonoBehaviour
{
    public GazeController gazeController;
    public Transform gazeOrigin;                        //main camera's transform
    public string loggingFolderName;                    //Name that the user has given to the folder containing the logs, probably called "Logs"
    public bool enableLogging = true;                   //Enable / disable logging
    private bool isLogging = false;                     //Wether the logging coroutine is active or not

    private string fileDate;                            //Name given to the log file (i.e current date and time)
    private string fullPath;                            // "loggingFolderName/fileName.csv"
    private double timeStamp;                           //Time on which the user's eyes were at a certain position and had a certain rotation, logged into the csv file
    private double startSystemTime;                     //We need a reference timestamp, so we check the System clock for that (slightly more precise than using Unity's Time.time)

    private bool isReady = false;                       //Needed for synchronization at the start of execution (?)//TODO

    //Using WaitForSeconds has a 0.1 ~ 0.5 milliseconds error margin between samples, couldn't manage to get anything better than that, sometimes the error is about 1.5 ms
 
    void OnApplicationQuit()
    {
        StopLogging();
    }

    private void ReceiveGaze(GazeData gazeData)
    {

        if (File.Exists(fullPath))
        {
            Vector3 localGazeDirection = gazeData.GazeDirection;
            Vector3 gazeDirectionWorldSpace = gazeOrigin.TransformDirection(localGazeDirection);
            timeStamp = ((double)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds - startSystemTime) / 1000.0; //timestamp will be in range [0, infinity]
            Vector3 n = gazeDirectionWorldSpace.normalized;
            float u = (Mathf.Atan2(n.x, n.z) / (2.0f * Mathf.PI)) + 0.5f;
            float v = 0.5f - (Mathf.Asin(n.y) / Mathf.PI);
            Quaternion rotQ = Quaternion.LookRotation(gazeDirectionWorldSpace);
            Vector3 rotEuler = rotQ.eulerAngles;

            File.AppendAllText(fullPath, timeStamp.ToString().Replace(",", ".") + ";("
            + rotEuler.x.ToString().Replace(",", ".") + ","
            + rotEuler.y.ToString().Replace(",", ".") + ","
            + rotEuler.z.ToString().Replace(",", ".") + ");("
            + rotQ.x.ToString().Replace(",", ".") + ","
            + rotQ.y.ToString().Replace(",", ".") + ","
            + rotQ.z.ToString().Replace(",", ".") + ","
            + rotQ.w.ToString().Replace(",", ".") + ");("
            + gazeDirectionWorldSpace.x.ToString().Replace(",", ".") + ","
            + gazeDirectionWorldSpace.y.ToString().Replace(",", ".") + ","
            + gazeDirectionWorldSpace.z.ToString().Replace(",", ".") + ");("
            + u.ToString().Replace(",", ".") + ","
            + v.ToString().Replace(",", ".") + ");"
            + gazeData.Confidence.ToString().Replace(",", ".") + ";\n");
        }
        else
        {
            Debug.LogError("Logging failed: This log file does not exist! " + fullPath);
        }

    }

    // Start is called before the first frame update
    void Start()
    {
        timeStamp = 0.0;
        isReady = true;
    }

    void Update()
    {
    }

    void CreateLog(int clipID, float startTimestamp)
    {
        fileDate = System.DateTime.UtcNow.ToLocalTime().ToString("ddMMyyyy_HHmmss");
        fullPath = Application.dataPath + "/" + loggingFolderName + "/" + fileDate + "_clip" + clipID + "_gaze" + ".csv";
        if (!File.Exists(fullPath))
        {
            File.WriteAllText(fullPath, "startingTimestamp;" + startTimestamp.ToString().Replace(",", ".") + ";\n"
                + "timestamp;EulerRotation;QuaternionRotation;GazeDirectionWorldSpace;UV_Gaze_Range01;Confidence\n");
        }
        else
        {
            Debug.LogError("Logging failed: This log file already exists! " + fullPath);
        }
    }

    public void StopLogging()
    {
        if (enableLogging && isLogging)
        {
            gazeController.OnReceive3dGaze -= ReceiveGaze;
            isLogging = false;
        }
    }

    public void StartLogging(int clipID, float startTimestamp)
    {
        if (enableLogging && !isLogging)
        {
            isLogging = true;
            CreateLog(clipID, startTimestamp);
            gazeController.OnReceive3dGaze += ReceiveGaze;
            startSystemTime = (double)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }
    }

    public bool IsLogging()
    {
        return isLogging;
    }

    public bool IsReady()
    {
        return isReady;
    }

}

/*
 * Author: Pedro Jos? P?rez Garc?a, 756642
 * Date: 22-03-2022 (last revision)
 * Comms: Trabajo de fin de grado de Ingenier?a Inform?tica, Graphics and Imaging Lab, Universidad de Zaragoza
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Threading;
using UnityEngine.Video;
using PupilLabs;

public class HeadPositionLogger : MonoBehaviour
{
    public GazeController gazeController;
    public GameObject headToTrack;                      //The main camera, probably
    public string loggingFolderName;                    //Name that the user has given to the folder containing the logs, probably called "Logs"
    public bool enableLogging = true;                   //Enable / disable logging
    private bool isLogging = false;                     //Wether the logging coroutine is active or not

    private string fileDate;                            //Name given to the log file (i.e current date and time)
    private string fullPath;                            // "loggingFolderName/fileName.csv"
    private double timeStamp;                           //Time on which the user's head was at a certain position and had a certain rotation, logged into the csv file
    private double startSystemTime;                     //We need a reference timestamp, so we check the System clock for that (slightly more precise than using Unity's Time.time)
    private List<String> logEntries;                    //Every time a logging event arrives we'll store it here in order to reduce disk accessing operations

    private bool isReady = false;                       //Needed for synchronization at the start of execution

    void OnApplicationQuit()
    {
        StopLogging();
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

    void CreateLog(int clipID, float startTimestamp)    //Change this and ReceiveGaze() if you want to log additional data on eye tracking behavior
    {
        fileDate = System.DateTime.UtcNow.ToLocalTime().ToString("ddMMyyyy_HHmmss");
        fullPath = Application.dataPath + "/" + loggingFolderName + "/" + fileDate + "_clip" + clipID + "_head" + ".csv";
        if (!File.Exists(fullPath))
        {
            File.WriteAllText(fullPath, "startingTimestamp;" + startTimestamp.ToString().Replace(",",".") + ";\n"
                + "InitialRotationQuaternion;(" + headToTrack.transform.rotation.x.ToString().Replace(",", ".") + "," 
                + headToTrack.transform.rotation.y.ToString().Replace(",", ".") + ","
                + headToTrack.transform.rotation.z.ToString().Replace(",", ".") + ","
                + headToTrack.transform.rotation.w.ToString().Replace(",", ".") + ");\n"
                + "timestamp;EulerRotationXYZ;QuaternionRot;forwardXYZ;UV_range01\n");
        }
        else
        {
            Debug.LogError("Logging failed: This log file already exists! " + fullPath);
        }
    }

    public void CreateVideoNamesList(string[] names)
    {
        if(enableLogging)
        {
            string _fileDate = System.DateTime.UtcNow.ToLocalTime().ToString("ddMMyyyy_HHmmss");
            string filePath = Application.dataPath + "/" + loggingFolderName + "/" + _fileDate + "_video_clips_list" + ".txt";
            if (!File.Exists(filePath))
            {
                for(int i = 0; i < names.Length; i++)
                {
                    File.AppendAllText(filePath, names[i] + "\n");
                }
            }
            else
            {
                Debug.LogError("Error creating text file with video clip names: This file already exists! " + filePath);
            }
        }
        
    }
    
    public void CreateAudioNamesList(string[] names)
    {
        if(enableLogging)
        {
            string _fileDate = System.DateTime.UtcNow.ToLocalTime().ToString("ddMMyyyy_HHmmss");
            string filePath = Application.dataPath + "/" + loggingFolderName + "/" + _fileDate + "_audio_clips_list" + ".txt";
            if (!File.Exists(filePath))
            {
                for(int i = 0; i < names.Length; i++)
                {
                    File.AppendAllText(filePath, names[i] + "\n");
                }
            }
            else
            {
                Debug.LogError("Error creating text file with audio clip names: This file already exists! " + filePath);
            }
        }
    }

    private void ReceiveGaze(GazeData gazeData)     //Change this and CreateLog() if you want to log additional data on eye tracking behavior
    {
        timeStamp = ((double)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds - startSystemTime) / 1000.0; //timeStamp will be in range [0, infinity]
        Vector3 n = headToTrack.transform.forward.normalized;
        float u = (Mathf.Atan2(n.x, n.z) / (2.0f * Mathf.PI)) + 0.5f;
        float v = 0.5f - (Mathf.Asin(n.y) / Mathf.PI);

        logEntries.Add(timeStamp.ToString().Replace(",", ".") + ";("
        + headToTrack.transform.eulerAngles.x.ToString().Replace(",", ".") + ","
        + headToTrack.transform.eulerAngles.y.ToString().Replace(",", ".") + ","
        + headToTrack.transform.eulerAngles.z.ToString().Replace(",", ".") + ");("
        + headToTrack.transform.rotation.x.ToString().Replace(",", ".") + ","
        + headToTrack.transform.rotation.y.ToString().Replace(",", ".") + ","
        + headToTrack.transform.rotation.z.ToString().Replace(",", ".") + ","
        + headToTrack.transform.rotation.w.ToString().Replace(",", ".") + ");("
        + headToTrack.transform.forward.x.ToString().Replace(",", ".") + ","
        + headToTrack.transform.forward.y.ToString().Replace(",", ".") + ","
        + headToTrack.transform.forward.z.ToString().Replace(",", ".") + ");("
        + u.ToString().Replace(",", ".") + "," + v.ToString().Replace(",", ".") + ");"
        + "\n");
    }

    private IEnumerator DumpLoggedInfoToFile(string path)
    {
        if (File.Exists(path))
        {
            foreach(string dataEntry in logEntries)
            {
                File.AppendAllText(path, dataEntry);
            }
        }
        else
        {
            Debug.LogError("Logging failed: This log file does not exist! " + path);
        }
        yield return null;
    }

    public void StopLogging()
    {
        if(enableLogging && isLogging)
        {
            gazeController.OnReceive3dGaze -= ReceiveGaze;
            isLogging = false;
            StartCoroutine(DumpLoggedInfoToFile(fullPath));
        }
    }

    public void StartLogging(int clipID, float startTimestamp)
    {
        if(enableLogging && !isLogging)
        {
            logEntries = new List<String>();
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

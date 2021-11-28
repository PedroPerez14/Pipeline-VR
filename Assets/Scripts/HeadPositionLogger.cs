using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Threading;

public class HeadPositionLogger : MonoBehaviour
{
    public GameObject headToTrack;                      //The main camera, probably
    public string loggingFolderName;                    //Name that the user has given to the folder containing the logs, probably called "Logs"
    public float sampleRateInMilliseconds = 200;        //Time between two samples of the user's head position and rotation
    public bool enableLogging = true;                   //Enable / disable logging
    private bool isLogging = false;                     //Wether the logging coroutine is active or not

    private string fileDate;                            //Name given to the log file (i.e current date and time)
    private string fullPath;                            // "loggingFolderName/fileName.csv"
    private double timeStamp;                           //Time on which the user's head was at a certain position and had a certain rotation, logged into the csv file
    private double startSystemTime;                     //We need a reference timestamp, so we check the System clock for that (slightly more precise than using Unity's Time.time)
    private WaitForSeconds wait;                        //Object used to tell the Coroutine to wait <sampleRateInMilliseconds> milliseconds before two consecutive samples
    private Coroutine coroutine;                        //We need to keep a copy of the current logging coroutine to stop it later when we're told to (or when closing the program)

    private bool isReady = false;                       //Needed for synchronization at the start of execution (?)//TODO

    //Using WaitForSeconds has a 0.1 ~ 0.5 milliseconds error margin between samples, couldn't manage to get anything better than that, sometimes the error is about 1.5 ms
    //TODO booleanos de qué cosas logear, o de si hacer directamente; servirá para algo, creo (?)

    void OnApplicationQuit()
    {
        if (coroutine != null)
        {
            StopLogging();
            coroutine = null;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        timeStamp = 0.0;
        wait = new WaitForSeconds(sampleRateInMilliseconds / 1000.0f);      //We initialize its value to reuse the object instead of creating one every time
        isReady = true;
    }

    void Update()
    {
    }

    void CreateLog(int clipID, float startTimestamp)
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

    public void StopLogging()
    {
        if(enableLogging && isLogging)
        {
            StopCoroutine(coroutine);
            isLogging = false;
        }
    }

    public void StartLogging(int clipID, float startTimestamp)
    {
        if(enableLogging && !isLogging)
        {
            isLogging = true;
            CreateLog(clipID, startTimestamp);
            coroutine = StartCoroutine(LogHeadData());
        }
    }

    private IEnumerator LogHeadData()
    {
        startSystemTime = (double)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        while (true)
        {
            if (File.Exists(fullPath))
            {
                timeStamp = ((double)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds - startSystemTime) / 1000.0; //timeStamp will be in range [0, infinity]
                Vector3 n = headToTrack.transform.forward.normalized;
                float u = (Mathf.Atan2(n.x, n.z) / (2.0f * Mathf.PI)) + 0.5f;
                float v = 0.5f - (Mathf.Asin(n.y) / Mathf.PI);

                File.AppendAllText(fullPath, timeStamp.ToString().Replace(",", ".") + ";("
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
            else
            {
                Debug.LogError("Logging failed: This log file does not exist! " + fullPath);
            }
            yield return wait;
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

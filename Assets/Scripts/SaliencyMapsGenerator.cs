using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaliencyMapsGenerator : MonoBehaviour
{
    [Header("")]
    public TextAsset videoList;                             //This should be a .txt file generated at the same time as the logs that we are going to process
    public TextAsset[] logsToProcess;                       //We'll validate that the video clip we're referring to from the log's name 
    public float t;                                         //Instant of the 

    //---------- We'll let the users choose which maps they want to output --------//
    public bool outputFrame = true;
    public bool outputFixationMap = true;
    public bool outputHeatMap = true;
    public bool outputSaliencyMap = true;
    //-----------------------------------------------------------------------------//


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

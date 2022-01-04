using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]

public class VideoController : MonoBehaviour
{
    public enum StartingHeadOrientation { noRotation, randomize90DegIntervals, fullyRandomized };

    [SerializeField] private Camera mainCamera;
    [Header("Video pool options")]
    [SerializeField] public VideoClip[] videos;
    [SerializeField] public AudioClip[] audios;             //Please make sure to place the audio clips in the same position as their corresponding video clip
    public int numberOfVideosToShow;                        //Number of videos to be randomly shown to the user among the preloaded ones

    [Header("Video showing config")]
    [SerializeField] public float timeToShowEachClip;       //Expressed in seconds
    [SerializeField] public float timeBetweenClips;         //Expressed in seconds
    [SerializeField] public float audioVolume;              //This should affect the ambisonic audio clip. Affects both experiment and replay mode!
    [SerializeField] public float playbackSpeed = 1.0f;     //Video and audio reproduction speed
    [SerializeField] public bool enableSound;               //This should affect the ambisonic audio, the video clips will ALWAYS be muted. Affects both experiment and replay mode!
    [SerializeField] public bool randomStartingTimeStamp;   //Start at 0:00 or at a random timestamp on the interval {0:00 .. (videoLength - timeToShowEachClip)}
    [SerializeField] public StartingHeadOrientation startingHeadOrientation;

    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private bool[] alreadyShownVideos;                      //To track which videos have already been shown to the user and avoid playing them more than once
    private Coroutine coroutine = null;

    [Header("Logging config")]
    [SerializeField] private HeadPositionLogger headLogger;

    private bool isReady = false;                           //Needed for synchronization at the start of execution


    void OnApplicationQuit()
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
            coroutine = null;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        videoPlayer = gameObject.GetComponent<VideoPlayer>();
        audioSource = gameObject.GetComponent<AudioSource>();

        videoPlayer.clip = null;
        audioSource.clip = null;
        videoPlayer.playOnAwake = false;                    //If something's broken in the future, check this
        audioSource.playOnAwake = false;
        alreadyShownVideos = new bool[videos.Length];

        if(numberOfVideosToShow > videos.Length)            //Additional check, just in case
        {
            numberOfVideosToShow = videos.Length;   
        }

        for(int i = 0; i < alreadyShownVideos.Length; i++)
        {
            alreadyShownVideos[i] = false;
        }
        isReady = true;
    }


    /// Call one of the following two somewhere ///             //TODO
    public void playVideoQueue(int[] IDs, float[] startingTimestamps, float[] timeToPlayEach)
    {
        coroutine = StartCoroutine(VideoLoopFromQueue(IDs, startingTimestamps, timeToPlayEach));
    }

    public IEnumerator playVideosRandomly()
    {
        coroutine = StartCoroutine(VideoLoop());
        string[] videoNames = new string[videos.Length];
        string[] audioNames = new string[audios.Length];                                                //videos[] and audios[] should have the same length
        for(int i = 0; i < videos.Length; i++)
        {
            videoNames[i] = videos[i].name;
            audioNames[i] = audios[i].name;
        }
        headLogger.CreateVideoNamesList(videoNames);                                                    //Create a .txt file containing the order of the video clips right before the logging starts
        headLogger.CreateAudioNamesList(audioNames);                                                    //Create a .txt file containing the order of the audio clips right before the logging starts
        yield return coroutine;
    }

    private IEnumerator VideoLoop()
    {
        for (int i = 0; i < numberOfVideosToShow; i++)
        {
            int videoID = ChooseRandomVideo();
            AdjustRenderTextureDimensions(videoID);                                                         //In case the video clips have different resolutions, we'll adjust the renderTexture
            PlayMedia(videoID);
            yield return new WaitForSeconds(Mathf.Min((float)videoPlayer.clip.length, timeToShowEachClip)); //Play the video clip for the specified amount of time or clip duration, if it's shorter than expected
            StopCurrentlyPlayingMedia(videoID);
            yield return new WaitForSeconds(timeBetweenClips);                                              //Delay between clips for a specified amount of time
        }
        Debug.Log("QUIT CALLED FROM VIDEOLOOP!");
        Application.Quit();
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private IEnumerator VideoLoopFromQueue(int[] IDs, float[] startingTimestamps, float[] timeToPlayEach)   //HeadDataReplayer will load a log queue and call this to play those clips
    {
        for(int i = 0; i < IDs.Length; i++)
        {
            AdjustRenderTextureDimensions(IDs[i]);
            PlayMediaFromTimestamp(IDs[i], startingTimestamps[i]);                      //TODO I need to make a v2 of this
            yield return new WaitForSeconds(timeToPlayEach[i]);                         //need a second float array from the logs extracted data to determine the playing time, and a third one for the starting timestamps
            StopCurrentlyPlayingMedia(IDs[i]);                                          //this should work in both modes without changes
            yield return new WaitForSeconds(timeBetweenClips);                          //Should i use this config? or set up another variable specifically for this mode?
        }
        Debug.Log("QUIT CALLED FROM VIDEOLOOPFROMQUEUE!");
        Application.Quit();
        UnityEditor.EditorApplication.isPlaying = false;
    }


    private void AdjustRenderTextureDimensions(int videoID)
    {
        videoPlayer.targetTexture.width = (int)videos[videoID].width;
        videoPlayer.targetTexture.height = (int)videos[videoID].height;
    }

    private void StopCurrentlyPlayingMedia(int videoID)
    {
        if(headLogger.IsLogging())
        {
            headLogger.StopLogging();
        }
        videoPlayer.Stop();
        audioSource.Stop();
        videoPlayer.targetTexture.Release();
    }

    private float PrepareMediaToPlay(int id)
    {
        float startTime = 0.0f;                 //Starting video/audio timestamp                        
        videoPlayer.Prepare();                  //Important if we want to seek to a random timestamp before playing the video

        //Mute audio from the video clip, ambisonic audio will be loaded and played through a different file
        for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
        {
            videoPlayer.SetDirectAudioMute(i, true);
        }

        if(!enableSound)
        {
            audioVolume = 0.0f;
        }
        audioSource.volume = audioVolume;       //We control the AudioSource volume from this component, value in range [0..1]


        alreadyShownVideos[id] = true;
        //If true, video will start playing at a random moment in the interval [0..(ending - "timeToShowEachClip")].
        //  If the video is too short, it just plays fully from the beginning
        if (randomStartingTimeStamp)
        {
            if (videoPlayer.canSetTime)
            {
                if (videoPlayer.clip.length > timeToShowEachClip && audioSource.clip.length > timeToShowEachClip)
                {
                    startTime = Random.Range(0.0f, (float)(videoPlayer.clip.length - timeToShowEachClip));
                    videoPlayer.time = (double)startTime;
                    audioSource.time = startTime;
                    Debug.Log("Se va a empezar la reproducción en el segundo " + (double)startTime + " del video con duracion " + videoPlayer.clip.length);  //DEBUG
                }
                else
                {
                    Debug.Log("Vídeo/audio muy corto, no se puede randomizar el timestamp de inicio!");
                }
            }
            else
            {
                Debug.Log("videoPlayer.canSetTime == false!");
            }
        }

        //Set playback speed after getting the clip prepared
        if (videoPlayer.canSetPlaybackSpeed)
        {
            videoPlayer.playbackSpeed = playbackSpeed;
            audioSource.pitch = playbackSpeed;                      //Pitch is directly linked to audio speed
        }
        else
        {
            Debug.Log("canSetPlaybackSpeed == false!!!!");
        }

        //User gaze starting position
        switch (startingHeadOrientation)
        {
            case StartingHeadOrientation.randomize90DegIntervals:
                int cameraOrientation = Random.Range(0, 3);
                mainCamera.transform.eulerAngles = new Vector3(0, 90 * cameraOrientation, 0);
                break;
            case StartingHeadOrientation.fullyRandomized:                       //TODO uniform cosine sampling? very likely to be changed
                float pitchRotationDegrees = Random.Range(-90.0f, 90.0f);
                float yawRotationDegrees = Random.Range(-180.0f, 180.0f);
                mainCamera.transform.eulerAngles = new Vector3(pitchRotationDegrees, yawRotationDegrees, 0);
                break;
            default:            //noRotation --> nothing to be done
                break;
        }
        return startTime;
    }

    public int ChooseRandomVideo()
    {
        int chosenID;
        do
        {
            chosenID = Random.Range(0, videos.Length);
        }
        while (alreadyShownVideos[chosenID]);
        return chosenID;
    }

    public void PlayMedia(int id)                    //Use this one to play videos/audio during the experiments, starting point of the video will be decided by the user
    {
        videoPlayer.clip = videos[id];
        audioSource.clip = audios[id];
        float startTime = PrepareMediaToPlay(id);
        headLogger.StartLogging(id, startTime);
        videoPlayer.Play();
        audioSource.Play();
    }


    private void PrepareMediaToPlayFromTimestamp(int id, float timestamp)
    {
        videoPlayer.Prepare();                  //Important if we want to seek to a random timestamp before playing the video

        //Mute audio from the video clip, ambisonic audio will be loaded and played through a different file
        for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
        {
            videoPlayer.SetDirectAudioMute(i, true);
        }

        if (!enableSound)
        {
            audioVolume = 0.0f;
        }
        audioSource.volume = audioVolume;       //We control the AudioSource volume from this component, value in range [0..1]

        if (videoPlayer.canSetTime)
        {
            videoPlayer.time = (double)timestamp;
            audioSource.time = timestamp;
            Debug.Log("Se va a empezar la reproducción en el segundo " + (double)timestamp + " del video con duracion " + videoPlayer.clip.length);  //DEBUG
        }
        else
        {
            Debug.Log("videoPlayer.canSetTime == false!");
        }
    }

    public void PlayMediaFromTimestamp(int id, float startingTimestamp)  //Use this one to play videos/audio when on replay mode, starting point of the video will be determined by the log loaded by the HeadDataReplayer calling this
    {
        videoPlayer.clip = videos[id];
        audioSource.clip = audios[id];
        PrepareMediaToPlayFromTimestamp(id, startingTimestamp);
        //headLogger.StartLogging(id, startTime);           //maybe i'll log this for debug, TODO delete later
        videoPlayer.Play();
        audioSource.Play();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public bool IsReady()
    {
        return isReady;
    }
}

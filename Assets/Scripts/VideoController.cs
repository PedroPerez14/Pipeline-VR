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
    [SerializeField] public float audioVolume;              //This should affect the ambisonic audio clip
    [SerializeField] public float playbackSpeed = 1.0f;     //Video and audio reproduction speed
    [SerializeField] public bool enableSound;               //This should affect the ambisonic audio, the video clips will ALWAYS be muted
    [SerializeField] public bool randomStartingTimeStamp;   //Start at 0:00 or at a random timestamp on the interval {0:00 .. (videoLength - timeToShowEachClip)}
    [SerializeField] public StartingHeadOrientation startingHeadOrientation;

    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private bool[] alreadyShownVideos;                      //To track which videos have already been shown to the user and avoid playing them more than once

    [Header("Logging config")]
    [SerializeField] private HeadPositionLogger headLogger;

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
        StartCoroutine(PlayMedia());
    }

    private IEnumerator PlayMedia()
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
    }

    private void AdjustRenderTextureDimensions(int videoID)
    {
        videoPlayer.targetTexture.width = (int)videos[videoID].width;
        videoPlayer.targetTexture.height = (int)videos[videoID].height;
    }

    private void StopCurrentlyPlayingMedia(int videoID)
    {
        headLogger.StopLogging();
        videoPlayer.Stop();
        audioSource.Stop();
        videoPlayer.targetTexture.Release();
        Debug.Log("Parando el vídeo tras el tiempo especificado.");
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
        Debug.Log("Elegido el vídeo número " + chosenID);
        return chosenID;
    }

    public void PlayMedia(int id)
    {
        videoPlayer.clip = videos[id];
        audioSource.clip = audios[id];
        float startTime = PrepareMediaToPlay(id);
        headLogger.StartLogging(id, startTime);
        videoPlayer.Play();
        audioSource.Play();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

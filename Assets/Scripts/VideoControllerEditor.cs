using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VideoController))]
public class VideoControllerEditor : Editor
{
    SerializedProperty timeBetweenClipsProperty;
    SerializedProperty cubeProperty;

    void OnEnable()
    {
        timeBetweenClipsProperty = serializedObject.FindProperty("timeBetweenClips");
        cubeProperty = serializedObject.FindProperty("cubeToTriggerPlay");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        VideoController myBehaviour = (target as VideoController);
        if (!myBehaviour.findCubeBetweenClips)
        {
            EditorGUILayout.PropertyField(timeBetweenClipsProperty);
        }
        else
        {
            EditorGUILayout.PropertyField(cubeProperty);
        }

        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }
}

/*
 * Author: Pedro José Pérez García, 756642
 * Date: 24-02-2022 (last revision)
 * Comms: Trabajo de fin de grado de Ingeniería Informática, Graphics and Imaging Lab, Universidad de Zaragoza
 *          Script that hides or shows some variables in the Unity editor, depending on the selected configuration
 */
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

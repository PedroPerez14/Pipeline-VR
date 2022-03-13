/*
 * Author: Pedro Jos� P�rez Garc�a, 756642
 * Date: 24-02-2022 (last revision)
 * Comms: Trabajo de fin de grado de Ingenier�a Inform�tica, Graphics and Imaging Lab, Universidad de Zaragoza
 *          Script that hides or shows some variables in the Unity editor, depending on the selected configuration
 */
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SaliencyMapsGenerator))]
public class SaliencyMapsGeneratorEditor : Editor
{
    SerializedProperty velocityThreshIVTProperty;
    SerializedProperty discardPercentProperty;

    void OnEnable()
    {
        velocityThreshIVTProperty = serializedObject.FindProperty("velocityThresholdIVT");
        discardPercentProperty = serializedObject.FindProperty("dynamicThreshDiscardPercent");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        SaliencyMapsGenerator myBehaviour = (target as SaliencyMapsGenerator);
        if (!myBehaviour.dynamicVelocityThreshold)
        {
            EditorGUILayout.PropertyField(velocityThreshIVTProperty);
        }
        else
        {
            EditorGUILayout.PropertyField(discardPercentProperty);
        }

        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }
}

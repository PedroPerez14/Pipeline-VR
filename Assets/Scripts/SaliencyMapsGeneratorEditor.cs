using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SaliencyMapsGenerator))]
public class SaliencyMapsGeneratorEditor : Editor
{
    SerializedProperty speedThreshIVTProperty;
    SerializedProperty discardPercentProperty;

    void OnEnable()
    {
        speedThreshIVTProperty = serializedObject.FindProperty("speedThresholdIVT");
        discardPercentProperty = serializedObject.FindProperty("dynamicThreshDiscardPercent");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        SaliencyMapsGenerator myBehaviour = (target as SaliencyMapsGenerator);
        if (!myBehaviour.dynamicSpeedThreshold)
        {
            EditorGUILayout.PropertyField(speedThreshIVTProperty);
        }
        else
        {
            EditorGUILayout.PropertyField(discardPercentProperty);
        }

        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }
}

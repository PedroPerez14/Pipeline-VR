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

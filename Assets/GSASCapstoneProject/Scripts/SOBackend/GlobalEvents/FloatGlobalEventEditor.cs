using UnityEditor;
using UnityEngine;

namespace Core.GlobalEvents
{
    [CustomEditor(typeof(FloatGlobalEvent), editorForChildClasses: true)]
    public class FloatGlobalEventEditor : Editor
    {
        float value = 0;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUI.enabled = Application.isPlaying;

            FloatGlobalEvent fe = target as FloatGlobalEvent;
            value = EditorGUILayout.FloatField("Value", value);
            if (GUILayout.Button("Raise"))
                fe.Raise(value);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace Core.GlobalEvents
{
    [CustomEditor(typeof(StringGlobalEvent), editorForChildClasses: true)]
    public class StringGlobalEventEditor : Editor
    {
        string value = "";

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUI.enabled = Application.isPlaying;

            StringGlobalEvent se = target as StringGlobalEvent;
            value = EditorGUILayout.TextField("Value", value);
            if (GUILayout.Button("Raise"))
                se.Raise(value);
        }
    }
}

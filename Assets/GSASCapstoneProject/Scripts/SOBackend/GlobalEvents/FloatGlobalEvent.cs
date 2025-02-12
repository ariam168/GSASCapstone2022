///
/// Created by Dennis Chen
/// Reference: Ryan's code talked about in https://www.youtube.com/watch?v=raQ3iHhE_Kk
///

using System.Collections.Generic;
using UnityEngine;

namespace Core.GlobalEvents
{
    [CreateAssetMenu]
    public class FloatGlobalEvent : ScriptableObject
    {
        /// <summary>
        /// The list of listeners that this event will notify if it is raised.
        /// </summary>
        private readonly List<FloatGlobalEventListener> _eventListeners =
            new List<FloatGlobalEventListener>();

        public delegate void FloatEvent(float val);
        public event FloatEvent OnFloatEventCalled;

        public void Raise(float value)
        {
            if(OnFloatEventCalled != null)
            {
                OnFloatEventCalled.Invoke(value);
            }
            for (int i = _eventListeners.Count - 1; i >= 0; i--)
                _eventListeners[i].OnEventRaised(value); ;
        }

        public void Subscribe(FloatEvent floatEvent)
        {
            OnFloatEventCalled += floatEvent;
        }

        public void Unsubscribe(FloatEvent floatEvent)
        {
            OnFloatEventCalled -= floatEvent;
        }

        public void RegisterListener(FloatGlobalEventListener listener)
        {
            if (!_eventListeners.Contains(listener))
                _eventListeners.Add(listener);
        }

        public void UnregisterListener(FloatGlobalEventListener listener)
        {
            if (_eventListeners.Contains(listener))
                _eventListeners.Remove(listener);
        }

    }
}
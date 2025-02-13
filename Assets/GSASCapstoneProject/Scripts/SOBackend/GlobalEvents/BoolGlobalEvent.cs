///
/// Created by Dennis Chen
/// Reference: Ryan's code talked about in https://www.youtube.com/watch?v=raQ3iHhE_Kk
///

using System.Collections.Generic;
using UnityEngine;

namespace Core.GlobalEvents
{
    [CreateAssetMenu]
    public class BoolGlobalEvent : ScriptableObject
    {
        /// <summary>
        /// The list of listeners that this event will notify if it is raised.
        /// </summary>
        private readonly List<BoolGlobalEventListener> _eventListeners =
            new List<BoolGlobalEventListener>();

        public delegate void BoolEvent(bool val);
        public event BoolEvent OnBoolEventCalled;

        public void Raise(bool value)
        {
            if(OnBoolEventCalled != null)
            {
                OnBoolEventCalled.Invoke(value);
            }
            for (int i = _eventListeners.Count - 1; i >= 0; i--)
                _eventListeners[i].OnEventRaised(value); ;
        }

        public void Subscribe(BoolEvent boolEvent)
        {
            OnBoolEventCalled += boolEvent;
        }

        public void Unsubscribe(BoolEvent boolEvent)
        {
            OnBoolEventCalled -= boolEvent;
        }

        public void RegisterListener(BoolGlobalEventListener listener)
        {
            if (!_eventListeners.Contains(listener))
                _eventListeners.Add(listener);
        }

        public void UnregisterListener(BoolGlobalEventListener listener)
        {
            if (_eventListeners.Contains(listener))
                _eventListeners.Remove(listener);
        }

    }
}
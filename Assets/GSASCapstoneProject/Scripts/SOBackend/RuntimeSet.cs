﻿///
/// Created by Dennis Chen
/// Reference: Ryan's code talked about in https://www.youtube.com/watch?v=raQ3iHhE_Kk
///

using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public abstract class RuntimeSet<T> : ScriptableObject
    {
        public List<T> Items = new List<T>();

        public void Add(T thing)
        {
            if (!Items.Contains(thing))
                Items.Add(thing);
        }

        public void Remove(T thing)
        {
            if (Items.Contains(thing))
                Items.Remove(thing);
        }
    }
}
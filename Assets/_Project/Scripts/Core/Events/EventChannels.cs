using System;
using UnityEngine;

namespace NW.Core.Events
{
    /// <summary>
    /// ScriptableObject event channels: the decoupling backbone (doc 07 §4).
    /// Systems raise on a channel asset; listeners subscribe — board, combat and
    /// UI never reference each other directly.
    /// </summary>
    public abstract class EventChannel<T> : ScriptableObject
    {
        public event Action<T> Raised;
        public void Raise(T payload) => Raised?.Invoke(payload);
    }

    [CreateAssetMenu(menuName = "NW/Events/Void Channel", fileName = "evt_")]
    public class VoidEventChannel : ScriptableObject
    {
        public event Action Raised;
        public void Raise() => Raised?.Invoke();
    }

    [CreateAssetMenu(menuName = "NW/Events/Int Channel", fileName = "evt_")]
    public class IntEventChannel : EventChannel<int> { }

    [CreateAssetMenu(menuName = "NW/Events/Float Channel", fileName = "evt_")]
    public class FloatEventChannel : EventChannel<float> { }

    [CreateAssetMenu(menuName = "NW/Events/String Channel", fileName = "evt_")]
    public class StringEventChannel : EventChannel<string> { }
}

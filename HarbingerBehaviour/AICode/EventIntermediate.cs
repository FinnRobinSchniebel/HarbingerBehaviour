using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace HarbingerBehaviour.AICode
{
    internal class EventIntermediate : MonoBehaviour
    {
        [SerializeField] private UnityEvent Listen;

        public void TriggerEvent()
        {
            Listen.Invoke();
        }
    }
}

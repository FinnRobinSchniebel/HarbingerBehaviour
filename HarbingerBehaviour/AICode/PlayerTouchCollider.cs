using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace HarbingerBehaviour.AICode
{
    internal class PlayerTouchCollider: MonoBehaviour
    {

        public HarbingerAI mainScript;
        private void OnTriggerEnter(Collider other)
        {

            if (other.CompareTag("Player") && mainScript != null)
            {

                mainScript.PlayerTouched(other.GetComponent<PlayerControllerB>());
                
            }
            




        }


    }
}

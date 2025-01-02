using GameNetcodeStuff;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HarbingerBehaviour.AICode
{
    internal class ItemCollider : MonoBehaviour
    {

        public HarbingerAI AI;

        public LayerMask LayerMask;

        private void OnTriggerEnter(Collider other)
        {
            
            if (other.GetComponent<GrabbableObject>() != null && other.GetComponent<GrabbableObject>() is LungProp )
            {
                HarbingerLoader.mls.LogInfo("Triggered Appi");
                //AI.LungProp = other.GetComponent<LungProp>();
            }


        }

        public void TeleportCheck()
        {
            //this.GetComponent<MeshCollider>().bounds
            Bounds b = GetComponent<BoxCollider>().bounds;
            Collider[] other = Physics.OverlapBox(b.center, b.size / 2, Quaternion.identity, LayerMask);
            
            foreach (Collider c in other)
            {
                if(c.GetComponent<GrabbableObject>() != null && c.GetComponent<GrabbableObject>() is LungProp)
                {
                    HarbingerLoader.mls.LogInfo("Triggered TP appi");
                    //AI.LungProp = c.GetComponent<LungProp>();
                    break;
                }
            }
            

        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("PhysicsProp") && other.GetComponent<GrabbableObject>() is LungProp && AI.LungProp == other.GetComponent<GrabbableObject>())
            {
                AI.LungProp = null;
            }
        }
    }
}

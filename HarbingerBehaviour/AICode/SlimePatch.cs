using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace HarbingerBehaviour.AICode
{
    internal class SlimePatchExample : NetworkBehaviour
    {

        public EnemyAI ai; //must be synced between clients

        [ClientRpc]
        public void TeleportCreatureClientRpc(Vector3 TPLocation)
        {
            Vector3 centeroffset = ((BlobAI)ai).centerPoint.position - ((BlobAI)ai).transform.position;
            Vector3[] SlimeBones = new Vector3[8];
            //  prefix/prep
            if (ai is BlobAI)
            {
                for (int i = 0; i < ((BlobAI)ai).SlimeRaycastTargets.Length; i++)
                {
                    SlimeBones[i] = ((BlobAI)ai).SlimeBones[i].position - ((BlobAI)ai).centerPoint.position;
                }
            }
            //teleport ai agent
            if (ai.IsOwner)
            {
                ai.agent.Warp(TPLocation);
            }
            //  postfix
            if (ai is BlobAI)
            {
                ((BlobAI)ai).centerPoint.position = ((BlobAI)ai).transform.position + centeroffset;
                for (int i = 0; i < ((BlobAI)ai).SlimeRaycastTargets.Length; i++)
                {
                    SlimeBones[i] = ((BlobAI)ai).centerPoint.position + SlimeBones[i];
                }
                Traverse.Create((BlobAI)ai).Field("SlimeBonePositions").SetValue(SlimeBones);
            }
        }
    }
}

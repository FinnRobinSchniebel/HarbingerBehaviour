using GameNetcodeStuff;
using HarbingerBehaviour.ConfigSync;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace HarbingerBehaviour.Items
{
    internal class RealityFracture : PhysicsProp
    {

        public int DamagePlayer = 20;
        public AudioSource UseSource;
        public AudioClip UseItemAudio;
        public AudioClip CantUseItemAudio;
        GameObject[] allAINodes;
        private System.Random TeleportRandom;
        private System.Random noisemakerRandom;
        public bool CanBeUsed = true;

        private void Awake()
        {
            
        }

        public override void Start()
        {
            base.Start();
            //allAINodes = GameObject.FindGameObjectsWithTag("AINode");
            TeleportRandom = new System.Random();
            noisemakerRandom = new System.Random();
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (!CanBeUsed)
            {
                return;
            }

            HarbingerLoader.mls.LogInfo("Item activiated");
            float pitch = (float)noisemakerRandom.Next((int)(.8 * 100f), (int)(1 * 100f)) / 100f;
            UseSource.pitch = pitch;
            allAINodes = GameObject.FindGameObjectsWithTag("AINode");
            if (!playerHeldBy.isInsideFactory || allAINodes == null)
            {
                currentUseCooldown = .2f;
                UseSource.PlayOneShot(CantUseItemAudio);
                return;
            }
            StartCoroutine(Teleport());
            
        }

        private IEnumerator Teleport()
        {
            HarbingerLoader.mls.LogInfo("Item Teleporting");
            UseSource.PlayOneShot(UseItemAudio);
            
            yield return new WaitForSeconds(1.5f);
            
            if ((GameNetworkManager.Instance.localPlayerController == null) || !RoundManager.Instance.IsHost)
            {
                yield break;
            }
            int tries = 5;
            while (tries > 0)
            {


                int nextLoc = TeleportRandom.Next(allAINodes.Length);
                Vector3 ExitPosition = allAINodes[nextLoc].transform.position;

                NavMeshHit hit = FindMoveLocation(ExitPosition, new Vector2(.5f, 1f), 5);

                if (hit.hit)
                {

                    if (playerHeldBy != null)
                    {
                        TeleportPlayerClientRPC(playerHeldBy.playerClientId, hit.position);
                        //playerHeldBy.GetComponent<PlayerControllerB>().TeleportPlayer(hit.position, true);
                    }
                    break;
                }

                tries--;
            }
        }


        public NavMeshHit FindMoveLocation(Vector3 SourceLocation, Vector2 distanceRange, int maxTries = 15, float maxDistance = 2, bool hasVertical = false, float VerticalAmount = 0)
        {
            HarbingerLoader.mls.LogInfo("Item find location");
            NavMeshHit hit = new NavMeshHit();
            int tries = 0;
            bool success = false;
            while (!success && tries < maxTries)
            {
                //Vector3 target = EnemyLocation + new Vector3(UnityEngine.Random.Range(.01f, .5f), 0, UnityEngine.Random.Range(.01f, .5f));
                Vector2 RandomPoint = UnityEngine.Random.insideUnitCircle.normalized.normalized * distanceRange;
                float Vertical = 0;
                if (hasVertical)
                {
                    Vertical = UnityEngine.Random.Range(-VerticalAmount, VerticalAmount);
                }
                Vector3 TpPos = SourceLocation + new Vector3(RandomPoint.x, Vertical, RandomPoint.y);

                NavMesh.SamplePosition(TpPos, out hit, maxDistance, NavMesh.AllAreas);

                if (hit.hit && allAINodes.Length != 0)
                {
                    HarbingerLoader.mls.LogInfo("Item found location");
                    success = true;
                }

                HarbingerLoader.mls.LogInfo("Item failed location");

                tries++;
            }
            return hit;
        }

        [ClientRpc]
        public void TeleportPlayerClientRPC(ulong playernum, Vector3 pos)
        {
            //Vector3 pos = new Vector3(1,2,3);
            
            PlayerControllerB Player = StartOfRound.Instance.allPlayerScripts[playernum];
            Player.DamagePlayer(DamagePlayer);
            Player.GetComponent<PlayerControllerB>().TeleportPlayer(pos, true);
        }


    }
}

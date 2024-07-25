using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace HarbingerBehaviour.AICode
{
    internal class HarbingerAI : EnemyAI
    {
        public enum HarbingerStates
        {
            Moving,
            Standing,
            Teleporting,
            TeleportingSelf,

        }

        private Vector3 mainEntrancePosition; //usefull later when I add the crown (maybe)

        public float MaxDistanceToPlayerBeforeTeleport = 20f;

        public HarbingerStates state = HarbingerStates.Standing;

        public float movementspeed = 5f;

        private System.Random enemyRandom;

        public Vector2 MinMaxMovement = new Vector2(5, 10);

        public GameObject debugPoint;

        public GameObject NextPos;

        public float stationaryTime = 2f;

        public float ActionStart = 0;

        public float MovementTimeout = 6f;

        
        [Header("Teleport Other")]
        public TeleportRing tpRing;
        public float TPOtherCooldown = 15f;
        private float TPOtherInitCooldown;
        private System.Random TeleportRandom;
        private bool TeleportValid = false;
        private bool aligned = false;
        private Quaternion lookRotation;
        

        [Header("TeleportSelf")]
        public Vector2 TeleportSelfArriveRadius = new Vector2(4, 6);
        public float SelfTeleportCooldown = 40f;
        public AudioSource PositionBeforeSelfTeleport;
        private float lastTeleportTime = 0.0f;
        private List<EnemyAI> Alreadyused = new List<EnemyAI>();
        private float IntialCooldown;
        private bool TPSelfBehaviourOverridable = false;
        

        public override void Start()
        {

            base.Start();
            movingTowardsTargetPlayer = true;
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            TeleportRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            mainEntrancePosition = RoundManager.FindMainEntrancePosition();
            ActionStart = Time.time;
            
            lastTeleportTime = Time.time;
            PositionBeforeSelfTeleport.transform.SetParent(this.transform.transform.parent);
            tpRing.transform.SetParent(this.transform.transform.parent);
            HarbingerLoader.mls.LogWarning("Testing TP Speed: " + SyncedInstance<Config>.Instance.TPSelfCooldown.Value);
            HostConfigApply(SyncedInstance<Config>.Instance.TeleportSpeed.Value, Math.Max(SyncedInstance<Config>.Instance.TPSelfCooldown.Value, 5), Math.Max(SyncedInstance<Config>.Instance.TPOtherCooldown.Value, 5));
            

        }

        public void HostConfigApply(float tpSpeedMult, float teleportSelfCooldown, float teleportOthersCooldown)
        {
            creatureAnimator.SetFloat("TeleportMultipier", tpSpeedMult);
            //cooldowns
            SelfTeleportCooldown = teleportSelfCooldown;
            IntialCooldown = SelfTeleportCooldown;

            TPOtherCooldown = teleportOthersCooldown;
            TPOtherInitCooldown = TPOtherCooldown;

        }


        public override void OnDestroy()
        {
            
            
        }

        public override void DoAIInterval()
        {
            if (moveTowardsDestination)
            {
                agent.SetDestination(destination);
            }
            if (StartOfRound.Instance.livingPlayers == 0)
            {
                if (moveTowardsDestination)
                {
                    agent.SetDestination(destination);
                }
                return;
            }
            if (!RoundManager.Instance.IsHost)
            {
                return;
            }

            PlayerControllerB ClosestPlayer = GetClosestPlayer(false, false, true);
            
            //only enter if the player is not teleporting something else and the cooldown is finished
            if(state != HarbingerStates.Teleporting && SelfTeleportCooldown == 0)
            {
                
                //self Teleport
                if (ClosestPlayer != null && ClosestPlayer.isInsideFactory && Vector3.Distance(ClosestPlayer.transform.position, transform.position) > MaxDistanceToPlayerBeforeTeleport)
                {
                    if(state != HarbingerStates.TeleportingSelf)
                    {
                        NewStateClientRpc(HarbingerStates.TeleportingSelf);
                    }
                    
                }
                //interrupt teleport if player is out of range
                else if(TPSelfBehaviourOverridable && state == HarbingerStates.TeleportingSelf)
                {
                    //cancel teleport if a player reentered the area
                    TPSelfBehaviourOverridable = false;
                    StopCoroutine(TeleportSelf());
                    NewStateClientRpc(HarbingerStates.Moving);
                }
                else
                {
                    SelfTeleportCooldown += 5;
                }

            }
            //test teleport other if you arnt teleporting
            if(state != HarbingerStates.TeleportingSelf && TPOtherCooldown == 0)
            {
                //teleport other
                if (ClosestPlayer != null && ClosestPlayer.isInsideFactory && Vector3.Distance(ClosestPlayer.transform.position, transform.position) < MaxDistanceToPlayerBeforeTeleport)
                {
                    if (state != HarbingerStates.Teleporting)
                    {
                        NewStateClientRpc(HarbingerStates.Teleporting);
                    }
                }
                // only cancel the teleport if it hasnt been started yet
                else if(!TeleportValid && state == HarbingerStates.Teleporting)
                {
                    //cancel teleport if a player reentered the area
                    StopCoroutine(PrepTeleportOther());
                    NewStateClientRpc(HarbingerStates.Moving);
                }
                else
                {
                    TPOtherCooldown += 5;
                }
            }

        }

        public override void Update()
        {
            base.Update();

            if(SelfTeleportCooldown > 0)
            {
                SelfTeleportCooldown = Math.Max(0, SelfTeleportCooldown - Time.deltaTime);
            }
            if (TPOtherCooldown > 0)
            {
                TPOtherCooldown = Math.Max(0, TPOtherCooldown - Time.deltaTime);
            }

            switch (state) 
            { 
                case HarbingerStates.Standing:
                    if(ActionStart + stationaryTime < Time.time)
                    {
                        NewStateClientRpc(HarbingerStates.Moving);
                    }
                    break;
                case HarbingerStates.Moving:
                    if(Vector3.Distance(transform.position, destination) < .5f || MovementTimeout + ActionStart < Time.time)
                    {
                        NewStateClientRpc(HarbingerStates.Standing);
                    }
                    break;                
                case HarbingerStates.Teleporting:
                    if (TeleportValid)
                    {
                        /*if(ActionStart + 20f < Time.time && )
                        {
                            HarbingerLoader.mls.LogError("TeleportTimeout switching behaviour");
                            CancelTeleportClientRpc();
                            
                            NewStateClientRpc(HarbingerStates.Standing);
                        }*/

                        if(!aligned && Math.Abs(agent.transform.eulerAngles.y - lookRotation.eulerAngles.y) < 5f)
                        {
                            aligned = true;
                            HarbingerLoader.mls.LogWarning("In possition");
                            StartTeleportEffectsClientRpc();
                            SwitchToBehaviourState(2);
                            //StartCoroutine(TimeTP());

                        }
                        else
                        {
                            //HarbingerLoader.mls.LogError("Rotating?");
                            agent.transform.rotation = Quaternion.RotateTowards(agent.transform.rotation, lookRotation, Math.Abs(180f) * Time.deltaTime);
                        }
                        
                    }
                    break;
                case HarbingerStates.TeleportingSelf:
                    break;
                default:
                    break;

            }


        }

        public IEnumerator TimeTP()
        {
            yield return new WaitForSeconds(6.25f);
            TeleportEvent();
        }



        [ClientRpc]
        public void NewStateClientRpc(HarbingerStates newstate)
        {
            HarbingerLoader.mls.LogInfo("NewState " + newstate);

            if(state == HarbingerStates.Teleporting && newstate != state)
            {
                aligned = false;
                TeleportValid = false;
                Alreadyused.Clear();
            }

            state = newstate;
            ActionStart = Time.time;

            switch (state)
            {
                case HarbingerStates.Standing:
                    //HarbingerLoader.mls.LogInfo("Standing");
                    agent.speed = 0f; 
                    SwitchToBehaviourState(0);
                    break;
                case HarbingerStates.Moving:
                    Vector3 nextSelectedPoint = randomNewPoint(MinMaxMovement, transform);
                    NavMeshHit h = clossetLocation(nextSelectedPoint);
                    while (!h.hit)
                    {                        
                        nextSelectedPoint = randomNewPoint(MinMaxMovement, transform);
                        h = clossetLocation(nextSelectedPoint);
                    }
                    SetDestinationToPosition(h.position);
                    agent.speed = movementspeed;
                    SwitchToBehaviourState(1);
                    break;
                case HarbingerStates.Teleporting:
                    agent.speed = 0f;
                    if (!RoundManager.Instance.IsHost)
                    {
                        break;
                    }
                    StartCoroutine(PrepTeleportOther());
                    break;
                case HarbingerStates.TeleportingSelf:
                    agent.speed = 0f;
                    
                    StartCoroutine(TeleportSelf());                   

                    break;
                default:
                    break;

            }
        }

        private IEnumerator TeleportSelf()
        {
            creatureAnimator.SetTrigger("CheckingTeleport");
            TPSelfBehaviourOverridable = true;
            yield return new WaitForSeconds(2f);
            

            List<PlayerControllerB> validTeleportPlayers = new List<PlayerControllerB>();

            foreach (PlayerControllerB a in StartOfRound.Instance.allPlayerScripts)
            {
                if (a.isInsideFactory == true && !a.isPlayerDead)
                {
                    validTeleportPlayers.Add(a);
                }
            }
            if(validTeleportPlayers.Count != 0)
            {
                LastLocationUpdateClientRpc(transform.position);
                
                if (RoundManager.Instance.IsHost)
                {
                    int player = TeleportRandom.Next(validTeleportPlayers.Count);
                    randomNewPoint(TeleportSelfArriveRadius, validTeleportPlayers[player].transform);
                    NavMeshHit h = clossetLocation(NextPos.transform.position);
                    int tries = 0;
                    while (!h.hit && tries != 15)
                    {
                        randomNewPoint(TeleportSelfArriveRadius, transform);
                        h = clossetLocation(NextPos.transform.position);
                        tries++;
                    }
                    if (h.hit)
                    {
                        SwitchToBehaviourState(3);
                        yield return new WaitForSeconds(.1f);
                        agent.Warp(h.position);
                        lastTeleportTime = Time.time;
                        SelfTeleportCooldown = IntialCooldown;
                        
                    }
                    
                }
                
            }
            TPSelfBehaviourOverridable = false;
            yield return new WaitForSeconds(.3f);
            NewStateClientRpc(HarbingerStates.Moving);
        }


        [ClientRpc]
        public void LastLocationUpdateClientRpc(Vector3 newLocation)
        {
            PositionBeforeSelfTeleport.transform.position = newLocation;
            PositionBeforeSelfTeleport.Play();
        }
        

        private IEnumerator PrepTeleportOther()
        {
            creatureAnimator.SetTrigger("CheckingTeleport");
            //ensure this is only done by the server
            if (!RoundManager.Instance.IsHost)
            {
                yield break;
            }

            yield return new WaitForSeconds(.5f);
            List<EnemyAI> validEnemies = new List<EnemyAI>();
            foreach(EnemyAI Ai in RoundManager.Instance.SpawnedEnemies){
                if (Ai.isOutside == this.isOutside && !Ai.isEnemyDead && Vector3.Distance(Ai.transform.position, transform.position) > 20f && MonsterBlackList(Ai)) 
                {
                    validEnemies.Add(Ai);
                }
            }

            if(validEnemies.Count == 0)
            {
                HarbingerLoader.mls.LogInfo("No valid Targets");
                TPOtherCooldown += 5;
                NewStateClientRpc(HarbingerStates.Standing);
                yield break;
            }

            EnemyAI a = validEnemies[TeleportRandom.Next(validEnemies.Count)];
            Alreadyused.Add(a);
            int tries = 0;
            randomNewPoint(new Vector2(2.5f, 4f), transform);
            NavMeshHit h = clossetLocation(NextPos.transform.position);

            while (!h.hit && tries != 15)
            {
                randomNewPoint(MinMaxMovement, transform);
                h = clossetLocation(NextPos.transform.position);
                tries++;
            }

            if (!h.hit)
            {
                NewStateClientRpc(HarbingerStates.Standing);
                yield break;
            }


            //give all clients the same enemy and location
            SettUpTPClientRpc(a.NetworkObjectId, h.position);

            Vector3 RotateToTeleportionDirection = (tpRing.transform.position - agent.transform.position).normalized;
            RotateToTeleportionDirection.y = 0;

            lookRotation = Quaternion.LookRotation(RotateToTeleportionDirection);

            TeleportValid = true;
        }

        [ClientRpc]
        public void SettUpTPClientRpc(ulong EnemyNetID, Vector3 loc)
        {
            NetworkObject en = NetworkManager.Singleton.SpawnManager.SpawnedObjects[EnemyNetID];
            tpRing.setup(en.GetComponent<EnemyAI>(), loc);
        }


        public Vector3 randomNewPoint(Vector2 radMinMax, Transform AtPoint)
        {
            float radius = Mathf.Lerp(radMinMax.x, radMinMax.y, (float)enemyRandom.NextDouble());
            NextPos.transform.position = AtPoint.position;
            NextPos.transform.rotation = AtPoint.rotation;

            float direction = Mathf.Lerp(0, 360, (float)enemyRandom.NextDouble());

            NextPos.transform.Rotate(0f, direction, 0f);
            NextPos.transform.Translate(0, Mathf.Lerp(-2, 2, (float)enemyRandom.NextDouble()), radius);

            if(debugPoint != null)
            {
                debugPoint.transform.position = NextPos.transform.position;
            }

            return NextPos.transform.position;
        }

        public NavMeshHit clossetLocation(Vector3 target)
        {
            NavMeshHit hit;
            float maxDistance = 2.0f;
            NavMesh.SamplePosition(target, out hit, maxDistance, NavMesh.AllAreas);
            return hit;
        }

        public void TeleportEvent()
        {
            if (!RoundManager.Instance.IsHost)
            {
                return;
            }
            lastTeleportTime = Time.time;
            TPOtherCooldown += TPOtherInitCooldown;
            TeleportClientRpc();
            StartCoroutine(EndTP());

        }

        [ClientRpc]
        public void TeleportClientRpc()
        {
            tpRing.CompleteTeleport();
            tpRing.BurstEffects();
        }

        private IEnumerator EndTP()
        {
            yield return new WaitForSeconds(1.5f);
            NewStateClientRpc(HarbingerStates.Moving);
        }

        [ClientRpc]
        public void StartTeleportEffectsClientRpc()
        {
            tpRing.VisualEffectsActive();
        }
        
        [ClientRpc]
        public void CancelTeleportClientRpc()
        {
            tpRing.Cancel();
        }

        [ClientRpc]
        public void AskClientsToTeleportOwnedEnemyClientRpc()
        {
            
        }


        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force,playerWhoHit,playHitSFX, hitID);
            if (playerWhoHit == null || !playerWhoHit.IsOwner)
            {
                return;
            }
            List<EnemyAI> validEnemies = new List<EnemyAI>();
            foreach (EnemyAI Ai in RoundManager.Instance.SpawnedEnemies)
            {
                if (Ai.isOutside == this.isOutside && !Ai.isEnemyDead && Vector3.Distance(Ai.transform.position, transform.position) > 10f)
                {
                    validEnemies.Add(Ai);
                }
            }
            if(validEnemies.Count > 0)
            {
                
                Vector3 enemypos = validEnemies[UnityEngine.Random.Range(0, validEnemies.Count - 1)].transform.position;
                Ray r = new Ray(enemypos, Vector3.down);
                RaycastHit Rhit;
                
                if (Physics.Raycast(r, out Rhit, 5f))
                {
                    enemypos = Rhit.point;
                }

                NavMeshHit hit = new NavMeshHit();
                int tries = 0;
                while (!hit.hit && tries < 10)
                {
                    Vector2 RandomPoint = UnityEngine.Random.insideUnitCircle.normalized.normalized * UnityEngine.Random.Range(3, 6);
                    Vector3 TpPos = enemypos + new Vector3(RandomPoint.x, 0, RandomPoint.y);
                    //HarbingerLoader.mls.LogInfo("Pos: " + TpPos);
                    NavMesh.SamplePosition(TpPos, out hit, .5f, NavMesh.AllAreas);
                    HarbingerLoader.mls.LogInfo("Nav Pos: " + hit.position);

                    tries++;
                }
                if(tries < 10)
                {
                    playerWhoHit.TeleportPlayer(hit.position, false);
                }
                else
                {
                    playerWhoHit.TeleportPlayer(ChooseFarthestNodeFromPosition(mainEntrancePosition).position, false);
                }

            }
            else
            {
                playerWhoHit.TeleportPlayer(ChooseFarthestNodeFromPosition(mainEntrancePosition).position, false);
            }
            
        }

        public static bool MonsterBlackList(EnemyAI toTest)
        {
            if(toTest == null)
            {
                return false;
            }
            else if(toTest is DressGirlAI || toTest is CentipedeAI || toTest is SandSpiderAI)
            {
                return false;
            }
            return true;
        }


    }
}

using BepInEx.Configuration;
using GameNetcodeStuff;
using HarbingerBehaviour.AICode;
using HarbingerBehaviour.Items;
using HarmonyLib;
using LethalLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;

namespace HarbingerBehaviour.ConfigSync
{

    [Serializable]
    public class Config : SyncedInstance<Config>
    {
        [NonSerialized]
        public ConfigItem<float> TeleportSpeed;
        public ConfigItem<float> TPSelfCooldown;
        public ConfigItem<float> TPOtherCooldown;
        public ConfigItem<int> MaxCount;
        public ConfigItem<string> HarbingerSpawnLocations;
        public ConfigItem<string> BlackList;
        public ConfigItem<string> WhiteList;
        public ConfigItem<bool> CanTPItems;


        public ConfigItem<bool> CanTeleportSelf;

        public ConfigItem<bool> CanCreateFractures;
        public ConfigItem<int> SimultaneousFractures;
        public ConfigItem<float> StunDuration;
        public ConfigItem<float> ShockDifficulty;

        public ConfigItem<float> RealityFractureChance;
        public ConfigItem<int> MinFractureValue;
        public ConfigItem<int> MaxFractureValue;
        public ConfigItem<int> FractureCooldown;
        public ConfigItem<int> FractureDamageDelt;

        public ConfigItem<bool> HarbingerCanDie;
        public ConfigItem<int> HarbingerHealth;

        public ConfigItem<bool> TeleportRandom;
        public ConfigItem<bool> TpOnHarbingerTouch;


        public Config()
        {
            InitInstance(this);

        }

        public Config(ConfigFile cfg)
        {
            InitInstance(this);

            // ...
            HarbingerCanDie = cfg.Bind("Health", "Can Harbinger Die", defaultValue: true, "If true Harbingers can be killed by breaking Space Fractures");
            HarbingerHealth = cfg.Bind("Health", "Hitpoints", defaultValue: 3, "How many fractures need to break to kill a harbinger");

            TeleportSpeed = cfg.Bind("Teleport Enemies", "Teleportation speed multiplier", defaultValue: 1f, "How fast the telporation animation should play (default 1 -> 3.35 Sec)");
            TPOtherCooldown = cfg.Bind("Teleport Enemies", "Teleport Enemies Cooldown", defaultValue: 10f, "How frequantly should the harbinger teleport enemies to players (default 15 sec). Minimum of 10 seconds");
            BlackList = cfg.Bind("Teleport Enemies", "Teleportation BlackList", "none", "Add the names of the entities that should not be teleported (This must be the internal name of the entities). Make sure to separate each name with a comma and a space (PjonkGoose, Harbinger)");
            WhiteList = cfg.Bind("Teleport Enemies", "Teleportation WhiteList", "none", "An alternative to the blacklist. If this value is not default it will only teleport enemies that are part of the whitelist.");
            CanTPItems = cfg.Bind("Teleport Items", "Can Teleport Items", defaultValue: true, "This allows you to turn off the ability to teleport the apparatus.");

            CanTeleportSelf = cfg.Bind("Teleport Self", "Can TP Self", defaultValue: true, "Can the harbinger teleport itself to players.");
            TPSelfCooldown = cfg.Bind("Teleport Self", "Teleport Self Cooldown", defaultValue: 20f, "How frequantly should the harbinger teleport to players (default 20 sec). Minimum of 5 seconds");

            CanCreateFractures = cfg.Bind("Harbinger Space Fracture", "Can Create Space Fractures", defaultValue: true, "Harbingers can spawn Fractures on teleport if enabled. Note: if disabled harbingers can't die. If enabled but teleport self isn't, it will create fractures on teleporting enemies.");
            SimultaneousFractures = cfg.Bind("Harbinger Space Fracture", "Max Simultaneous Fractures", defaultValue: 3, "How many fractures can a Harbinger have active at the same time.");
            StunDuration = cfg.Bind("Harbinger Space Fracture", "Fracture stun Duration", defaultValue: 4f, "How long a fracture needs to be stunned for it to break");
            ShockDifficulty = cfg.Bind("Harbinger Space Fracture", "Fracture stun Difficulty", defaultValue: 1.2f, "How hard the zap-gun minigame is when attacking a fracture. (1.1 is easy, 1.5+ is hard)");

            RealityFractureChance = cfg.Bind("Reality Fracture", "Drop Chance", defaultValue: 0.3f, "How Frequantly a Reality Fracture item should be dropped from Space Fractures (0 for disabled and 1 for always)");
            MinFractureValue = cfg.Bind("Reality Fracture", "Min Value", defaultValue: 50, "The lowest a dropped fragment should be worth");
            MaxFractureValue = cfg.Bind("Reality Fracture", "Max Value", defaultValue: 100, "The highest a dropped fragment should be worth");
            FractureCooldown = cfg.Bind("Reality Fracture", "Reality Fracture Cooldown", defaultValue: 60, "The cooldown of using a Reality fracture to teleport (in seconds)");
            FractureDamageDelt = cfg.Bind("Reality Fracture", "Fracture Self Damage", defaultValue: 20, "The amount of damage a fracture should do to a player when used");

            MaxCount = cfg.Bind("Spawning", "Maximum Harbingers", defaultValue: 1, "What is the maximum number of harbingers that should be able to spawn (defaut 1)");
            HarbingerSpawnLocations = cfg.Bind("Spawning", "Moon Spawn Weight", "Modded:35,ExperimentationLevel:10,AssuranceLevel:10,VowLevel:10,OffenseLevel:20,MarchLevel:20,RendLevel:40,DineLevel:40,TitanLevel:50,AdamanceLevel:20,EmbrionLevel:20,ArtificeLevel:50", "Rarety of Harbinger spawning on Each Moon (works will LLL as well).");

            TeleportRandom = cfg.Bind("Misc", "Change retaliate to TP random", false, "If set will teleport the player to a random location if touched or attacked, instead of teleporting them to enemies.");
            TpOnHarbingerTouch = cfg.Bind("Misc", "TP On Touch", true, "If set will teleport players on physical contatct with a harbinger instead of just when attacked.");
        }

        public static void RequestSync()
        {
            if (!IsClient) return;

            using FastBufferWriter stream = new FastBufferWriter(IntSize, Allocator.Temp);
            MessageManager.SendNamedMessage("Harbinger_OnRequestConfigSync", 0uL, stream);
        }

        public static void OnRequestSync(ulong clientId, FastBufferReader _)
        {
            if (!IsHost) return;

            HarbingerLoader.mls.LogInfo($"Config sync request received from client: {clientId}");

            byte[] array = SerializeToBytes(Instance);
            int value = array.Length;

            using FastBufferWriter stream = new FastBufferWriter(value + IntSize, Allocator.Temp);

            try
            {
                stream.WriteValueSafe(in value, default);
                stream.WriteBytesSafe(array);

                MessageManager.SendNamedMessage("Harbinger_OnReceiveConfigSync", clientId, stream);
            }
            catch (Exception e)
            {
                HarbingerLoader.mls.LogInfo($"Error occurred syncing config with client: {clientId}\n{e}");
            }
        }

        public static void OnReceiveSync(ulong _, FastBufferReader reader)
        {
            if (!reader.TryBeginRead(IntSize))
            {
                HarbingerLoader.mls.LogError("Config sync error: Could not begin reading buffer.");
                return;
            }

            reader.ReadValueSafe(out int val, default);
            if (!reader.TryBeginRead(val))
            {
                HarbingerLoader.mls.LogError("Config sync error: Host could not sync.");
                return;
            }

            byte[] data = new byte[val];
            reader.ReadBytesSafe(ref data, val);

            SyncInstance(data);
            if (Instance != null)
            {
                HarbingerLoader.mls.LogInfo("OnReceiveSync: Successfully synced instance!");
                SetUpPrefabVariables();

            }
            else
            {
                HarbingerLoader.mls.LogError("OnReceiveSync: Instance is still null after sync!");

            }
            //HarbingerLoader.mls.LogInfo("Successfully synced config with host.");
            //SetUpPrefabVariables();
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        public static void InitializeLocalPlayer()
        {
            if (IsHost)
            {
                MessageManager.RegisterNamedMessageHandler("Harbinger_OnRequestConfigSync", OnRequestSync);
                Synced = true;
                SetUpPrefabVariables();
                return;
            }
            HarbingerLoader.mls.LogInfo("I patched here");
            Synced = false;
            MessageManager.RegisterNamedMessageHandler("Harbinger_OnReceiveConfigSync", OnReceiveSync);
            RequestSync();



        }


        public static void SetUpPrefabVariables()
        {
            HarbingerAI harbingerAI = HarbingerLoader.VoidHarbinger.enemyPrefab.GetComponent<HarbingerAI>();
            //harbingerAI.enemyHP = SyncedInstance<Config>.Instance.HarbingerHealth.Value;
            if (harbingerAI == null)
            {
                HarbingerLoader.mls.LogError("Harbinger has not been create???");
                return;
            }
            if (Instance == null)
            {
                HarbingerLoader.mls.LogError("Did not sync???");
                return;

            }

            if (Instance.HarbingerHealth == null)
            {
                HarbingerLoader.mls.LogError("Health is not set...");
                HarbingerLoader.mls.LogError("type: " + Instance);
                return;
            }
            HarbingerLoader.VoidHarbinger.MaxCount = Instance.MaxCount.Value;
            HarbingerLoader.VoidHarbinger.canDie = Instance.HarbingerCanDie.Value;
            harbingerAI.enemyHP = Instance.HarbingerHealth.Value;
            harbingerAI.CanTeleportSelf = Instance.CanTeleportSelf.Value;
            harbingerAI.RandomTeleport = Instance.TeleportRandom.Value;
            harbingerAI.teleportOnContact = Instance.TpOnHarbingerTouch.Value;

            harbingerAI.CanCreateFractures = Instance.CanCreateFractures.Value;
            harbingerAI.AllowedSimultaneousFractures = Instance.SimultaneousFractures.Value;
            harbingerAI.CanTeleportSelf = Instance.CanTeleportSelf.Value;

            SpaceFractureEnemy spaceFractureEnemy = HarbingerLoader.HarbingerFractur.GetComponent<SpaceFractureEnemy>();
            spaceFractureEnemy.NeededStunDuration = Instance.StunDuration.Value;
            spaceFractureEnemy.enemyType.stunGameDifficultyMultiplier = Instance.ShockDifficulty.Value;
            spaceFractureEnemy.dropChance = Instance.RealityFractureChance.Value;

            HarbingerLoader.RealityFragment.minValue = Instance.MinFractureValue.Value;
            HarbingerLoader.RealityFragment.maxValue = Instance.MaxFractureValue.Value;
            HarbingerLoader.RealityFragment.spawnPrefab.GetComponent<RealityFracture>().useCooldown = Instance.FractureCooldown.Value;
            HarbingerLoader.RealityFragment.spawnPrefab.GetComponent<RealityFracture>().DamagePlayer = Instance.FractureDamageDelt.Value;

            HarbingerLoader.mls.LogInfo("Item cooldown: " + HarbingerLoader.RealityFragment.spawnPrefab.GetComponent<RealityFracture>().useCooldown);
            HarbingerLoader.mls.LogInfo("Config Item  cooldown: " + HarbingerLoader.RealityFragment.spawnPrefab.GetComponent<RealityFracture>().useCooldown);

        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        public static void PlayerLeave()
        {
            RevertSync();
        }

    }
}

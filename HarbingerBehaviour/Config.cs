using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;

namespace HarbingerBehaviour
{

    [Serializable]
    public class Config : SyncedInstance<Config>
    {

        public readonly ConfigEntry<float> TeleportSpeed;
        public readonly ConfigEntry<float> TPSelfCooldown;
        public readonly ConfigEntry<float> TPOtherCooldown;
        public readonly ConfigEntry<int> MaxCount;
        public readonly ConfigEntry<String> HarbingerSpawnLocations;
        public readonly ConfigEntry<String> BlackList;
        public readonly ConfigEntry<String> WhiteList;
        public readonly ConfigEntry<bool> CanTPItems;


        public Config()
        {
            InitInstance(this);
            
        }

        public Config(ConfigFile cfg)
        {
            InitInstance(this);

            // ...
            TeleportSpeed = cfg.Bind("Teleport Enemies", "Teleportation speed multiplier", defaultValue: 1f, "How fast the telporation animation should play (default 1 -> 3.35 Sec)");
            TPOtherCooldown = cfg.Bind("Teleport Enemies", "Teleport Enemies Cooldown", defaultValue: 10f, "How frequantly should the harbinger teleport enemies to players (default 15 sec). Minimum of 10 seconds");
            TPSelfCooldown = cfg.Bind("Teleport Self", "Teleport Self Cooldown", defaultValue: 20f, "How frequantly should the harbinger teleport to players (default 20 sec). Minimum of 5 seconds");
            MaxCount = cfg.Bind("Spawning", "Maximum Harbingers", defaultValue: 1, "What is the maximum number of harbingers that should be able to spawn (defaut 1)");
            HarbingerSpawnLocations = cfg.Bind("Spawning", "Moon Spawn Weight", "Modded:35,ExperimentationLevel:10,AssuranceLevel:10,VowLevel:10,OffenseLevel:20,MarchLevel:20,RendLevel:40,DineLevel:40,TitanLevel:50,AdamanceLevel:20,EmbrionLevel:20,ArtificeLevel:50", "Rarety of Harbinger spawning on Each Moon (works will LLL as well).");
            BlackList = cfg.Bind("Teleport Enemies", "Teleportation BlackList", "none", "Add the names of the entities that should not be teleported (This must be the internal name of the entities). Make sure to separate each name with a comma and a space (PjonkGoose, Harbinger)");
            WhiteList = cfg.Bind("Teleport Enemies", "Teleportation WhiteList", "none", "An alternative to the blacklist. If this value is not default it will only teleport enemies that are part of the whitelist.");
            CanTPItems = cfg.Bind("Teleport Items", "Can Teleport Items", defaultValue: true, "This allows you to turn off the ability to teleport the apparatus.");

        }

        public static void RequestSync()
        {
            if (!IsClient) return;

            using FastBufferWriter stream = new FastBufferWriter(IntSize, Allocator.Temp);
            MessageManager.SendNamedMessage("ModName_OnRequestConfigSync", 0uL, stream);
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

                MessageManager.SendNamedMessage("ModName_OnReceiveConfigSync", clientId, stream);
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

            HarbingerLoader.mls.LogInfo("Successfully synced config with host.");
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        public static void InitializeLocalPlayer()
        {
            if (IsHost)
            {
                MessageManager.RegisterNamedMessageHandler("ModName_OnRequestConfigSync", OnRequestSync);
                Synced = true;

                return;
            }

            Synced = false;
            MessageManager.RegisterNamedMessageHandler("ModName_OnReceiveConfigSync", OnReceiveSync);
            RequestSync();
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        public static void PlayerLeave()
        {
            Config.RevertSync();
        }

    }
}

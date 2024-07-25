using LethalLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;

using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;

namespace HarbingerBehaviour
{
    [Serializable]
    public class SyncedInstance<T>
    {
        internal static CustomMessagingManager MessageManager => NetworkManager.Singleton.CustomMessagingManager;
        internal static bool IsClient => NetworkManager.Singleton.IsClient;
        internal static bool IsHost => NetworkManager.Singleton.IsHost;

        [NonSerialized]
        protected static int IntSize = 4;

        public static T Default { get; private set; }
        public static T Instance { get; private set; }

        public static bool Synced { get; internal set; }

        protected void InitInstance(T instance)
        {
            Default = instance;
            Instance = instance;

            // Makes sure the size of an integer is correct for the current system.
            // We use 4 by default as that's the size of an int on 32 and 64 bit systems.
            IntSize = sizeof(int);
        }

        internal static void SyncInstance(byte[] data)
        {
            Instance = DeserializeFromBytes(data);
            Synced = true;
        }

        internal static void RevertSync()
        {
            Instance = Default;
            Synced = false;
        }

        public static byte[] SerializeToBytes(T val)
        {
            
            using MemoryStream stream = new MemoryStream();

            try
            {
                var s = new XmlSerializer(typeof(T));
                s.Serialize(stream, val);
                return stream.ToArray();
            }
            catch (Exception e)
            {
                HarbingerLoader.mls.LogError($"Error serializing instance: {e}");
                return null;
            }
        }

        public static T DeserializeFromBytes(byte[] data)
        {
            var s = new XmlSerializer(typeof(T));

            using MemoryStream stream = new MemoryStream(data);

            try
            {
                return (T)s.Deserialize(stream);
            }
            catch (Exception e)
            {
                HarbingerLoader.mls.LogError($"Error deserializing instance: {e}");
                return default;
            }
        }
    }
}

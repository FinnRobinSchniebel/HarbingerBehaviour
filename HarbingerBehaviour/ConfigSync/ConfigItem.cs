using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace HarbingerBehaviour.ConfigSync
{
    [Serializable]
    public class ConfigItem<T>
    {

        public T Value;
        [NonSerialized]
        public readonly ConfigEntry<T> entry;

        ConfigItem()
        {

        }

        public ConfigItem(ConfigEntry<T> item)
        {
            entry = item;
            Value = item.Value;

        }


        public static implicit operator ConfigItem<T>(ConfigEntry<T> configEntry)
        {
            return new ConfigItem<T>(configEntry);
        }

    }
}

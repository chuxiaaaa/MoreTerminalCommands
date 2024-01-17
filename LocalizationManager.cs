using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using static IngamePlayerSettings;
using MoreTerminalCommands.Lang;

namespace MoreTerminalCommands
{
    public class LocalizationManager
    {
        private static ResourceManager resourceManager;

        public static Dictionary<string, Type> Languages = new Dictionary<string, Type>
        {
            { "en_us", typeof(en_US) },
            { "zh_cn", typeof(zh_CN) }
        };

        public static void SetLanguage(string language)
        {
            if (Languages.ContainsKey(language))
            {
                resourceManager = new ResourceManager(Languages[language]);
            }
            else
            {
                resourceManager = new ResourceManager(typeof(en_US));
            }
        }

        public static string TryGetString(string prefix, string key)
        {
            try
            {
                string value = resourceManager.GetString(prefix + key);
                return value == null ? key : value.TrimEnd();
            }
            catch (Exception)
            {
                return key;
            }
        }

        public static string GetString(string key)
        {
            try
            {
                return resourceManager.GetString(key);
            }
            catch (Exception)
            {
                return "Missing translation for key: " + key;
            }
        }
    }
}

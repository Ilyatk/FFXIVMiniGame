using System.ComponentModel;
using System.Configuration;
using System.IO;
using Clio.Utilities;
using ff14bot.Helpers;
using Newtonsoft.Json;

namespace MiniGameBot
{
    public class MiniGameSettings : JsonSettings
    {
        [JsonIgnore]
        private static MiniGameSettings _instance;
        public static MiniGameSettings Instance { get { return _instance ?? (_instance = new MiniGameSettings("MiniGameSettings")); } }
        public MiniGameSettings(string filename) : base(Path.Combine(CharacterSettingsDirectory, "MiniGameSettings.json")) { }

        /*
         "Cuff-a-Cur",
         "Crystal Tower Striker",
         "The Moogle's Paw",
         "Monster Toss",
         */
        [Setting, DefaultValue("Cuff-a-Cur")]
        public string SelectedGame { get; set; }

        // INFO: If count == -1 than continue bot infinity
        [Setting, DefaultValue(4)]
        public int Count { get; set; }
    }
}
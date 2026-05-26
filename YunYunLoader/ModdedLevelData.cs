using Newtonsoft.Json;
using YYNoteEditor;

namespace YunYunLoader
{
    internal class ModdedLevelData
    {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public string? Editor;
        public int Difficulty;
        public string? Path;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

        [JsonIgnore]
        public ScoreData? Data;

        [JsonIgnore]
        public string? ID;
    }
}

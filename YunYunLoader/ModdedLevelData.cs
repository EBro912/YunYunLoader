using Newtonsoft.Json;
using System;
using YYNoteEditor;

namespace YunYunLoader
{
    internal class ModdedLevelData
    {
        public string? Editor;
        public int Difficulty;
        public string? Path;

        [JsonIgnore]
        public ScoreData? Data;

        [JsonIgnore]
        public string? ID;
    }
}

using System.Collections.Generic;

namespace YunYunLoader
{
    internal class ModdedScoreData
    {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public string? ID;
        public string? Audio;
        public string? Icon;
        public string? Title;
        public string? Artist;
        public string? ListArtist;
        public string? Lyricist;
        public string? Composer;
        public string? Arranger;
        public List<ModdedLevelData>? Levels;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    }
}

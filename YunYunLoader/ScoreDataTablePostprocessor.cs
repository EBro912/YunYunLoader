using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace YunYunLoader
{
    // unity wipes localization tables whenever the language is changed
    // luckily, the game doesn't utilize the postprocessor so we can utilize it ourselves to repopulate modded metadata
    internal class ScoreDataTablePostprocessor : ITablePostprocessor
    {
        private readonly ITablePostprocessor? Previous;

        public ScoreDataTablePostprocessor(ITablePostprocessor? previous)
        {
            Previous = previous;
        }

        public void PostprocessTable(LocalizationTable table)
        {
            Previous?.PostprocessTable(table);

            if (table is StringTable stringTable && table.TableCollectionName == "ScoreData")
                Plugin.PopulateScoreDataTable(stringTable);
        }
    }
}

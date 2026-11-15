using System.Collections.Generic;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// What a received item does once. Permanent effects (a blueprint's recipe) are not applied
    /// here: they are derived from the ledger every time (see BlueprintRando.Reconcile), so they
    /// survive loading a checkpoint without ever being doubled.
    /// </summary>
    internal static class ItemEffects
    {
        public static void ApplyOnce(long itemId)
        {
            Registry.Entry entry;
            if (!Registry.Items.TryGetValue(itemId, out entry))
            {
                Plugin.Log.LogWarning("Unknown item id " + itemId);
                return;
            }
            List<KeyValuePair<string, int>> contents;
            if (Registry.FillerContents.TryGetValue(itemId, out contents))
            {
                foreach (var pair in contents)
                {
                    CardData card = UniqueIDScriptable.GetFromID<CardData>(pair.Key);
                    if (card == null) continue;
                    for (int i = 0; i < pair.Value; i++)
                    {
                        GameManager.GiveCard(card, true);
                    }
                }
            }
        }
    }
}

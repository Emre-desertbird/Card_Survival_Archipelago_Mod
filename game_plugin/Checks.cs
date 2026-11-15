using System.Linq;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Checks that come from playing (research checks live in BlueprintRando):
    /// - "Build - X": the last construction step of blueprint X finishes. The game reports every
    ///   finished action through GameManager.OnActionPerformed (its own journal uses it too); only
    ///   the final step carries the result collection "&lt;blueprint&gt;_blueprint". Cards handed out by
    ///   Archipelago never pass through a build action, so they can never complete this check.
    /// - "Visit - Area": the player is in that area (polled, so the starting area counts too).
    /// - "First time" checks (Catch, Hunt, Cook, Plant): a watched card exists in the game. All
    ///   cards are scanned a few times per second; every watched card ever seen is remembered for
    ///   good (SlotState.Seen), so "three different crops" also counts crops seen days apart.
    ///   Finds never contain a watched card (tools/build_registry.py refuses that), so items from
    ///   Archipelago cannot complete these checks.
    /// </summary>
    internal static class Checks
    {
        private static CardData lastEnvironment;
        private static float nextScan;

        public static void Install()
        {
            GameManager.OnActionPerformed += OnActionPerformed;
        }

        private static void OnActionPerformed(ActionReport report)
        {
            if (!ApGame.Active || report.Cancelled || report.FromCard == null || report.Action == null) return;
            if (report.FromCard.CardType != CardTypes.Blueprint && report.FromCard.CardType != CardTypes.EnvImprovement) return;
            Registry.Entry location;
            if (!Registry.BuildByCard.TryGetValue(report.FromCard.UniqueID, out location)) return;
            string finished = report.FromCard.name + "_blueprint";
            if (report.Action.ProducedCards != null && report.Action.ProducedCards.Any(c => c != null && c.CollectionName == finished))
            {
                ApGame.CompleteLocation(location.Id);
            }
        }

        /// <summary>Called every frame while a randomized game is running.</summary>
        public static void Update(GameManager gm)
        {
            CardData environment = gm.CurrentEnvironment;
            if (environment == null || environment == lastEnvironment) return;
            lastEnvironment = environment;
            Registry.Entry location;
            if (Registry.VisitByCard.TryGetValue(environment.UniqueID, out location))
            {
                ApGame.CompleteLocation(location.Id);
            }
        }

        /// <summary>Remembers watched cards that exist now and completes the checks they finish.</summary>
        public static void ScanCards(GameManager gm)
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.25f;
            bool changed = false;
            foreach (InGameCardBase card in gm.AllCards)
            {
                if (card == null || card.Destroyed || card.CardModel == null) continue;
                string id = card.CardModel.UniqueID;
                if (!Registry.WatchedCards.Contains(id) || ApGame.State.HasSeen(id)) continue;
                ApGame.State.AddSeen(id);
                changed = true;
            }
            if (!changed) return;
            ApGame.State.Write();
            EvaluateWatched();
        }

        /// <summary>Completes every enabled "first time" check whose conditions hold.</summary>
        public static void EvaluateWatched()
        {
            foreach (Registry.Entry location in Registry.Watched)
            {
                if (ApGame.State.IsChecked(location.Id) || !ApGame.State.LocationKinds.Contains(location.Kind)) continue;
                if (location.Watch.All(part => part.Cards.Count(ApGame.State.HasSeen) >= part.Count))
                {
                    ApGame.CompleteLocation(location.Id);
                }
            }
        }

        public static void Reset()
        {
            lastEnvironment = null;
            nextScan = 0f;
        }
    }
}

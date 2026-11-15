using System.Linq;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Checks that come from playing (research checks live in BlueprintRando):
    /// - "Build - X": the last construction step of blueprint X finishes. The game reports every
    ///   finished action through GameManager.OnActionPerformed (its own journal uses it too); only
    ///   the final step carries the result collection "&lt;blueprint&gt;_blueprint". Cards handed out by
    ///   Archipelago never pass through a build action, so they can never complete this check.
    /// - "Visit - Area": the player is in that area (polled, so the starting area counts too).
    /// </summary>
    internal static class Checks
    {
        private static CardData lastEnvironment;

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

        public static void Reset()
        {
            lastEnvironment = null;
        }
    }
}

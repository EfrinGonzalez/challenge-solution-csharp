namespace Challenge.src.Domain.Extensions
{
    public static class OrderStateExtensions
    {
        /// <summary>
        /// Applies freshness decay since the last update and advances the timestamp.
        /// Clamps at 0 to avoid negative budgets.
        /// </summary>
        public static void AccumulateDecay(this OrderState s, DateTime nowUtc)
        {
            var elapsed = (nowUtc - s.LastUpdateUtc).TotalSeconds;
            if (elapsed <= 0) return;

            s.BudgetSec -= elapsed * s.Rate;   // Rate: 1 at ideal, 2 off-ideal
            if (s.BudgetSec < 0) s.BudgetSec = 0;
            s.LastUpdateUtc = nowUtc;
        }
    }
}

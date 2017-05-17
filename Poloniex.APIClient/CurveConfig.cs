using System;
using System.Collections.Generic;
using System.Text;

namespace Poloniex.APIClient
{
    public class CurveConfig
    {
        /// <summary>
        /// Length of the curve (period of data to analyze)
        /// </summary>
        public TimeSpan PeriodLength { get; set; }

        /// <summary>
        /// Minimal curve percentage required, positive curve means a drop, negative curve means a raise
        /// </summary>
        public decimal MinCurvePct { get; set; }
        /// <summary>
        /// Maximum value of the data in the opposite direction of the curve to allow DURING the curve
        /// </summary>
        public decimal MaxOppositeCurvePctDuringCurve { get; set; }
        /// <summary>
        /// Maximum value of the data in the direction of the curve to allow DURING the curve
        /// </summary>
        public decimal MaxCurvePctDuringCurve { get; set; }
        /// <summary>
        /// Length of the last/final period of the curve to have the most steep part of the curve
        /// </summary>
        public TimeSpan MostRecentPeriodLength { get; set; }

        public static CurveConfig Drop(int periodLengthMinutes = 30, decimal minCurvePct = 0.05m)
        {
            var periodLength = TimeSpan.FromMinutes(periodLengthMinutes);

            return new CurveConfig
            {
                PeriodLength = periodLength,
                MinCurvePct = minCurvePct,
                MaxOppositeCurvePctDuringCurve = 0.1m * minCurvePct, // 10% of minCurvePct
                MaxCurvePctDuringCurve = 0.5m * minCurvePct, // 50% of minCurvePct
                MostRecentPeriodLength = TimeSpan.FromTicks(Convert.ToInt64(0.05m * Convert.ToDecimal(periodLength.Ticks))), // Last 5% of period
            };
        }


        /*                *
         *                *
         *               *
         *           ****
         *    *******
         */

        public static CurveConfig Raise(int periodLengthMinutes = 30, decimal minCurvePct = -0.05m)
        {
            var periodLength = TimeSpan.FromMinutes(periodLengthMinutes);

            return new CurveConfig
            {
                PeriodLength = periodLength,
                MinCurvePct = minCurvePct,
                MaxOppositeCurvePctDuringCurve = 0.1m * minCurvePct, // 10% of minCurvePct
                MaxCurvePctDuringCurve = 0.5m * minCurvePct, // 50% of minCurvePct
                MostRecentPeriodLength = TimeSpan.FromTicks(Convert.ToInt64(0.05m * Convert.ToDecimal(periodLength.Ticks))), // Last 5% of period
            };
        }
    }
}

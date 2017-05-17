using Poloniex.APIClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Poloniex.APIClient
{
    public class DataAnalyzer
    {
        private readonly List<Ticker> RawData;
        private readonly DateTime PeriodStart;
        private readonly DateTime PeriodEnd;

        public DataAnalyzer(IEnumerable<Ticker> rawData)
        {
            RawData = rawData.OrderBy(t => t.Timestamp).ToList();
            PeriodStart = RawData.First().Timestamp;
            PeriodEnd = RawData.Last().Timestamp;
        }

        private IEnumerable<Ticker> Aggregate(IEnumerable<Ticker> tickerData, TimeSpan groupsize)
        {
            // Make groups
            List<Ticker> groups = new List<Ticker>();

            Ticker curGroup = null;
            List<Ticker> groupValues = new List<Ticker>();

            foreach (var cv in tickerData)
            {
                if (curGroup != null && cv.Timestamp < curGroup.Timestamp.Add(groupsize))
                {
                    groupValues.Add(cv);
                }
                else
                {
                    if (curGroup != null)
                    {
                        // Finish open group
                        curGroup.HighestBid = groupValues.Max(t => t.HighestBid);
                        curGroup.LowestAsk = groupValues.Min(t => t.LowestAsk);
                        curGroup.IsFrozen = groupValues.Last().IsFrozen;
                        curGroup.Last = groupValues.Last().Last;
                        curGroup.Timestamp = groupValues.Last().Timestamp; // Set timestamp to LAST instead of first
                        curGroup.TwentyFourHrLow = groupValues.Min(t => t.TwentyFourHrLow);
                        curGroup.TwentyFourHrHigh = groupValues.Max(t => t.TwentyFourHrHigh);
                        curGroup.Average = groupValues.Average(gv => gv.Last);
                        groups.Add(curGroup);
                    }

                    // Create new group
                    curGroup = new Ticker
                    {
                        BaseCurrency = cv.BaseCurrency,
                        Currency = cv.Currency,
                        Timestamp = cv.Timestamp
                    };
                    groupValues = new List<Ticker>();
                    groupValues.Add(cv);
                }
            }

            if (curGroup != null)
            {
                // Finish open group
                curGroup.HighestBid = groupValues.Max(t => t.HighestBid);
                curGroup.LowestAsk = groupValues.Min(t => t.LowestAsk);
                curGroup.IsFrozen = groupValues.Last().IsFrozen;
                curGroup.Last = groupValues.Last().Last;
                curGroup.Timestamp = groupValues.Last().Timestamp; // Set timestamp to LAST instead of first
                curGroup.TwentyFourHrLow = groupValues.Min(t => t.TwentyFourHrLow);
                curGroup.TwentyFourHrHigh = groupValues.Max(t => t.TwentyFourHrHigh);
                curGroup.Average = groupValues.Average(gv => gv.Last);
                groups.Add(curGroup);
            }

            return groups;
        }

        public AnalysisResult CheckCurve(DateTime time, CurveConfig curveConfig, int groupsizeSeconds = 30)
        {
            var messages = new List<string>();

            try
            {
                var groups = Aggregate(RawData, TimeSpan.FromSeconds(groupsizeSeconds));

                if (groups.Count() > 2)
                {

                    var hasDiff = (groups.Last().Last != groups.First().Last);

                    // Drop: 1 - (60/80) = 1 - .75 = .25
                    // Raise: 1 - (80/60) = 1 - 1.33 = -.33
                    var curvePct = 1m - (groups.Last().Last / groups.First().Last);
                    var isOkCurve = false;

                    if (hasDiff)
                    {
                        var curveSteepEnough = ((curveConfig.MinCurvePct > 0 && curvePct >= curveConfig.MinCurvePct) // Drop
                            || (curveConfig.MinCurvePct < 0 && curvePct <= curveConfig.MinCurvePct)); // Raise

                        if (curveSteepEnough)
                        {
                            messages.Add($"Curve found of {curvePct * 100:N2} %");
                            var grpMostRecentStart = groups.Where(g => g.Timestamp < groups.Last().Timestamp.AddTicks(-1 * curveConfig.MostRecentPeriodLength.Ticks)).Last(); // Kan null zijn?
                            var curvePctMostRecent = 1m - (groups.Last().Last / grpMostRecentStart.Last);
                            var minCurveMostRecent = curvePct * 0.1m;

                            var finalCurveSteepEnough = ((curveConfig.MinCurvePct > 0 && curvePctMostRecent >= minCurveMostRecent)
                                || (curveConfig.MinCurvePct < 0 && curvePctMostRecent <= minCurveMostRecent));

                            if (finalCurveSteepEnough)
                            {
                                messages.Add($"Steep final curve of {curvePctMostRecent * 100:N2} %");
                                isOkCurve = true;
                            }
                            else
                                messages.Add($"Final curve not steep enough ({curvePctMostRecent * 100:N2} % instead of {(curvePct * 0.1m) * 100:N2} %)");
                        }
                        else
                            messages.Add($"CurvePct {curvePct * 100:N2} % does not meet minimum {curveConfig.MinCurvePct * 100:N2} %.");
                    }
                    else
                        messages.Add($"No diff (first: {groups.First().Last}, last: {groups.Last().Last})");

                    return AnalysisResult.Create(isOkCurve, curvePct, messages);
                }
                else
                {
                    messages.Add($"Not enough data (groupcount: {groups.Count()})");
                    return AnalysisResult.Create(false, 0.0m, messages);
                }
            }
            catch (Exception ex)
            {
                messages.Add($"Exception: {ex})");
                return AnalysisResult.Create(false, 0.0m, messages);
            }
        }
    }
}

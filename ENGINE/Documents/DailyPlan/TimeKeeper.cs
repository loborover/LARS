using System;
using System.Collections.Generic;
using System.Linq;

namespace LARS.ENGINE.Documents.DailyPlan;

public static class TimeKeeper
{
    private static readonly List<(TimeSpan Start, TimeSpan End)> ExclusionIntervals = new()
    {
        // 00:00-08:00 (Early Morning / Night Shift End)
        (new TimeSpan(0, 0, 0), new TimeSpan(8, 0, 0)),
        
        // 10:00-10:10 (Morning Break)
        (new TimeSpan(10, 0, 0), new TimeSpan(10, 10, 0)),
        
        // 12:00-13:00 (Lunch)
        (new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0)),
        
        // 15:00-15:10 (Afternoon Break)
        (new TimeSpan(15, 0, 0), new TimeSpan(15, 10, 0)),
        
        // 17:00-17:30 (Dinner / Overtime Break)
        (new TimeSpan(17, 0, 0), new TimeSpan(17, 30, 0)),
        
        // 19:30-19:40 (Evening Break)
        (new TimeSpan(19, 30, 0), new TimeSpan(19, 40, 0)),
        
        // 20:30-00:00 (Night Constraint - End of Day)
        // Note: Logic handles same-day intervals. 
        // 20:30-24:00 is technically until next day 00:00, but for TimeSpan comparison we treat it carefully.
        (new TimeSpan(20, 30, 0), new TimeSpan(24, 0, 0)) 
        // TimeSpan(24,0,0) is actually 1.00:00:00 (1 day), works for comparison if we handle day overflow.
    };

    /// <summary>
    /// Calculates net duration between two day-times, excluding break times.
    /// Logic mirrors VBA Time_Filtering:
    /// - If Start/End are same day: NetDurationSingleDay.
    /// - If different days: Start Day (Start~Midnight) + End Day (Midnight~End).
    /// - Intermediate days are IGNORED (as per VBA comments "중간 날짜... 무시").
    /// </summary>
    public static TimeSpan CalculateDuration(DateTime startDt, DateTime endDt)
    {
        if (endDt <= startDt) return TimeSpan.Zero;

        DateTime startDay = startDt.Date;
        DateTime endDay = endDt.Date;

        if (startDay == endDay)
        {
            return CalculateNetDurationSingleDay(startDt, endDt);
        }
        else
        {
            // Start Day: From StartTime to Midnight of NEXT day
            DateTime startDayMidnight = startDay.AddDays(1);
            TimeSpan startDuration = CalculateNetDurationSingleDay(startDt, startDayMidnight);

            // End Day: From Midnight of EndDay to EndTime
            DateTime endDayMidnight = endDay; // 00:00:00
            TimeSpan endDuration = CalculateNetDurationSingleDay(endDayMidnight, endDt);

            return startDuration + endDuration;
        }
    }

    private static TimeSpan CalculateNetDurationSingleDay(DateTime rangeStart, DateTime rangeEnd)
    {
        if (rangeEnd <= rangeStart) return TimeSpan.Zero;

        // Base Duration
        TimeSpan totalDuration = rangeEnd - rangeStart;

        // Current range times (relative to day start)
        TimeSpan rangeStartTime = rangeStart.TimeOfDay;
        TimeSpan rangeEndTime = (rangeEnd.Date > rangeStart.Date) ? new TimeSpan(24, 0, 0) : rangeEnd.TimeOfDay;

        // Subtract overlaps with exclusion intervals
        foreach (var (exStart, exEnd) in ExclusionIntervals)
        {
            // Find Overlap
            TimeSpan overlapStart = (rangeStartTime > exStart) ? rangeStartTime : exStart;
            TimeSpan overlapEnd = (rangeEndTime < exEnd) ? rangeEndTime : exEnd;

            if (overlapEnd > overlapStart)
            {
                totalDuration -= (overlapEnd - overlapStart);
            }
        }

        return (totalDuration < TimeSpan.Zero) ? TimeSpan.Zero : totalDuration;
    }
}

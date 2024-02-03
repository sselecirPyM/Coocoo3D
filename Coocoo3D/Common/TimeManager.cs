using System;
using System.Collections.Generic;

namespace Coocoo3D.Common;

public class TimeManager
{
    public double simulationSpeed = 1;
    public long simulationTime;
    public long unscaledTime;

    public long deltaTime;
    public long simulationDeltaTime;

    Dictionary<string, long> timers = new Dictionary<string, long>();

    Dictionary<string, ValueTuple<long, long, double>> counters = new Dictionary<string, ValueTuple<long, long, double>>();

    public void SetSimulationTime(long simulationTime)
    {
        this.simulationTime = simulationTime;
    }

    public void AbsoluteTimeInput(long time)
    {
        deltaTime = time - unscaledTime;
        unscaledTime = time;
        simulationTime += (long)(deltaTime * simulationSpeed);
    }

    public void TimeInput(long deltaTime)
    {
        unscaledTime += deltaTime;
        simulationTime += (long)(deltaTime * simulationSpeed);
        this.deltaTime = deltaTime;
        this.simulationDeltaTime = (long)(deltaTime * simulationSpeed);
    }

    //public bool Timer(string timerName, double period)
    //{
    //    return IncorrectTimer(timers, timerName, simulationTime, (long)(1e7 * period), out _);
    //}

    //public bool Timer(string timerName, double period, out long tick)
    //{
    //    return IncorrectTimer(timers, timerName, simulationTime, (long)(1e7 * period), out tick);
    //}

    //public bool RealTimer(string timerName, double realTimePeriod)
    //{
    //    return IncorrectTimer(timers, timerName, unscaledTime, (long)(1e7 * realTimePeriod), out _);
    //}

    //public bool RealTimer(string timerName, double realTimePeriod, out long tick)
    //{
    //    return IncorrectTimer(timers, timerName, unscaledTime, (long)(1e7 * realTimePeriod), out tick);
    //}

    public bool RealTimerCorrect(string timerName, double realTimePeriod, out double deltaTime)
    {
        bool result = CorrectTimer(timers, timerName, unscaledTime, (long)(1e7 * realTimePeriod), out long previous);
        deltaTime = (unscaledTime - previous) / 1e7;
        return result;
    }

    public bool RealCounter(string timerName, double realTimePeriod, out double rate)
    {
        bool result = false;
        var val = counters.GetValueOrDefault(timerName);
        val.Item2++;
        if (unscaledTime - val.Item1 > realTimePeriod * 1e7)
        {
            val.Item3 = val.Item2 * 1e7 / (unscaledTime - val.Item1);
            val.Item1 = unscaledTime;
            val.Item2 = 0;
            result = true;
        }

        counters[timerName] = val;
        rate = val.Item3;

        return result;
    }

    public double GetDeltaTime()
    {
        return deltaTime / 1e7;
    }

    public double GetSimulationDeltaTime()
    {
        return simulationDeltaTime / 1e7;
    }

    //static bool IncorrectTimer(Dictionary<string, long> dict, string key, long now, long delta, out long value)
    //{
    //    var val = dict.GetValueOrDefault(key);

    //    if (now - val > delta)
    //    {
    //        dict[key] = value = Math.Max(now - delta + 1, val + delta);
    //        return true;
    //    }
    //    value = val;
    //    return false;
    //}

    static bool CorrectTimer(Dictionary<string, long> dict, string key, long now, long delta, out long previous)
    {
        var val = dict.GetValueOrDefault(key);

        previous = val;
        if (now - val > delta)
        {
            dict[key] = now;
            return true;
        }
        return false;
    }
}

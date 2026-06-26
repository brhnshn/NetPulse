using System;
using System.Collections.Concurrent;

namespace NetPulse.App.Core
{
    public class CircuitBreaker
    {
        private class TargetState
        {
            public int ConsecutiveFailures { get; set; }
            public DateTime? TripTimeUtc { get; set; }
        }

        private readonly ConcurrentDictionary<string, TargetState> _states = new();
        private readonly int _maxFailures;
        private readonly TimeSpan _coolDownPeriod;

        public CircuitBreaker(int maxFailures = 10, TimeSpan? coolDownPeriod = null)
        {
            _maxFailures = maxFailures;
            _coolDownPeriod = coolDownPeriod ?? TimeSpan.FromSeconds(60);
        }

        public bool IsTripped(string targetAddress)
        {
            if (!_states.TryGetValue(targetAddress, out var state))
            {
                return false;
            }

            if (state.TripTimeUtc.HasValue)
            {
                if (DateTime.UtcNow - state.TripTimeUtc.Value < _coolDownPeriod)
                {
                    return true;
                }
                else
                {
                    // Cool down finished, reset state
                    lock (state)
                    {
                        state.TripTimeUtc = null;
                        state.ConsecutiveFailures = 0;
                    }
                }
            }

            return false;
        }

        public void RecordSuccess(string targetAddress)
        {
            var state = _states.GetOrAdd(targetAddress, _ => new TargetState());
            lock (state)
            {
                state.ConsecutiveFailures = 0;
                state.TripTimeUtc = null;
            }
        }

        public bool RecordFailure(string targetAddress, out bool trippedNow)
        {
            trippedNow = false;
            var state = _states.GetOrAdd(targetAddress, _ => new TargetState());
            lock (state)
            {
                if (state.TripTimeUtc.HasValue)
                {
                    if (DateTime.UtcNow - state.TripTimeUtc.Value < _coolDownPeriod)
                    {
                        return true;
                    }
                    else
                    {
                        // Cool down expired
                        state.TripTimeUtc = null;
                        state.ConsecutiveFailures = 0;
                    }
                }

                state.ConsecutiveFailures++;
                if (state.ConsecutiveFailures >= _maxFailures)
                {
                    state.TripTimeUtc = DateTime.UtcNow;
                    trippedNow = true;
                    return true;
                }
            }

            return false;
        }

        public void Reset(string targetAddress)
        {
            if (_states.TryGetValue(targetAddress, out var state))
            {
                lock (state)
                {
                    state.ConsecutiveFailures = 0;
                    state.TripTimeUtc = null;
                }
            }
        }

        public int GetConsecutiveFailures(string targetAddress)
        {
            if (_states.TryGetValue(targetAddress, out var state))
            {
                lock (state)
                {
                    return state.ConsecutiveFailures;
                }
            }
            return 0;
        }
    }
}

namespace RagAuth.UnitTests.TestDoubles;

/// <summary>Relógio controlável nos testes; os timers criados por ele disparam ao avançar o relógio.</summary>
public sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
{
    private readonly Lock _lock = new();
    private readonly List<AdjustableTimer> _timers = [];
    private DateTimeOffset _utcNow = initialUtcNow;

    public AdjustableTimeProvider()
        : this(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _utcNow;
        }
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        lock (_lock)
        {
            _utcNow = utcNow;
        }

        FireDueTimers();
    }

    public void Advance(TimeSpan duration) => SetUtcNow(GetUtcNow() + duration);

    public int PendingTimerCount
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count;
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new AdjustableTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private void Schedule(AdjustableTimer timer, TimeSpan dueTime)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                timer.DueAt = _utcNow + dueTime;
                _timers.Add(timer);
            }
        }

        FireDueTimers();
    }

    private void Remove(AdjustableTimer timer)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
        }
    }

    private void FireDueTimers()
    {
        List<AdjustableTimer> dueTimers;
        lock (_lock)
        {
            dueTimers = _timers.Where(timer => timer.DueAt <= _utcNow).ToList();
            _timers.RemoveAll(dueTimers.Contains);
        }

        foreach (var timer in dueTimers)
        {
            timer.Fire();
        }
    }

    private sealed class AdjustableTimer(AdjustableTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            owner.Schedule(this, dueTime);
            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

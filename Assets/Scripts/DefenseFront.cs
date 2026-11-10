using System;
using System.Collections.Generic;

// Logical coordinates are independent of where rooms are placed in the scene.
public sealed class DefenseFront
{
    public enum RoomState { Disabled, Locked, Contested, Secured, Lost }
    public sealed class Room
    {
        public int Id { get; internal set; }
        public int Line { get; internal set; }
        public int Index { get; internal set; }
        public int ParentId { get; internal set; }
        public RoomState State { get; internal set; }
        public int Enemies { get; internal set; }
        public int Waves { get; internal set; }
        internal bool clearRewardClaimed;
        public bool IsActive => State != RoomState.Disabled;
        public bool CanSelect => State == RoomState.Contested || State == RoomState.Secured;
        public bool HasThreat => Enemies > 0 || Waves > 0;
        // Clock layout: hour 1-12 on the dial, 0 when the room is not a clock front.
        public int ClockHour { get; internal set; }
        public bool IsClockCenter { get; internal set; }
        // Clock fronts that were never attacked pay no income.
        public bool EarnsIncome { get; internal set; } = true;
        public string Label => ClockHour > 0 ? ClockHour + " ч"
            : IsClockCenter ? "ЦЕНТР" : Line + "-" + Index;
    }
    private readonly List<Room> rooms = new List<Room>(16);
    private readonly HashSet<int> claimedLineRewards = new HashSet<int>();
    public IReadOnlyList<Room> Rooms => rooms;
    public int OpenedLine { get; private set; }
    public int LastPaidTurn { get; private set; }
    public int EarnedGold { get; private set; }

    public DefenseFront()
    {
        for (int line = 1; line <= 4; line++)
        for (int index = 1; index <= line * 2 - 1; index++)
            rooms.Add(new Room {
                Id = IdFor(line, index), Line = line, Index = index,
                ParentId = ParentIdFor(line, index),
                State = RoomState.Locked
            });
    }
    public static int IdFor(int line, int index) => (line - 1) * (line - 1) + index - 1;
    public static int ParentIdFor(int line, int index)
    {
        if (line <= 1) return -1;
        int parentCount = (line - 1) * 2 - 1;
        int parentIndex = Math.Max(1, Math.Min(index - 1, parentCount));
        return IdFor(line - 1, parentIndex);
    }
    public Room GetRoom(int id) => id >= 0 && id < rooms.Count ? rooms[id] : null;

    public void ConfigureActiveRooms(IEnumerable<int> activeRoomIds)
    {
        if (OpenedLine != 0) return;
        var active = activeRoomIds != null
            ? new HashSet<int>(activeRoomIds)
            : new HashSet<int>();
        foreach (Room room in rooms)
        {
            room.State = active.Contains(room.Id) ? RoomState.Locked : RoomState.Disabled;
            room.Enemies = 0;
            room.Waves = 0;
        }
    }

    public bool IsClockLayout { get; private set; }

    // Clock siege: the main room is the centre and every listed front breaches
    // straight into it. Other rooms are not part of the run.
    public void ConfigureClockLayout(IReadOnlyList<int> frontIds, IReadOnlyList<int> hours)
    {
        if (OpenedLine != 0 || frontIds == null || hours == null) return;
        var active = new List<int>(frontIds) { 0 };
        ConfigureActiveRooms(active);
        IsClockLayout = true;
        rooms[0].IsClockCenter = true;
        for (int i = 0; i < frontIds.Count && i < hours.Count; i++)
        {
            Room room = GetRoom(frontIds[i]);
            if (room == null || room.Id == 0) continue;
            room.ParentId = 0;
            room.ClockHour = hours[i];
        }
    }

    public void SetClockHour(int id, int hour)
    {
        Room room = GetRoom(id);
        if (room != null && IsClockLayout && room.ClockHour > 0) room.ClockHour = hour;
    }

    public void SetEarnsIncome(int id, bool earns)
    {
        Room room = GetRoom(id);
        if (room != null) room.EarnsIncome = earns;
    }

    public Room RoomAtClockHour(int hour)
    {
        foreach (Room room in rooms)
            if (room.ClockHour == hour) return room;
        return null;
    }

    // Opens one front without opening its whole line.
    public bool OpenRoom(int id)
    {
        Room room = GetRoom(id);
        if (room == null || room.State != RoomState.Locked) return false;
        if (OpenedLine == 0) OpenedLine = 1;
        room.State = RoomState.Contested;
        return true;
    }

    public void OpenLine(int line)
    {
        if (line != OpenedLine + 1 || line > 4) return;
        OpenedLine = line;
        foreach (Room room in rooms)
            if (room.Line == line && room.State == RoomState.Locked) room.State = RoomState.Contested;
    }
    public void Observe(int id, int enemies, int waves)
    {
        Room room = GetRoom(id);
        if (room == null || !room.IsActive || room.State == RoomState.Locked) return;
        room.Enemies = Math.Max(0, enemies);
        room.Waves = Math.Max(0, waves);
        if (room.State != RoomState.Lost)
            room.State = room.HasThreat ? RoomState.Contested : RoomState.Secured;
    }
    public bool LoseRoom(int id)
    {
        Room room = GetRoom(id);
        if (room == null || !room.CanSelect) return false;
        // Outer breaches never lose rooms. Only the main room falls, when its
        // barricade is destroyed, and that never affects any other room.
        room.State = RoomState.Lost;
        return true;
    }
    public int LiberateLostRoomsThroughLine(int completedLine)
    {
        // A line is liberated only after every active threat has been resolved.
        if (!IsQuiet(0)) return 0;
        int restored = 0;
        foreach (Room room in rooms)
        {
            if (room.Line > completedLine || room.State != RoomState.Lost || room.HasThreat) continue;
            room.State = RoomState.Secured;
            restored++;
        }
        return restored;
    }
    public bool ClaimFirstClear(int id)
    {
        Room room = GetRoom(id);
        if (room == null || room.State != RoomState.Secured || room.clearRewardClaimed) return false;
        room.clearRewardClaimed = true;
        return true;
    }

    public bool ClaimLineClear(int line)
    {
        if (line < 1 || line > OpenedLine || claimedLineRewards.Contains(line)) return false;
        bool hasRooms = false;
        foreach (Room room in rooms)
        {
            if (room.Line != line || !room.IsActive) continue;
            hasRooms = true;
            if (room.State != RoomState.Secured || room.HasThreat) return false;
        }
        if (!hasRooms) return false;
        claimedLineRewards.Add(line);
        return true;
    }
    public bool IsQuiet(int travellingEnemies)
    {
        if (OpenedLine == 0 || travellingEnemies > 0) return false;
        foreach (Room room in rooms)
            if (room.IsActive && room.State != RoomState.Locked
                && (room.HasThreat || room.State == RoomState.Contested)) return false;
        return true;
    }
    public int Income(int goldPerRoom)
    {
        int secured = 0;
        foreach (Room room in rooms)
            if (room.State == RoomState.Secured && room.EarnsIncome) secured++;
        return secured * Math.Max(0, goldPerRoom);
    }
    public int CompleteTurn(int turn, int goldPerRoom)
    {
        if (turn <= LastPaidTurn) return 0;
        LastPaidTurn = turn;
        int income = Income(goldPerRoom);
        EarnedGold += income;
        return income;
    }
}

// Enemies from a lost room travel inward in cohorts. Their ready turn is based on
// how many cells remained to the breached line when the room was abandoned;
// the nearest minimum cohort from the actual breach is promoted to the current turn.
public sealed class DefensePressureQueue<T>
{
    public sealed class Passage
    {
        public T Enemy;
        public int Source;
        public int Target;
        public int ReadyTurn;
        public int Cohort;
    }
    private readonly List<Passage> passages = new List<Passage>();
    public IReadOnlyList<Passage> Passages => passages;
    public int Count => passages.Count;
    public bool Remove(T enemy)
    {
        int removed = passages.RemoveAll(passage => EqualityComparer<T>.Default.Equals(passage.Enemy, enemy));
        return removed > 0;
    }
    public void Enqueue(T enemy, int source, int target, int turn, int delayTurns,
        int cohort = 0)
    {
        passages.Add(new Passage { Enemy = enemy, Source = source, Target = target,
            ReadyTurn = turn + Math.Max(0, delayTurns), Cohort = cohort });
    }
    public void DeliverGroups(int turn, Func<IReadOnlyList<Passage>, bool> tryArrive)
    {
        // A physical breach can stage residents of another room while delivery
        // is already running. Repeat from a fresh snapshot so all groups that
        // currently fit arrive in this phase; crowded passages remain queued.
        for (int pass = 0; pass < 64; pass++)
        {
            var ready = new List<Passage>();
            for (int i = 0; i < passages.Count; i++)
                if (passages[i].ReadyTurn <= turn) ready.Add(passages[i]);
            if (ready.Count == 0) return;

            bool deliveredAny = false;
            var handled = new HashSet<Passage>();
            var blockedTargets = new HashSet<int>();
            for (int i = 0; i < ready.Count; i++)
            {
                Passage first = ready[i];
                if (!handled.Add(first)) continue;
                var group = new List<Passage> { first };
                for (int j = i + 1; j < ready.Count; j++)
                {
                    Passage candidate = ready[j];
                    if (candidate.Source != first.Source || candidate.Target != first.Target
                        || candidate.ReadyTurn != first.ReadyTurn
                        || candidate.Cohort != first.Cohort) continue;
                    handled.Add(candidate);
                    group.Add(candidate);
                }
                // A later cohort may not overtake an earlier cohort waiting for
                // space in the same destination room.
                if (blockedTargets.Contains(first.Target)) continue;
                if (!tryArrive(group))
                {
                    blockedTargets.Add(first.Target);
                    continue;
                }
                for (int j = 0; j < group.Count; j++) passages.Remove(group[j]);
                deliveredAny = true;
            }
            if (!deliveredAny) return;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        checks++;
    }
    private static void Main(string[] args)
    {
        if (args.Contains("--line-rewards"))
        {
            LineRewards();
            Console.WriteLine("PASS: " + checks + " line-reward checks.");
            return;
        }
        Graph();
        ActiveRoomLayout();
        Income();
        LineRewards();
        Pressure();
        SecondLineRetreat();
        CompactNumbers();
        Console.WriteLine("PASS: " + checks + " defense graph, income and pressure checks.");
    }
    private static void LineRewards()
    {
        var front = new DefenseFront();
        Check(!front.ClaimLineClear(1) && !front.ClaimLineClear(0), "Locked and invalid lines grant no reward");
        front.OpenLine(1);
        front.Observe(0, 0, 1);
        Check(!front.ClaimLineClear(1), "A line with a pending wave grants no reward");
        front.Observe(0, 0, 0);
        Check(front.ClaimLineClear(1) && !front.ClaimLineClear(1), "A completed first line rewards exactly once");
        front.OpenLine(2);
        for (int id = 1; id <= 3; id++) front.Observe(id, 1, 0);
        front.Observe(1, 0, 0);
        Check(!front.ClaimLineClear(2), "Clearing one of three rooms grants no line reward");
        front.Observe(2, 0, 0);
        front.Observe(3, 0, 1);
        Check(!front.ClaimLineClear(2), "The last room's pending wave blocks the reward");
        front.Observe(3, 0, 0);
        Check(front.ClaimLineClear(2), "Completing all three rooms grants the second-line reward");
        front.Observe(1, 1, 0); front.Observe(1, 0, 0);
        Check(!front.ClaimLineClear(2), "Reoccupation cannot farm a second line reward");

        var limited = new DefenseFront();
        limited.ConfigureActiveRooms(new[] { 0, 1 }); limited.OpenLine(1); limited.OpenLine(2);
        limited.Observe(1, 0, 0);
        Check(limited.ClaimLineClear(2), "Disabled rooms do not prevent a line reward");
        limited.OpenLine(3);
        Check(!limited.ClaimLineClear(3), "A line with no active rooms grants no reward");

        var lost = new DefenseFront(); lost.OpenLine(1); lost.OpenLine(2);
        for (int id = 0; id <= 3; id++) lost.Observe(id, 0, 0);
        lost.LoseRoom(1);
        Check(!lost.ClaimLineClear(2), "Abandoning a line does not count as clearing it");
        lost.LiberateLostRoomsThroughLine(2);
        Check(lost.ClaimLineClear(2), "A genuinely liberated line can claim its reward");
    }
    private static void ActiveRoomLayout()
    {
        var front = new DefenseFront();
        var active = front.Rooms.Where(room => room.Line < 4)
            .Select(room => room.Id).ToList();
        active.Add(DefenseFront.IdFor(4, 1));
        active.Add(DefenseFront.IdFor(4, 7));
        front.ConfigureActiveRooms(active);

        for (int line = 1; line <= 4; line++) front.OpenLine(line);
        Check(front.Rooms.Where(room => room.Line < 4).All(room => room.IsActive && room.CanSelect),
            "The first three lines remain active on every location level");
        Check(front.Rooms.Where(room => room.Line == 4 && (room.Index == 1 || room.Index == 7))
            .All(room => room.IsActive && room.CanSelect),
            "Configured fourth-line branches open normally");
        Check(front.Rooms.Where(room => room.Line == 4 && room.Index != 1 && room.Index != 7)
            .All(room => !room.IsActive && !room.CanSelect && room.State == DefenseFront.RoomState.Disabled),
            "Fourth-line branches outside the level stay disabled");

        foreach (var room in front.Rooms.Where(room => room.IsActive)) front.Observe(room.Id, 0, 0);
        Check(front.IsQuiet(0), "Disabled rooms do not block level victory");
        Check(front.Income(1) == active.Count, "Disabled rooms grant neither clear income nor phantom rewards");
    }
    private static void CompactNumbers()
    {
        Check(CompactNumberFormatter.Format(0) == "0", "Zero is displayed directly");
        Check(CompactNumberFormatter.Format(9) == "9", "One-digit health is displayed directly");
        Check(CompactNumberFormatter.Format(999) == "999", "Three-digit health is displayed directly");
        Check(CompactNumberFormatter.Format(999.6) == "1K", "Rounding never produces an unshortened four-digit value");
        Check(CompactNumberFormatter.Format(0.2) == "1", "A living fractional health value never displays zero");
        Check(CompactNumberFormatter.Format(1000) == "1K", "Thousands use K");
        Check(CompactNumberFormatter.Format(1250) == "1.3K", "Thousands keep one useful decimal");
        Check(CompactNumberFormatter.Format(12500) == "12.5K", "Tens of thousands stay readable");
        Check(CompactNumberFormatter.Format(125000) == "125K", "Hundreds of thousands omit decimals");
        Check(CompactNumberFormatter.Format(999999) == "1M", "Rounded 1000K promotes to M");
        Check(CompactNumberFormatter.Format(1200000) == "1.2M", "Millions use M");
        Check(CompactNumberFormatter.Format(3400000000d) == "3.4B", "Billions use B");
        Check(CompactNumberFormatter.Format(7800000000000d) == "7.8T", "Trillions use T");
    }
    private static void Graph()
    {
        var front = new DefenseFront();
        Check(front.Rooms.Count == 16, "Exactly 16 rooms");
        Check(front.Rooms.All(r => !r.CanSelect), "Unopened rooms cannot be selected");
        for (int line = 1; line <= 4; line++)
        {
            Check(front.Rooms.Count(r => r.Line == line) == 2 * line - 1, "1/3/5/7 structure");
            front.OpenLine(line);
            foreach (var room in front.Rooms.Where(r => r.Line == line))
            {
                Check(room.CanSelect, "Opening a line enables every room on it");
                if (room.ParentId >= 0)
                {
                    var parent = front.GetRoom(room.ParentId);
                    Check(parent.Line == line - 1, "Every edge descends exactly one line");
                    Check(Math.Abs((room.Index - line) - (parent.Index - parent.Line)) <= 1, "Adjacent parent");
                }
                front.Observe(room.Id, 0, 0);
            }
        }
        Check(front.GetRoom(0).ParentId == -1, "Main room is the only terminal");
        Check(front.Rooms.Skip(1).Count(r => r.ParentId == -1) == 0, "No detached branch");
        Check(front.IsQuiet(0), "Full clear resolves level");
        Check(!front.IsQuiet(1), "An enemy travelling prevents victory");
        front.Observe(0, 1, 0);
        Check(!front.IsQuiet(0), "Lower-line survivor prevents premature victory");
        front.Observe(0, 0, 1);
        Check(!front.IsQuiet(0), "Remaining wave prevents victory");
        front.LoseRoom(15);
        front.Observe(15, 0, 1);
        Check(!front.IsQuiet(0), "Waves in lost rooms must still be resolved");
        Check(!front.GetRoom(15).CanSelect, "Lost rooms cannot be selected");
        Check(!front.LoseRoom(15), "Repeated breach is idempotent");
    }
    private static void Income()
    {
        var front = new DefenseFront();
        front.OpenLine(1);
        front.Observe(0, 0, 1);
        Check(front.Income(1) == 0, "Empty room with pending waves earns nothing");
        front.Observe(0, 0, 0);
        Check(front.ClaimFirstClear(0), "First clear grants XP");
        Check(front.CompleteTurn(1, 1) == 1, "Income paid on completed turn");
        Check(front.CompleteTurn(1, 1) == 0, "No duplicate turn payment");
        Check(front.CompleteTurn(0, 1) == 0, "No old turn payment");
        front.OpenLine(2);
        for (int id = 1; id <= 3; id++) front.Observe(id, 2, 0);
        front.Observe(1, 0, 0);
        Check(front.CompleteTurn(2, 1) == 2, "Early clear adds income while other rooms fight");
        front.Observe(0, 1, 0);
        Check(front.CompleteTurn(3, 1) == 1, "Reoccupation stops income immediately");
        front.Observe(0, 0, 0);
        Check(!front.ClaimFirstClear(0), "Recapture cannot farm room XP");
        Check(front.CompleteTurn(4, 1) == 2, "Clearing invaders restores income");
        front.LoseRoom(1);
        front.Observe(1, 0, 0);
        Check(front.CompleteTurn(5, 1) == 1, "Lost room never earns income");
        Check(!front.ClaimFirstClear(1), "Loss is not liberation");
        Check(front.EarnedGold == 7, "Earned gold matches exact turn sum");
        Check(front.CompleteTurn(6, -5) == 0, "Negative configured income cannot deduct gold");
    }
    private static void SecondLineRetreat()
    {
        for (int breached = 1; breached <= 3; breached++)
        {
            var front = new DefenseFront();
            front.OpenLine(1);
            front.Observe(0, 0, 0);
            front.OpenLine(2);
            for (int id = 1; id <= 3; id++) front.Observe(id, id == breached ? 3 : 0, 0);
            Check(front.LoseRoom(breached), "Any second-line direction can trigger retreat");
            Check(front.Rooms.Where(r => r.Line == 2).All(r => r.State == DefenseFront.RoomState.Lost && !r.CanSelect),
                "Entire second line closes, including previously secured siblings");
            Check(front.GetRoom(0).CanSelect, "Main room remains accessible for fallback defense");
            Check(front.Income(1) == 1, "Abandoned secured siblings no longer earn gold");
            Check(!front.LoseRoom(breached % 3 + 1), "Sibling breach cannot transfer troops twice");
            front.Observe(breached, 0, 1);
            Check(!front.IsQuiet(0), "Pending waves from abandoned line prevent advancement");
            front.Observe(breached, 0, 0);
            Check(!front.IsQuiet(3), "Enemies in transit prevent advancement even when main room is empty");
            front.Observe(0, 3, 0);
            Check(!front.IsQuiet(0), "Invaders must be defeated in main room before advancement");
            front.Observe(0, 0, 0);
            Check(front.IsQuiet(0), "Defeating all second-line invaders permits advancement");
            Check(front.LiberateLostRoomsThroughLine(2) == 3, "Clearing the line liberates all three abandoned rooms");
            Check(front.Rooms.Where(r => r.Line == 2).All(r => r.State == DefenseFront.RoomState.Secured && r.CanSelect),
                "Liberated second-line rooms become accessible again");
            Check(front.Income(1) == 4, "Liberated line resumes gold income together with main room");
            front.OpenLine(3);
            Check(front.Rooms.Where(r => r.Line == 3).All(r => r.CanSelect), "All five third-line rooms open next");
            Check(front.Rooms.Where(r => r.Line == 2).All(r => r.CanSelect), "Second line remains accessible after advancement");
        }
    }
    private static void Pressure()
    {
        var queue = new DefensePressureQueue<int>();
        var arrived = new List<int>();
        for (int i = 0; i < 5; i++) queue.Enqueue(i, 1, 0, 3, 2);
        var groupSizes = new List<int>();
        Func<IReadOnlyList<DefensePressureQueue<int>.Passage>, bool> accept = group => {
            groupSizes.Add(group.Count);
            arrived.AddRange(group.Select(p => p.Enemy));
            return true;
        };
        queue.DeliverGroups(3, accept);
        queue.DeliverGroups(4, accept);
        Check(arrived.Count == 0, "No instant arrival");
        queue.DeliverGroups(5, group => false);
        Check(arrived.Count == 0 && queue.Count == 5, "Blocked entry retains the whole cohort");
        queue.DeliverGroups(6, accept);
        Check(arrived.SequenceEqual(new[] {0, 1, 2, 3, 4}) && queue.Count == 0,
            "Every enemy from a breach arrives together and exactly once");
        Check(groupSizes.SequenceEqual(new[] {5}), "A five-enemy breach is delivered as one group");
        queue.DeliverGroups(6, accept);
        Check(arrived.Count == 5, "Reprocessing a turn cannot deliver a group twice");

        queue.Enqueue(100, 4, 1, 0, 2);
        queue.Enqueue(101, 5, 1, 0, 2);
        int before = arrived.Count;
        queue.DeliverGroups(9, accept);
        Check(arrived.Count == before + 2 && groupSizes.Skip(1).SequenceEqual(new[] {1, 1}),
            "Different branches remain separate pressure groups");

        queue.Enqueue(200, 15, 8, 10, 2);
        queue.Enqueue(201, 15, 8, 10, 2);
        queue.DeliverGroups(12, group => {
            foreach (var passage in group)
                queue.Enqueue(passage.Enemy, passage.Target, 3, 12, 2);
            return true;
        });
        Check(queue.Count == 2 && queue.Passages.All(p => p.Target == 3 && p.ReadyTurn == 14),
            "Crossing an already lost parent costs another full travel period");
        before = arrived.Count;
        queue.DeliverGroups(13, accept);
        Check(arrived.Count == before, "Cannot traverse several lines in one turn");
        queue.DeliverGroups(14, accept);
        Check(arrived.TakeLast(2).SequenceEqual(new[] {200, 201}) && queue.Count == 0,
            "The same complete cohort survives multi-line transit");

        queue.Enqueue(300, 1, 0, 20, 2);
        queue.Enqueue(301, 1, 0, 21, 2);
        int groupsBefore = groupSizes.Count;
        queue.DeliverGroups(23, accept);
        Check(groupSizes.Count == groupsBefore + 1 && groupSizes.Last() == 2,
            "All ready cohorts on one passage merge instead of bypassing each other");
    }
}

using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public enum DemoInputRejection { None, NoStepSelected, EmptyOrigin, UnknownTarget, HeroAlreadyExited, BlockedDestination, StaleStepChoice, MovementAnimation }

    [Serializable]
    public sealed class DemoLearningErrorCount { public DemoInputRejection reason; public int count; }

    [Serializable]
    public sealed class DemoLearningState
    {
        public string levelId, attemptId, pendingHintId;
        public int boardRevision;
        public int moves, unaidedMoves;
        public bool sawBlocked;
        public bool guidanceHidden, introSeen;
        public List<string> seenEventIds = new();
        public List<string> masteredSkills = new();
        public List<string> errorKeys = new();
        public List<DemoLearningErrorCount> errorCounts = new();
    }

    public sealed class DemoLearningContext
    {
        public int tish = -1, luma = -1, bum = -1, exited, active, selectedStep, remaining, hazard = -1;
        public int from = -1, step, to = -1, legalMoves, moves;
        public string hero, topic;
        public bool shared, anyExit, blocked, hazardYielded;
        public DemoInputRejection rejection;
    }

    // Authored conditions are deliberately typed here; descriptive JSON is not an executable DSL.
    public sealed class DemoLearningPolicy
    {
        public DemoLearningState State { get; }
        public int Moves { get => State.moves; private set => State.moves = value; }
        public int UnassistedMoves { get => State.unaidedMoves; private set => State.unaidedMoves = value; }
        private readonly Dictionary<DemoInputRejection, int> errorCounts = new();
        private readonly HashSet<string> errorKeys = new();

        public DemoLearningPolicy(DemoLearningState state) : this(state, false) { }
        public DemoLearningPolicy(DemoLearningState state, bool resume)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            State.seenEventIds ??= new(); State.masteredSkills ??= new();
            State.errorKeys ??= new(); State.errorCounts ??= new();
            if (!resume || string.IsNullOrEmpty(State.attemptId)) Restart();
            else
            {
                foreach (string key in State.errorKeys) errorKeys.Add(key);
                foreach (var count in State.errorCounts) errorCounts[count.reason] = count.count;
            }
        }
        public void Restart()
        {
            State.attemptId = Guid.NewGuid().ToString("N"); State.boardRevision = 0; State.pendingHintId = null;
            Moves = UnassistedMoves = 0; errorCounts.Clear(); errorKeys.Clear();
            State.sawBlocked = false; State.errorCounts.Clear(); State.errorKeys.Clear();
        }
        public void Help() => UnassistedMoves = 0;
        public bool ObserveMove(bool hasMove, bool positionChanged, string hero, int from, int step, bool exited, bool jumped)
        {
            if (!hasMove || !positionChanged) return false;
            Moves++; UnassistedMoves++; State.boardRevision++; State.pendingHintId = null;
            Master("choose_origin"); Master("step_value");
            if (step == 2) Master("choose_distance");
            if (hero == "luma" || hero == "bum") Master("choose_friend");
            if (exited) Master("individual_exit");
            if (jumped) Master("free_landing");
            return true;
        }
        private void Master(string skill) { if (!State.masteredSkills.Contains(skill)) State.masteredSkills.Add(skill); }
        public bool CanNarrateError(DemoInputRejection reason, string stepInstance, string hero)
        {
            string key = State.boardRevision + ":" + reason + ":" + stepInstance + ":" + hero;
            if (!errorKeys.Add(key)) return false;
            State.errorKeys.Add(key);
            errorCounts.TryGetValue(reason, out int count);
            if (count >= 2) return false;
            errorCounts[reason] = count + 1;
            State.errorCounts.RemoveAll(e => e.reason == reason);
            State.errorCounts.Add(new DemoLearningErrorCount { reason = reason, count = count + 1 });
            UnassistedMoves = 0; return true;
        }
        public bool ShowOnce(string id, bool instruction)
        {
            if (State.seenEventIds.Contains(id) || (instruction && (State.guidanceHidden || UnassistedMoves >= 3))) return false;
            State.seenEventIds.Add(id); State.pendingHintId = id; return true;
        }
        public void Skip(bool all) { State.pendingHintId = null; if (all) State.guidanceHidden = true; }

        public static bool Eligible(int level, int number, DemoLearningContext c, bool sawBlock)
        {
            switch (level)
            {
                case 1: return number switch { 1 => c.moves == 0, 2 => c.selectedStep == 1 && c.tish == 0,
                    3 => c.moves == 1 && c.tish == 1, 4 => c.moves == 1 && c.legalMoves > 0,
                    5 => c.rejection == DemoInputRejection.EmptyOrigin, 8 => c.tish >= 4 && c.moves >= 3,
                    10 => c.tish == 7, 11 => c.exited == 1, _ => true };
                case 2: return number switch { 1 => c.moves == 0, 2 => c.tish == 0,
                    3 => c.from == 0 && c.step == 1, 4 => c.tish == 1,
                    5 => c.tish == 1 && c.selectedStep == 2, 6 => c.from == 1 && c.step == 2 && c.to == 3,
                    7 => c.moves > 0, 8 => c.remaining == 2, 9 => c.tish >= 6 && c.anyExit,
                    10 => c.rejection == DemoInputRejection.StaleStepChoice, 11 => c.tish == 7, _ => true };
                case 3: return number switch { 2 => c.tish == 0, 3 => c.tish == 2 && c.luma == 0,
                    4 => c.shared, 6 => c.active == 2, 7 => c.exited == 1, 8 => c.active == 2 && c.anyExit,
                    9 => c.rejection == DemoInputRejection.HeroAlreadyExited, 10 => c.active > 0, 11 => c.shared, _ => true };
                case 4: return number switch { 2 => c.active == 3, 3 => c.tish == 0,
                    4 => c.blocked && c.hero == "tish" && c.tish == 2 && c.selectedStep == 2,
                    5 => c.tish == 2 && c.selectedStep == 1 && sawBlock, 6 => c.tish == 3,
                    7 => c.bum >= 0, 8 => c.active >= 2, 9 => c.rejection == DemoInputRejection.BlockedDestination,
                    10 => c.exited == 1, 11 => c.anyExit, _ => true };
                case 5: return number switch { 1 => c.moves == 0, 2 => c.tish == 0 && c.remaining == 2,
                    3 => c.tish == 2 && c.remaining == 1 && c.luma == 0, 4 => c.remaining == 1,
                    5 => c.hazard == 4, 6 => c.blocked && c.tish == 2 && c.selectedStep == 2 && c.hazard == 4,
                    7 => c.tish == 2 && c.selectedStep == 1 && c.hazard == 4 && sawBlock,
                    8 => c.tish == 3 && c.luma == 1 && c.remaining == 1, 9 => c.remaining == 1,
                    10 => c.hazard >= 0, 11 => c.hazardYielded, _ => true };
                case 6: return number switch { 3 => c.rejection == DemoInputRejection.BlockedDestination,
                    5 => c.remaining >= 1, 7 => c.hero == "bum" && c.exited == 1, 8 => c.active == 1,
                    9 => c.anyExit, 10 => c.moves >= 3, 11 => c.exited == 3, _ => true };
                default: return false;
            }
        }
        public static bool IsAutomaticInstruction(int level, int number) => level switch
        {
            1 => number == 1 || number == 2 || number == 4 || number == 10,
            2 => number == 1 || number == 4 || number == 5 || number == 11,
            3 => number == 1 || number == 3 || number == 7,
            4 => number == 1 || number == 4 || number == 5 || number == 6,
            5 => number == 1 || number == 3 || number == 6 || number == 7 || number == 8,
            6 => number == 1,
            _ => false
        };
    }
}

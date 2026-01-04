using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using MLBShowdown.Core;

namespace MLBShowdown.BaseRunning
{
    public struct RunnerState : INetworkStruct
    {
        public int BatterIndex;
        public Base CurrentBase;
        public int Speed;
    }

    public class BaseRunnerController : NetworkBehaviour
    {
        [Networked, Capacity(3)]
        public NetworkArray<RunnerState> Runners => default;

        [Networked] public int RunnersOnBase { get; set; }

        public event Action<int> OnRunScored;
        public event Action<Base, int> OnRunnerAdvanced;
        public event Action OnBasesCleared;

        public override void Spawned()
        {
            ClearBases();
        }

        public void ClearBases()
        {
            for (int i = 0; i < 3; i++)
            {
                var runner = Runners.Get(i);
                runner.CurrentBase = Base.None;
                runner.BatterIndex = -1;
                runner.Speed = 0;
                Runners.Set(i, runner);
            }
            RunnersOnBase = 0;
        }

        public bool HasRunnerOn(Base targetBase)
        {
            for (int i = 0; i < 3; i++)
            {
                if (Runners.Get(i).CurrentBase == targetBase)
                    return true;
            }
            return false;
        }

        public RunnerState? GetRunnerOn(Base targetBase)
        {
            for (int i = 0; i < 3; i++)
            {
                var runner = Runners.Get(i);
                if (runner.CurrentBase == targetBase)
                    return runner;
            }
            return null;
        }

        public void PlaceRunner(int batterIndex, Base targetBase, int speed)
        {
            // Find empty slot
            for (int i = 0; i < 3; i++)
            {
                var runner = Runners.Get(i);
                if (runner.CurrentBase == Base.None)
                {
                    runner.BatterIndex = batterIndex;
                    runner.CurrentBase = targetBase;
                    runner.Speed = speed;
                    Runners.Set(i, runner);
                    RunnersOnBase++;
                    OnRunnerAdvanced?.Invoke(targetBase, batterIndex);
                    return;
                }
            }
        }

        public int ProcessWalk(int newBatterIndex, int batterSpeed)
        {
            int runsScored = 0;

            // Walk forces runners - only advance if forced
            if (HasRunnerOn(Base.First))
            {
                if (HasRunnerOn(Base.Second))
                {
                    if (HasRunnerOn(Base.Third))
                    {
                        // Bases loaded - runner on third scores
                        runsScored += ScoreRunner(Base.Third);
                    }
                    // Move second to third
                    MoveRunner(Base.Second, Base.Third);
                }
                // Move first to second
                MoveRunner(Base.First, Base.Second);
            }

            // Place batter on first
            PlaceRunner(newBatterIndex, Base.First, batterSpeed);

            return runsScored;
        }

        public int ProcessSingle(int newBatterIndex, int batterSpeed)
        {
            int runsScored = 0;

            // All runners advance 1 base
            if (HasRunnerOn(Base.Third))
            {
                runsScored += ScoreRunner(Base.Third);
            }
            if (HasRunnerOn(Base.Second))
            {
                MoveRunner(Base.Second, Base.Third);
            }
            if (HasRunnerOn(Base.First))
            {
                MoveRunner(Base.First, Base.Second);
            }

            PlaceRunner(newBatterIndex, Base.First, batterSpeed);

            return runsScored;
        }

        public int ProcessDouble(int newBatterIndex, int batterSpeed)
        {
            int runsScored = 0;

            // All runners advance 2 bases
            if (HasRunnerOn(Base.Third))
            {
                runsScored += ScoreRunner(Base.Third);
            }
            if (HasRunnerOn(Base.Second))
            {
                runsScored += ScoreRunner(Base.Second);
            }
            if (HasRunnerOn(Base.First))
            {
                MoveRunner(Base.First, Base.Third);
            }

            PlaceRunner(newBatterIndex, Base.Second, batterSpeed);

            return runsScored;
        }

        public int ProcessTriple(int newBatterIndex, int batterSpeed)
        {
            int runsScored = 0;

            // All runners score
            if (HasRunnerOn(Base.Third))
            {
                runsScored += ScoreRunner(Base.Third);
            }
            if (HasRunnerOn(Base.Second))
            {
                runsScored += ScoreRunner(Base.Second);
            }
            if (HasRunnerOn(Base.First))
            {
                runsScored += ScoreRunner(Base.First);
            }

            PlaceRunner(newBatterIndex, Base.Third, batterSpeed);

            return runsScored;
        }

        public int ProcessHomeRun(int newBatterIndex)
        {
            int runsScored = 1; // Batter scores

            // All runners score
            if (HasRunnerOn(Base.Third))
            {
                runsScored += ScoreRunner(Base.Third);
            }
            if (HasRunnerOn(Base.Second))
            {
                runsScored += ScoreRunner(Base.Second);
            }
            if (HasRunnerOn(Base.First))
            {
                runsScored += ScoreRunner(Base.First);
            }

            OnRunScored?.Invoke(runsScored);
            return runsScored;
        }

        private int ScoreRunner(Base fromBase)
        {
            for (int i = 0; i < 3; i++)
            {
                var runner = Runners.Get(i);
                if (runner.CurrentBase == fromBase)
                {
                    runner.CurrentBase = Base.None;
                    Runners.Set(i, runner);
                    RunnersOnBase--;
                    OnRunScored?.Invoke(1);
                    return 1;
                }
            }
            return 0;
        }

        private void MoveRunner(Base fromBase, Base toBase)
        {
            for (int i = 0; i < 3; i++)
            {
                var runner = Runners.Get(i);
                if (runner.CurrentBase == fromBase)
                {
                    runner.CurrentBase = toBase;
                    Runners.Set(i, runner);
                    OnRunnerAdvanced?.Invoke(toBase, runner.BatterIndex);
                    return;
                }
            }
        }

        // Optional action: Stolen base attempt
        public bool AttemptSteal(Base fromBase, int catcherDefense, int rollResult)
        {
            var runner = GetRunnerOn(fromBase);
            if (runner == null) return false;

            // Speed + roll vs catcher defense + 10
            int runnerTotal = runner.Value.Speed + rollResult;
            int defenseTotal = catcherDefense + 10;

            if (runnerTotal > defenseTotal)
            {
                // Success - advance runner
                Base toBase = (Base)((int)fromBase + 1);
                if (toBase == Base.Home)
                {
                    ScoreRunner(fromBase);
                }
                else
                {
                    MoveRunner(fromBase, toBase);
                }
                return true;
            }
            else
            {
                // Caught stealing - runner is out
                RemoveRunner(fromBase);
                return false;
            }
        }

        // Optional action: Tag up (sac fly)
        public bool AttemptTagUp(Base fromBase, int outfieldDefense, int rollResult)
        {
            var runner = GetRunnerOn(fromBase);
            if (runner == null) return false;

            // Speed + roll vs outfield defense + 10
            int runnerTotal = runner.Value.Speed + rollResult;
            int defenseTotal = outfieldDefense + 10;

            if (runnerTotal > defenseTotal)
            {
                // Success - advance runner
                Base toBase = (Base)((int)fromBase + 1);
                if (toBase == Base.Home)
                {
                    ScoreRunner(fromBase);
                }
                else
                {
                    MoveRunner(fromBase, toBase);
                }
                return true;
            }
            // Failed tag up - runner stays (already out from fly ball)
            return false;
        }

        // Optional action: Double play attempt
        public bool AttemptDoublePlay(int infieldDefense, int rollResult)
        {
            // Need runner on first for double play
            var runner = GetRunnerOn(Base.First);
            if (runner == null) return false;

            // Infield defense + roll vs runner speed + 10
            int defenseTotal = infieldDefense + rollResult;
            int runnerTotal = runner.Value.Speed + 10;

            if (defenseTotal > runnerTotal)
            {
                // Double play successful - remove runner
                RemoveRunner(Base.First);
                return true;
            }
            // Failed - runner safe, only one out recorded
            return false;
        }

        private void RemoveRunner(Base fromBase)
        {
            for (int i = 0; i < 3; i++)
            {
                var runner = Runners.Get(i);
                if (runner.CurrentBase == fromBase)
                {
                    runner.CurrentBase = Base.None;
                    runner.BatterIndex = -1;
                    runner.Speed = 0;
                    Runners.Set(i, runner);
                    RunnersOnBase--;
                    return;
                }
            }
        }

        public string GetBaseRunnersDisplay()
        {
            string display = "";
            if (HasRunnerOn(Base.First)) display += "1B ";
            if (HasRunnerOn(Base.Second)) display += "2B ";
            if (HasRunnerOn(Base.Third)) display += "3B ";
            return string.IsNullOrEmpty(display) ? "Bases Empty" : display.Trim();
        }

        public bool AreBasesLoaded()
        {
            return HasRunnerOn(Base.First) && HasRunnerOn(Base.Second) && HasRunnerOn(Base.Third);
        }

        public int GetRunnersInScoringPosition()
        {
            int count = 0;
            if (HasRunnerOn(Base.Second)) count++;
            if (HasRunnerOn(Base.Third)) count++;
            return count;
        }
    }
}

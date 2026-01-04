using System;
using System.Collections.Generic;
using UnityEngine;
using MLBShowdown.Cards;

namespace MLBShowdown.Core
{
    [Serializable]
    public class TeamStatistics
    {
        public int Runs;
        public int Hits;
        public int Errors;
        public int LeftOnBase;
        public int AtBats;
        public int Walks;
        public int Strikeouts;
        public int HomeRuns;
        public int Doubles;
        public int Triples;
        public int StolenBases;
        public int CaughtStealing;
        public int DoublePlays;
        public int SacrificeFlies;

        public float GetBattingAverage()
        {
            return AtBats > 0 ? (float)Hits / AtBats : 0f;
        }

        public float GetOnBasePercentage()
        {
            int plateAppearances = AtBats + Walks;
            return plateAppearances > 0 ? (float)(Hits + Walks) / plateAppearances : 0f;
        }

        public float GetSluggingPercentage()
        {
            if (AtBats == 0) return 0f;
            int singles = Hits - Doubles - Triples - HomeRuns;
            int totalBases = singles + (Doubles * 2) + (Triples * 3) + (HomeRuns * 4);
            return (float)totalBases / AtBats;
        }

        public void Reset()
        {
            Runs = 0;
            Hits = 0;
            Errors = 0;
            LeftOnBase = 0;
            AtBats = 0;
            Walks = 0;
            Strikeouts = 0;
            HomeRuns = 0;
            Doubles = 0;
            Triples = 0;
            StolenBases = 0;
            CaughtStealing = 0;
            DoublePlays = 0;
            SacrificeFlies = 0;
        }
    }

    [Serializable]
    public class InningScore
    {
        public int HomeRuns;
        public int AwayRuns;
    }

    public class GameStatistics : MonoBehaviour
    {
        public TeamStatistics HomeTeamStats { get; private set; } = new TeamStatistics();
        public TeamStatistics AwayTeamStats { get; private set; } = new TeamStatistics();
        public List<InningScore> InningScores { get; private set; } = new List<InningScore>();

        private int currentInning;
        private bool isTopOfInning;

        public void StartNewGame()
        {
            HomeTeamStats.Reset();
            AwayTeamStats.Reset();
            InningScores.Clear();
            currentInning = 1;
            isTopOfInning = true;
            InningScores.Add(new InningScore());
        }

        public void SetInning(int inning, bool topOfInning)
        {
            currentInning = inning;
            isTopOfInning = topOfInning;

            // Ensure we have enough inning slots
            while (InningScores.Count < inning)
            {
                InningScores.Add(new InningScore());
            }
        }

        public void RecordAtBat(AtBatOutcome outcome, bool isHomeTeam, int rbis = 0)
        {
            TeamStatistics stats = isHomeTeam ? HomeTeamStats : AwayTeamStats;

            switch (outcome)
            {
                case AtBatOutcome.Strikeout:
                    stats.AtBats++;
                    stats.Strikeouts++;
                    break;

                case AtBatOutcome.Groundout:
                case AtBatOutcome.Flyout:
                    stats.AtBats++;
                    break;

                case AtBatOutcome.Walk:
                    stats.Walks++;
                    break;

                case AtBatOutcome.Single:
                    stats.AtBats++;
                    stats.Hits++;
                    break;

                case AtBatOutcome.Double:
                    stats.AtBats++;
                    stats.Hits++;
                    stats.Doubles++;
                    break;

                case AtBatOutcome.Triple:
                    stats.AtBats++;
                    stats.Hits++;
                    stats.Triples++;
                    break;

                case AtBatOutcome.HomeRun:
                    stats.AtBats++;
                    stats.Hits++;
                    stats.HomeRuns++;
                    break;
            }
        }

        public void RecordRuns(int runs, bool isHomeTeam)
        {
            TeamStatistics stats = isHomeTeam ? HomeTeamStats : AwayTeamStats;
            stats.Runs += runs;

            // Update inning score
            if (currentInning > 0 && currentInning <= InningScores.Count)
            {
                if (isHomeTeam)
                    InningScores[currentInning - 1].HomeRuns += runs;
                else
                    InningScores[currentInning - 1].AwayRuns += runs;
            }
        }

        public void RecordStolenBase(bool success, bool isHomeTeam)
        {
            TeamStatistics stats = isHomeTeam ? HomeTeamStats : AwayTeamStats;
            if (success)
                stats.StolenBases++;
            else
                stats.CaughtStealing++;
        }

        public void RecordDoublePlay(bool isHomeTeam)
        {
            TeamStatistics stats = isHomeTeam ? HomeTeamStats : AwayTeamStats;
            stats.DoublePlays++;
        }

        public void RecordSacFly(bool isHomeTeam)
        {
            TeamStatistics stats = isHomeTeam ? HomeTeamStats : AwayTeamStats;
            stats.SacrificeFlies++;
        }

        public void RecordLeftOnBase(int runners, bool isHomeTeam)
        {
            TeamStatistics stats = isHomeTeam ? HomeTeamStats : AwayTeamStats;
            stats.LeftOnBase += runners;
        }

        public string GetBoxScoreHeader()
        {
            string header = "     ";
            for (int i = 1; i <= InningScores.Count; i++)
            {
                header += $" {i} ";
            }
            header += "  R  H  E";
            return header;
        }

        public string GetAwayBoxScore()
        {
            string line = "AWAY ";
            foreach (var inning in InningScores)
            {
                line += $" {inning.AwayRuns} ";
            }
            line += $"  {AwayTeamStats.Runs}  {AwayTeamStats.Hits}  {AwayTeamStats.Errors}";
            return line;
        }

        public string GetHomeBoxScore()
        {
            string line = "HOME ";
            foreach (var inning in InningScores)
            {
                line += $" {inning.HomeRuns} ";
            }
            line += $"  {HomeTeamStats.Runs}  {HomeTeamStats.Hits}  {HomeTeamStats.Errors}";
            return line;
        }

        public string GetFullBoxScore()
        {
            return $"{GetBoxScoreHeader()}\n{GetAwayBoxScore()}\n{GetHomeBoxScore()}";
        }
    }
}

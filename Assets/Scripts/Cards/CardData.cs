using System;
using System.Collections.Generic;
using UnityEngine;
using MLBShowdown.Core;

namespace MLBShowdown.Cards
{
    [Serializable]
    public class OutcomeRange
    {
        public int MinRoll;
        public int MaxRoll;

        public OutcomeRange(int min, int max)
        {
            MinRoll = min;
            MaxRoll = max;
        }

        public bool Contains(int roll) => roll >= MinRoll && roll <= MaxRoll;
    }

    [Serializable]
    public class OutcomeCard
    {
        public OutcomeRange Strikeout;
        public OutcomeRange Groundout;
        public OutcomeRange Flyout;
        public OutcomeRange Walk;
        public OutcomeRange Single;
        public OutcomeRange Double;
        public OutcomeRange Triple;
        public OutcomeRange HomeRun;

        public AtBatOutcome GetOutcome(int roll)
        {
            if (Strikeout != null && Strikeout.Contains(roll)) return AtBatOutcome.Strikeout;
            if (Groundout != null && Groundout.Contains(roll)) return AtBatOutcome.Groundout;
            if (Flyout != null && Flyout.Contains(roll)) return AtBatOutcome.Flyout;
            if (Walk != null && Walk.Contains(roll)) return AtBatOutcome.Walk;
            if (Single != null && Single.Contains(roll)) return AtBatOutcome.Single;
            if (Double != null && Double.Contains(roll)) return AtBatOutcome.Double;
            if (Triple != null && Triple.Contains(roll)) return AtBatOutcome.Triple;
            if (HomeRun != null && HomeRun.Contains(roll)) return AtBatOutcome.HomeRun;
            return AtBatOutcome.Groundout; // Default fallback
        }
    }

    [CreateAssetMenu(fileName = "NewBatterCard", menuName = "MLB Showdown/Batter Card")]
    public class BatterCard : ScriptableObject
    {
        public string PlayerName;
        public int OnBase;          // Target for advantage check (1-20)
        public int Speed;           // Used for steals, tag-ups (1-20)
        public int PositionPlus;    // Defensive bonus (0-5)
        public string Position;     // Position abbreviation (1B, SS, CF, etc.)
        
        public OutcomeCard OutcomeCard;

        // Runtime stats
        [NonSerialized] public int AtBats;
        [NonSerialized] public int Hits;
        [NonSerialized] public int HomeRuns;
        [NonSerialized] public int RBIs;
        [NonSerialized] public int Runs;

        public void ResetStats()
        {
            AtBats = 0;
            Hits = 0;
            HomeRuns = 0;
            RBIs = 0;
            Runs = 0;
        }

        public float GetBattingAverage()
        {
            return AtBats > 0 ? (float)Hits / AtBats : 0f;
        }
    }

    [CreateAssetMenu(fileName = "NewPitcherCard", menuName = "MLB Showdown/Pitcher Card")]
    public class PitcherCard : ScriptableObject
    {
        public string PlayerName;
        public int Control;         // Added to defense roll (1-10)
        public int Innings;         // Stamina/pitch limit (1-9)
        
        public OutcomeCard OutcomeCard;

        // Runtime stats
        [NonSerialized] public float InningsPitched;
        [NonSerialized] public int Strikeouts;
        [NonSerialized] public int EarnedRuns;
        [NonSerialized] public int Hits;
        [NonSerialized] public int Walks;

        public void ResetStats()
        {
            InningsPitched = 0;
            Strikeouts = 0;
            EarnedRuns = 0;
            Hits = 0;
            Walks = 0;
        }

        public float GetERA()
        {
            return InningsPitched > 0 ? (EarnedRuns / InningsPitched) * 9f : 0f;
        }
    }

    // Serializable data classes for network transfer
    [System.Serializable]
    public class BatterCardData
    {
        public string PlayerName;
        public int OnBase;
        public int Speed;
        public int PositionPlus;
        public string Position;
        public OutcomeCard OutcomeCard;

        // Runtime stats
        public int AtBats;
        public int Hits;
        public int HomeRuns;
        public int RBIs;
        public int Runs;

        public BatterCardData() { }

        public BatterCardData(string name, int onBase, int speed, int posPlus, string pos, OutcomeCard outcome)
        {
            PlayerName = name;
            OnBase = onBase;
            Speed = speed;
            PositionPlus = posPlus;
            Position = pos;
            OutcomeCard = outcome;
        }

        public void ResetStats()
        {
            AtBats = 0;
            Hits = 0;
            HomeRuns = 0;
            RBIs = 0;
            Runs = 0;
        }

        public float GetBattingAverage() => AtBats > 0 ? (float)Hits / AtBats : 0f;
    }

    [System.Serializable]
    public class PitcherCardData
    {
        public string PlayerName;
        public int Control;
        public int Innings;
        public OutcomeCard OutcomeCard;

        // Runtime stats
        public float InningsPitched;
        public int Strikeouts;
        public int EarnedRuns;
        public int Hits;
        public int Walks;

        public PitcherCardData() { }

        public PitcherCardData(string name, int control, int innings, OutcomeCard outcome)
        {
            PlayerName = name;
            Control = control;
            Innings = innings;
            OutcomeCard = outcome;
        }

        public void ResetStats()
        {
            InningsPitched = 0;
            Strikeouts = 0;
            EarnedRuns = 0;
            Hits = 0;
            Walks = 0;
        }

        public float GetERA() => InningsPitched > 0 ? (EarnedRuns / InningsPitched) * 9f : 0f;
    }

    public static class CardDatabase
    {
        public static List<BatterCardData> GetSampleBatters()
        {
            return new List<BatterCardData>
            {
                // Power hitters
                new BatterCardData("Mike Trout", 12, 14, 2, "CF", CreatePowerHitterOutcome()),
                new BatterCardData("Aaron Judge", 11, 10, 1, "RF", CreatePowerHitterOutcome()),
                new BatterCardData("Shohei Ohtani", 13, 12, 2, "DH", CreatePowerHitterOutcome()),
                
                // Contact hitters
                new BatterCardData("Mookie Betts", 14, 15, 3, "RF", CreateContactHitterOutcome()),
                new BatterCardData("Freddie Freeman", 15, 11, 2, "1B", CreateContactHitterOutcome()),
                new BatterCardData("Corey Seager", 13, 12, 2, "SS", CreateContactHitterOutcome()),
                
                // Speed players
                new BatterCardData("Trea Turner", 12, 18, 2, "SS", CreateSpeedsterOutcome()),
                new BatterCardData("Ronald Acuna Jr", 13, 17, 2, "CF", CreateSpeedsterOutcome()),
                new BatterCardData("Bobby Witt Jr", 11, 16, 3, "SS", CreateSpeedsterOutcome()),
                
                // Balanced players
                new BatterCardData("Marcus Semien", 11, 13, 3, "2B", CreateBalancedOutcome()),
                new BatterCardData("Bo Bichette", 12, 14, 2, "SS", CreateBalancedOutcome()),
                new BatterCardData("Rafael Devers", 12, 10, 1, "3B", CreateBalancedOutcome()),
                
                // Defensive specialists
                new BatterCardData("Andrelton Simmons", 9, 12, 5, "SS", CreateDefensiveOutcome()),
                new BatterCardData("Kevin Kiermaier", 8, 14, 5, "CF", CreateDefensiveOutcome()),
                new BatterCardData("Nicky Lopez", 8, 13, 4, "2B", CreateDefensiveOutcome()),
                
                // Catchers
                new BatterCardData("JT Realmuto", 11, 12, 4, "C", CreateBalancedOutcome()),
                new BatterCardData("Will Smith", 12, 10, 3, "C", CreateContactHitterOutcome()),
                new BatterCardData("Adley Rutschman", 13, 11, 4, "C", CreateContactHitterOutcome()),
                
                // First basemen
                new BatterCardData("Matt Olson", 11, 9, 2, "1B", CreatePowerHitterOutcome()),
                new BatterCardData("Vladimir Guerrero Jr", 13, 10, 2, "1B", CreatePowerHitterOutcome()),
                new BatterCardData("Pete Alonso", 10, 8, 1, "1B", CreatePowerHitterOutcome()),
            };
        }

        public static List<PitcherCardData> GetSamplePitchers()
        {
            return new List<PitcherCardData>
            {
                // Aces
                new PitcherCardData("Gerrit Cole", 8, 7, CreateAcePitcherOutcome()),
                new PitcherCardData("Max Scherzer", 7, 6, CreateAcePitcherOutcome()),
                new PitcherCardData("Jacob deGrom", 9, 5, CreateAcePitcherOutcome()),
                
                // Control pitchers
                new PitcherCardData("Zack Wheeler", 7, 7, CreateControlPitcherOutcome()),
                new PitcherCardData("Corbin Burnes", 8, 6, CreateControlPitcherOutcome()),
                new PitcherCardData("Spencer Strider", 6, 6, CreateStrikeoutPitcherOutcome()),
                
                // Workhorses
                new PitcherCardData("Framber Valdez", 6, 8, CreateGroundballPitcherOutcome()),
                new PitcherCardData("Logan Webb", 6, 8, CreateGroundballPitcherOutcome()),
                new PitcherCardData("Sandy Alcantara", 5, 9, CreateWorkhorseOutcome()),
                
                // Strikeout artists
                new PitcherCardData("Dylan Cease", 5, 6, CreateStrikeoutPitcherOutcome()),
                new PitcherCardData("Kevin Gausman", 6, 6, CreateStrikeoutPitcherOutcome()),
            };
        }

        private static OutcomeCard CreatePowerHitterOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 5),
                Groundout = new OutcomeRange(6, 8),
                Flyout = new OutcomeRange(9, 11),
                Walk = new OutcomeRange(12, 13),
                Single = new OutcomeRange(14, 16),
                Double = new OutcomeRange(17, 18),
                Triple = new OutcomeRange(19, 19),
                HomeRun = new OutcomeRange(20, 20)
            };
        }

        private static OutcomeCard CreateContactHitterOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 3),
                Groundout = new OutcomeRange(4, 7),
                Flyout = new OutcomeRange(8, 10),
                Walk = new OutcomeRange(11, 13),
                Single = new OutcomeRange(14, 17),
                Double = new OutcomeRange(18, 19),
                Triple = new OutcomeRange(20, 20),
                HomeRun = null
            };
        }

        private static OutcomeCard CreateSpeedsterOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 4),
                Groundout = new OutcomeRange(5, 8),
                Flyout = new OutcomeRange(9, 10),
                Walk = new OutcomeRange(11, 12),
                Single = new OutcomeRange(13, 17),
                Double = new OutcomeRange(18, 19),
                Triple = new OutcomeRange(20, 20),
                HomeRun = null
            };
        }

        private static OutcomeCard CreateBalancedOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 4),
                Groundout = new OutcomeRange(5, 8),
                Flyout = new OutcomeRange(9, 11),
                Walk = new OutcomeRange(12, 13),
                Single = new OutcomeRange(14, 17),
                Double = new OutcomeRange(18, 19),
                Triple = null,
                HomeRun = new OutcomeRange(20, 20)
            };
        }

        private static OutcomeCard CreateDefensiveOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 5),
                Groundout = new OutcomeRange(6, 10),
                Flyout = new OutcomeRange(11, 13),
                Walk = new OutcomeRange(14, 15),
                Single = new OutcomeRange(16, 19),
                Double = new OutcomeRange(20, 20),
                Triple = null,
                HomeRun = null
            };
        }

        private static OutcomeCard CreateAcePitcherOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 8),
                Groundout = new OutcomeRange(9, 12),
                Flyout = new OutcomeRange(13, 15),
                Walk = new OutcomeRange(16, 16),
                Single = new OutcomeRange(17, 19),
                Double = new OutcomeRange(20, 20),
                Triple = null,
                HomeRun = null
            };
        }

        private static OutcomeCard CreateControlPitcherOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 6),
                Groundout = new OutcomeRange(7, 11),
                Flyout = new OutcomeRange(12, 15),
                Walk = new OutcomeRange(16, 16),
                Single = new OutcomeRange(17, 19),
                Double = new OutcomeRange(20, 20),
                Triple = null,
                HomeRun = null
            };
        }

        private static OutcomeCard CreateStrikeoutPitcherOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 9),
                Groundout = new OutcomeRange(10, 12),
                Flyout = new OutcomeRange(13, 14),
                Walk = new OutcomeRange(15, 17),
                Single = new OutcomeRange(18, 19),
                Double = new OutcomeRange(20, 20),
                Triple = null,
                HomeRun = null
            };
        }

        private static OutcomeCard CreateGroundballPitcherOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 5),
                Groundout = new OutcomeRange(6, 13),
                Flyout = new OutcomeRange(14, 15),
                Walk = new OutcomeRange(16, 17),
                Single = new OutcomeRange(18, 19),
                Double = new OutcomeRange(20, 20),
                Triple = null,
                HomeRun = null
            };
        }

        private static OutcomeCard CreateWorkhorseOutcome()
        {
            return new OutcomeCard
            {
                Strikeout = new OutcomeRange(1, 5),
                Groundout = new OutcomeRange(6, 11),
                Flyout = new OutcomeRange(12, 15),
                Walk = new OutcomeRange(16, 17),
                Single = new OutcomeRange(18, 19),
                Double = new OutcomeRange(20, 20),
                Triple = null,
                HomeRun = null
            };
        }
    }
}

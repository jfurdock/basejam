using System;
using UnityEngine;
using MLBShowdown.Cards;

namespace MLBShowdown.Core
{
    public class AtBatController : MonoBehaviour
    {
        public event Action<bool> OnAdvantageDecided;
        public event Action<AtBatOutcome> OnOutcomeDecided;
        public event Action<string> OnAtBatMessage;

        private BatterCardData currentBatter;
        private PitcherCardData currentPitcher;
        private bool batterHasAdvantage;
        private int defenseRoll;
        private int offenseRoll;
        private AtBatOutcome currentOutcome;

        public void SetupAtBat(BatterCardData batter, PitcherCardData pitcher)
        {
            currentBatter = batter;
            currentPitcher = pitcher;
            batterHasAdvantage = false;
            defenseRoll = 0;
            offenseRoll = 0;
            currentOutcome = AtBatOutcome.None;
        }

        public bool ProcessDefenseRoll(int roll)
        {
            if (currentBatter == null || currentPitcher == null)
            {
                Debug.LogError("AtBat not properly set up");
                return false;
            }

            defenseRoll = roll;
            int defenseTotal = roll + currentPitcher.Control;
            batterHasAdvantage = defenseTotal <= currentBatter.OnBase;

            string message = $"{currentPitcher.PlayerName} rolls {roll} + {currentPitcher.Control} Control = {defenseTotal}";
            message += $"\nvs {currentBatter.PlayerName}'s OnBase {currentBatter.OnBase}";
            message += batterHasAdvantage ? "\n→ BATTER has advantage!" : "\n→ PITCHER has advantage!";
            
            OnAtBatMessage?.Invoke(message);
            OnAdvantageDecided?.Invoke(batterHasAdvantage);

            return batterHasAdvantage;
        }

        public AtBatOutcome ProcessOffenseRoll(int roll)
        {
            if (currentBatter == null || currentPitcher == null)
            {
                Debug.LogError("AtBat not properly set up");
                return AtBatOutcome.Groundout;
            }

            offenseRoll = roll;

            // Use appropriate outcome card based on advantage
            OutcomeCard outcomeCard = batterHasAdvantage ? 
                currentBatter.OutcomeCard : 
                currentPitcher.OutcomeCard;

            currentOutcome = outcomeCard.GetOutcome(roll);

            // Update stats
            currentBatter.AtBats++;
            
            switch (currentOutcome)
            {
                case AtBatOutcome.Strikeout:
                    currentPitcher.Strikeouts++;
                    break;
                case AtBatOutcome.Walk:
                    currentPitcher.Walks++;
                    break;
                case AtBatOutcome.Single:
                case AtBatOutcome.Double:
                case AtBatOutcome.Triple:
                    currentBatter.Hits++;
                    currentPitcher.Hits++;
                    break;
                case AtBatOutcome.HomeRun:
                    currentBatter.Hits++;
                    currentBatter.HomeRuns++;
                    currentPitcher.Hits++;
                    break;
            }

            string cardOwner = batterHasAdvantage ? currentBatter.PlayerName : currentPitcher.PlayerName;
            string message = $"{currentBatter.PlayerName} rolls {roll} on {cardOwner}'s card";
            message += $"\n→ {GetOutcomeDisplayText(currentOutcome)}!";
            
            OnAtBatMessage?.Invoke(message);
            OnOutcomeDecided?.Invoke(currentOutcome);

            return currentOutcome;
        }

        public static string GetOutcomeDisplayText(AtBatOutcome outcome)
        {
            return outcome switch
            {
                AtBatOutcome.Strikeout => "STRIKEOUT",
                AtBatOutcome.Groundout => "GROUND OUT",
                AtBatOutcome.Flyout => "FLY OUT",
                AtBatOutcome.Walk => "WALK",
                AtBatOutcome.Single => "SINGLE",
                AtBatOutcome.Double => "DOUBLE",
                AtBatOutcome.Triple => "TRIPLE",
                AtBatOutcome.HomeRun => "HOME RUN!",
                _ => "UNKNOWN"
            };
        }

        public static bool IsOut(AtBatOutcome outcome)
        {
            return outcome == AtBatOutcome.Strikeout || 
                   outcome == AtBatOutcome.Groundout || 
                   outcome == AtBatOutcome.Flyout;
        }

        public static bool IsHit(AtBatOutcome outcome)
        {
            return outcome == AtBatOutcome.Single || 
                   outcome == AtBatOutcome.Double || 
                   outcome == AtBatOutcome.Triple || 
                   outcome == AtBatOutcome.HomeRun;
        }

        public static int GetBasesAdvanced(AtBatOutcome outcome)
        {
            return outcome switch
            {
                AtBatOutcome.Walk => 1,
                AtBatOutcome.Single => 1,
                AtBatOutcome.Double => 2,
                AtBatOutcome.Triple => 3,
                AtBatOutcome.HomeRun => 4,
                _ => 0
            };
        }

        // Getters
        public BatterCardData CurrentBatter => currentBatter;
        public PitcherCardData CurrentPitcher => currentPitcher;
        public bool BatterHasAdvantage => batterHasAdvantage;
        public int DefenseRoll => defenseRoll;
        public int OffenseRoll => offenseRoll;
        public AtBatOutcome CurrentOutcome => currentOutcome;
    }
}

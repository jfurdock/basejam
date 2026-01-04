using System;
using UnityEngine;
using MLBShowdown.BaseRunning;

namespace MLBShowdown.Core
{
    public class OptionalActionController : MonoBehaviour
    {
        public event Action<OptionalActionType> OnOptionalActionAvailable;
        public event Action<bool, string> OnOptionalActionResult;

        private BaseRunnerController baseRunnerController;

        public void Initialize(BaseRunnerController runnerController)
        {
            baseRunnerController = runnerController;
        }

        public OptionalActionType CheckForOptionalAction(AtBatOutcome outcome, int outs)
        {
            if (baseRunnerController == null) return OptionalActionType.None;

            switch (outcome)
            {
                case AtBatOutcome.Strikeout:
                    // Stolen base attempt available if runner on base
                    if (baseRunnerController.HasRunnerOn(Base.First) || 
                        baseRunnerController.HasRunnerOn(Base.Second))
                    {
                        return OptionalActionType.StolenBase;
                    }
                    break;

                case AtBatOutcome.Flyout:
                    // Tag up available if runner on third (or second with less than 2 outs)
                    if (baseRunnerController.HasRunnerOn(Base.Third))
                    {
                        return OptionalActionType.TagUp;
                    }
                    break;

                case AtBatOutcome.Groundout:
                    // Double play available if runner on first and less than 2 outs
                    if (baseRunnerController.HasRunnerOn(Base.First) && outs < 2)
                    {
                        return OptionalActionType.DoublePlay;
                    }
                    break;
            }

            return OptionalActionType.None;
        }

        public bool ExecuteOptionalAction(
            OptionalActionType actionType, 
            int roll, 
            int catcherDefense, 
            int outfieldDefense, 
            int infieldDefense,
            out string resultMessage)
        {
            resultMessage = "";
            
            if (baseRunnerController == null)
            {
                resultMessage = "Error: No base runner controller";
                return false;
            }

            switch (actionType)
            {
                case OptionalActionType.StolenBase:
                    return ExecuteStolenBase(roll, catcherDefense, out resultMessage);

                case OptionalActionType.TagUp:
                    return ExecuteTagUp(roll, outfieldDefense, out resultMessage);

                case OptionalActionType.DoublePlay:
                    return ExecuteDoublePlay(roll, infieldDefense, out resultMessage);

                default:
                    resultMessage = "No action to execute";
                    return false;
            }
        }

        private bool ExecuteStolenBase(int roll, int catcherDefense, out string message)
        {
            // Try to steal from second base first (more valuable), then first
            Base fromBase = baseRunnerController.HasRunnerOn(Base.Second) ? Base.Second : Base.First;
            var runner = baseRunnerController.GetRunnerOn(fromBase);

            if (!runner.HasValue)
            {
                message = "No runner available for steal attempt";
                return false;
            }

            int runnerSpeed = runner.Value.Speed;
            int runnerTotal = runnerSpeed + roll;
            int defenseTotal = catcherDefense + 10;

            bool success = runnerTotal > defenseTotal;

            if (success)
            {
                Base toBase = (Base)((int)fromBase + 1);
                if (toBase == Base.Home)
                {
                    baseRunnerController.AttemptSteal(fromBase, catcherDefense, roll);
                    message = $"STOLEN BASE! Runner scores! (Speed {runnerSpeed} + Roll {roll} = {runnerTotal} > Defense {defenseTotal})";
                }
                else
                {
                    baseRunnerController.AttemptSteal(fromBase, catcherDefense, roll);
                    message = $"STOLEN BASE! Runner advances to {toBase}! (Speed {runnerSpeed} + Roll {roll} = {runnerTotal} > Defense {defenseTotal})";
                }
            }
            else
            {
                baseRunnerController.AttemptSteal(fromBase, catcherDefense, roll);
                message = $"CAUGHT STEALING! Runner is out! (Speed {runnerSpeed} + Roll {roll} = {runnerTotal} ≤ Defense {defenseTotal})";
            }

            OnOptionalActionResult?.Invoke(success, message);
            return success;
        }

        private bool ExecuteTagUp(int roll, int outfieldDefense, out string message)
        {
            var runner = baseRunnerController.GetRunnerOn(Base.Third);

            if (!runner.HasValue)
            {
                message = "No runner on third for tag up";
                return false;
            }

            int runnerSpeed = runner.Value.Speed;
            int runnerTotal = runnerSpeed + roll;
            int defenseTotal = outfieldDefense + 10;

            bool success = runnerTotal > defenseTotal;

            baseRunnerController.AttemptTagUp(Base.Third, outfieldDefense, roll);

            if (success)
            {
                message = $"TAG UP SUCCESSFUL! Runner scores! (Speed {runnerSpeed} + Roll {roll} = {runnerTotal} > Defense {defenseTotal})";
            }
            else
            {
                message = $"Runner held at third! (Speed {runnerSpeed} + Roll {roll} = {runnerTotal} ≤ Defense {defenseTotal})";
            }

            OnOptionalActionResult?.Invoke(success, message);
            return success;
        }

        private bool ExecuteDoublePlay(int roll, int infieldDefense, out string message)
        {
            var runner = baseRunnerController.GetRunnerOn(Base.First);

            if (!runner.HasValue)
            {
                message = "No runner on first for double play";
                return false;
            }

            int runnerSpeed = runner.Value.Speed;
            int defenseTotal = infieldDefense + roll;
            int runnerTotal = runnerSpeed + 10;

            bool success = defenseTotal > runnerTotal;

            baseRunnerController.AttemptDoublePlay(infieldDefense, roll);

            if (success)
            {
                message = $"DOUBLE PLAY! Two outs recorded! (Defense {infieldDefense} + Roll {roll} = {defenseTotal} > Runner {runnerTotal})";
            }
            else
            {
                message = $"Runner safe at second! Only one out. (Defense {infieldDefense} + Roll {roll} = {defenseTotal} ≤ Runner {runnerTotal})";
            }

            OnOptionalActionResult?.Invoke(success, message);
            return success;
        }

        public string GetOptionalActionDescription(OptionalActionType actionType)
        {
            return actionType switch
            {
                OptionalActionType.StolenBase => "Attempt Stolen Base?\nRunner Speed + D20 vs Catcher Defense + 10",
                OptionalActionType.TagUp => "Attempt Tag Up (Sac Fly)?\nRunner Speed + D20 vs Outfield Defense + 10",
                OptionalActionType.DoublePlay => "Attempt Double Play?\nInfield Defense + D20 vs Runner Speed + 10",
                _ => ""
            };
        }

        public bool IsOffensiveAction(OptionalActionType actionType)
        {
            return actionType == OptionalActionType.StolenBase || actionType == OptionalActionType.TagUp;
        }
    }
}

using System;

namespace MLBShowdown.Core
{
    public enum GameState
    {
        WaitingForPlayers,
        RollForTeamAssignment,
        PregameSetup,
        SetLineups,
        StartGame,
        DefenseTurn,
        OffenseTurn,
        AtBatAction,
        OptionalAction,
        UpdateBaseRunners,
        NextBatterUp,
        NewHalfInning,
        EndHalfInning,
        GameOver
    }

    public enum TeamType
    {
        None,
        Home,
        Away
    }

    public enum AtBatOutcome
    {
        None,
        Strikeout,
        Groundout,
        Flyout,
        Walk,
        Single,
        Double,
        Triple,
        HomeRun
    }

    public enum OptionalActionType
    {
        None,
        StolenBase,
        TagUp,
        DoublePlay
    }

    public enum Base
    {
        None = 0,
        First = 1,
        Second = 2,
        Third = 3,
        Home = 4
    }
}

using System;
using System.Collections.Generic;

namespace NW.Core
{
    public enum GameState
    {
        Boot, Title, Hub, MissionSetup,
        BattleIntro, BattleRunning, BattleDraft, BattleOutro,
        Results
    }

    /// <summary>
    /// Top-level flow state machine (doc 07 §4). States own their content loads;
    /// transitions are explicit and validated. Views subscribe to StateChanged.
    /// </summary>
    public sealed class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.Boot;
        public event Action<GameState, GameState> StateChanged; // (from, to)

        private static readonly Dictionary<GameState, GameState[]> Allowed = new()
        {
            [GameState.Boot] = new[] { GameState.Title },
            [GameState.Title] = new[] { GameState.Hub },
            [GameState.Hub] = new[] { GameState.MissionSetup, GameState.Title },
            [GameState.MissionSetup] = new[] { GameState.BattleIntro, GameState.Hub },
            [GameState.BattleIntro] = new[] { GameState.BattleRunning },
            [GameState.BattleRunning] = new[] { GameState.BattleDraft, GameState.BattleOutro },
            [GameState.BattleDraft] = new[] { GameState.BattleRunning },
            [GameState.BattleOutro] = new[] { GameState.Results },
            [GameState.Results] = new[] { GameState.Hub, GameState.MissionSetup },
        };

        public bool CanTransition(GameState to) =>
            Allowed.TryGetValue(Current, out var next) && Array.IndexOf(next, to) >= 0;

        public void TransitionTo(GameState to)
        {
            if (!CanTransition(to))
                throw new InvalidOperationException($"Illegal transition {Current} -> {to}");
            GameState from = Current;
            Current = to;
            StateChanged?.Invoke(from, to);
        }
    }
}

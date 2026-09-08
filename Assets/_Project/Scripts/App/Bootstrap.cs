using UnityEngine;
using NW.Core;
using NW.Core.Save;
using NW.Data;

namespace NW.App
{
    /// <summary>
    /// Composition root. Lives on the only object in the Boot scene; registers
    /// services explicitly (doc 07 §10) and drives the top-level state machine.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private GameDatabase database;

        public static GameStateMachine States { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Services.Clear();

            var save = new SaveService();
            bool existing = save.Load();
            Services.Register(save);
            if (database != null) Services.Register(database);

            States = new GameStateMachine();
            Debug.Log($"[NW] Boot complete. Profile: {(existing ? "loaded" : "fresh")}. " +
                      $"Units in db: {(database != null ? database.units.Count : 0)}");
            States.TransitionTo(GameState.Title);
        }

        private void OnApplicationQuit()
        {
            if (Services.TryGet(out SaveService save)) save.Save();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Events;

namespace RonJames.Foundation.Commands
{
    /// <summary>What a Command Sequence does when asked to Run while a Run is already going.</summary>
    public enum WhenAlreadyRunning
    {
        /// <summary>Queue another Run to start, in order, after the current one (and any already queued) finish.</summary>
        Queue,

        /// <summary>
        /// Stop the current Run, discard Queued Runs, and start a new Run on the frame after the current
        /// one has cleaned up. Several Restart requests in the same frame collapse into one.
        /// </summary>
        Restart
    }

    /// <summary>
    /// Holds a Sequence of Commands and starts Runs of it. The inspector shows the live Run tree of the
    /// current (or most recent) Run.
    /// </summary>
    [AddComponentMenu("RonJames Foundation/Command Sequence")]
    public class CommandSequence : SerializedMonoBehaviour
    {
        [OdinSerialize]
        [ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = false)]
        [PropertyOrder(10)]
        private List<ICommand> commands = new List<ICommand>();

        [SerializeField]
        [Tooltip("Start a Run in Start().")]
        private bool runOnStart = false;

        [SerializeField]
        [Tooltip("Queue: requests while running wait their turn, one Run each. Restart: Stop and start over.")]
        private WhenAlreadyRunning whenAlreadyRunning = WhenAlreadyRunning.Queue;

        [SerializeField]
        [Tooltip("When on, a Failed Command is logged and the Sequence carries on instead of Stopping.")]
        private bool continueOnFailure;

        [SerializeField]
        [FoldoutGroup("Events")]
        private UnityEvent onRunStarted = new UnityEvent();

        [SerializeField]
        [FoldoutGroup("Events")]
        private UnityEvent onRunCompleted = new UnityEvent();

        [SerializeField]
        [FoldoutGroup("Events")]
        private UnityEvent onRunStopped = new UnityEvent();

        [SerializeField]
        [FoldoutGroup("Events")]
        private UnityEvent onRunFailed = new UnityEvent();

        [NonSerialized]
        private int queuedRuns;

        [NonSerialized]
        private bool processing;

        [NonSerialized]
        private int runNumber;

        [NonSerialized]
        private bool restartRequested;

        /// <summary>The top-level Commands.</summary>
        public IReadOnlyList<ICommand> Commands => commands;

        /// <summary>Whether a Run starts in Start().</summary>
        public bool RunOnStart
        {
            get => runOnStart;
            set => runOnStart = value;
        }

        /// <summary>Whether a Failed Command lets the Sequence carry on.</summary>
        public bool ContinueOnFailure
        {
            get => continueOnFailure;
            set => continueOnFailure = value;
        }

        /// <summary>What happens when asked to Run while a Run is going.</summary>
        public WhenAlreadyRunning WhenAlreadyRunning
        {
            get => whenAlreadyRunning;
            set => whenAlreadyRunning = value;
        }

        /// <summary>The Run in progress, or null.</summary>
        public CommandRun CurrentRun { get; private set; }

        /// <summary>The Run in progress, or the most recent one. Kept until the next Run starts.</summary>
        [ShowInInspector]
        [HideLabel]
        [PropertyOrder(0)]
        [ShowIf(nameof(HasRun))]
        public CommandRun LastRun { get; private set; }

        /// <summary>True while a Run is going or queued.</summary>
        public bool IsRunning => processing;

        /// <summary>Runs waiting to start after the current one.</summary>
        public int QueuedRunCount => queuedRuns;

        /// <summary>Raised when each Run starts. For code; designers use the UnityEvents.</summary>
        public event Action<CommandRun> RunStarted;

        /// <summary>Raised when each Run ends, however it ended.</summary>
        public event Action<CommandRun> RunEnded;

        /// <summary>Replaces the Commands (for building Sequences from code). Takes effect from the next Run.</summary>
        public void SetCommands(IEnumerable<ICommand> newCommands)
        {
            commands = newCommands == null ? new List<ICommand>() : new List<ICommand>(newCommands);
        }

        /// <summary>
        /// Starts a Run, or, if one is going, queues one or restarts depending on
        /// <see cref="WhenAlreadyRunning"/>. Safe to wire to UnityEvents.
        /// </summary>
        [Button(ButtonSizes.Medium)]
        [HorizontalGroup("Controls")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        [PropertyOrder(-1)]
        public void Run()
        {
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning($"[RonJames Foundation] '{name}' was asked to Run while disabled; ignored.", this);
                return;
            }

            if (processing && whenAlreadyRunning == WhenAlreadyRunning.Restart)
            {
                // The new Run starts on the next frame, so several Restart requests in one frame
                // collapse into one Restart however quickly the stopped Run cleans up.
                queuedRuns = 1;
                restartRequested = true;
                CurrentRun?.Stop();
                return;
            }

            queuedRuns++;
            if (!processing)
            {
                ProcessQueue();
            }
        }

        /// <summary>Ends the current top-level Command early; the Run continues with the next one.</summary>
        [Button(ButtonSizes.Medium)]
        [HorizontalGroup("Controls")]
        [EnableIf(nameof(IsRunning))]
        [PropertyOrder(-1)]
        public void Skip()
        {
            CurrentRun?.Skip();
        }

        /// <summary>Stops the current Run and discards every Queued Run.</summary>
        [Button(ButtonSizes.Medium)]
        [HorizontalGroup("Controls")]
        [EnableIf(nameof(IsRunning))]
        [PropertyOrder(-1)]
        public void Stop()
        {
            queuedRuns = 0;
            CurrentRun?.Stop();
        }

        /// <summary>Completes once no Run is going or queued.</summary>
        public async Awaitable WaitUntilIdleAsync(CancellationToken token = default)
        {
            while (processing)
            {
                await Awaitable.NextFrameAsync(token);
            }
        }

        /// <summary>Stops, then completes once the current Run has finished its cleanup.</summary>
        public async Awaitable StopAndWaitAsync(CancellationToken token = default)
        {
            Stop();
            await WaitUntilIdleAsync(token);
        }

        /// <summary>Runs (queueing or restarting as configured), then completes once idle.</summary>
        public async Awaitable RunAndWaitAsync(CancellationToken token = default)
        {
            Run();
            await WaitUntilIdleAsync(token);
        }

        /// <summary>Starts a Run if Run on Start is on.</summary>
        protected virtual void Start()
        {
            if (runOnStart)
            {
                Run();
            }
        }

        /// <summary>Disabling Stops the Run, so every Command's cleanup runs.</summary>
        protected virtual void OnDisable()
        {
            Stop();
        }

        private async void ProcessQueue()
        {
            processing = true;
            try
            {
                while (queuedRuns > 0)
                {
                    queuedRuns--;
                    var run = new CommandRun(this, commands, continueOnFailure, ++runNumber);
                    CurrentRun = run;
                    LastRun = run;
                    Notify(onRunStarted, RunStarted, run);

                    await run.Execute();

                    CurrentRun = null;
                    switch (run.Status)
                    {
                        case RunStatus.Completed:
                            Notify(onRunCompleted, RunEnded, run);
                            break;
                        case RunStatus.Stopped:
                            Notify(onRunStopped, RunEnded, run);
                            break;
                        case RunStatus.Failed:
                            Notify(onRunFailed, RunEnded, run);
                            break;
                    }

                    if (restartRequested && queuedRuns > 0)
                    {
                        await Awaitable.NextFrameAsync();
                    }

                    restartRequested = false;
                    if (this == null || !isActiveAndEnabled)
                    {
                        queuedRuns = 0;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                CurrentRun = null;
                processing = false;
            }
        }

        private void Notify(UnityEvent unityEvent, Action<CommandRun> codeEvent, CommandRun run)
        {
            try
            {
                unityEvent?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            try
            {
                codeEvent?.Invoke(run);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private bool HasRun() => LastRun != null;
    }
}

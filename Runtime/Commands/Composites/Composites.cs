using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Events;

namespace RonJames.Foundation.Commands
{
    /// <summary>A Composite that runs its Commands one after another.</summary>
    public class SequenceComposite : Command
    {
        [OdinSerialize]
        [ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = true)]
        private List<ICommand> commands = new List<ICommand>();

        [SerializeField]
        [Tooltip("When on, a Failed Command is logged and this Sequence carries on instead of Stopping.")]
        private bool continueOnFailure;

        /// <summary>Creates an empty Sequence Composite.</summary>
        public SequenceComposite()
        {
        }

        /// <summary>Creates a Sequence Composite from code.</summary>
        public SequenceComposite(params ICommand[] commands)
        {
            this.commands = new List<ICommand>(commands);
        }

        /// <summary>The child Commands.</summary>
        public List<ICommand> Commands => commands;

        /// <summary>Whether a Failed child lets this Sequence carry on.</summary>
        public bool ContinueOnFailure
        {
            get => continueOnFailure;
            set => continueOnFailure = value;
        }

        /// <inheritdoc />
        public override Awaitable Execute(CommandContext context) => context.RunSequence(commands, continueOnFailure);
    }

    /// <summary>A Composite that runs its Commands at the same time and finishes when all have finished.</summary>
    public class ParallelComposite : Command
    {
        [OdinSerialize]
        [ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = true)]
        private List<ICommand> commands = new List<ICommand>();

        /// <summary>Creates an empty Parallel Composite.</summary>
        public ParallelComposite()
        {
        }

        /// <summary>Creates a Parallel Composite from code.</summary>
        public ParallelComposite(params ICommand[] commands)
        {
            this.commands = new List<ICommand>(commands);
        }

        /// <summary>The child Commands.</summary>
        public List<ICommand> Commands => commands;

        /// <inheritdoc />
        public override Awaitable Execute(CommandContext context) => context.RunParallel(commands);
    }

    /// <summary>A Composite that runs its Commands at the same time, finishes with the first, and Skips the rest.</summary>
    public class RaceComposite : Command
    {
        [OdinSerialize]
        [ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = true)]
        private List<ICommand> commands = new List<ICommand>();

        /// <summary>Creates an empty Race Composite.</summary>
        public RaceComposite()
        {
        }

        /// <summary>Creates a Race Composite from code.</summary>
        public RaceComposite(params ICommand[] commands)
        {
            this.commands = new List<ICommand>(commands);
        }

        /// <summary>The child Commands.</summary>
        public List<ICommand> Commands => commands;

        /// <inheritdoc />
        public override Awaitable Execute(CommandContext context) => context.RunRace(commands);
    }

    /// <summary>
    /// A Composite wrapping one Command so authors can respond when it starts, completes, is Skipped,
    /// is Stopped or Fails. Like any Composite, it is the "parent" its child's Skip Parent ends.
    /// </summary>
    public class HooksComposite : Command
    {
        [OdinSerialize]
        [Required]
        private ICommand command;

        [SerializeField]
        [FoldoutGroup("Hooks")]
        private UnityEvent onStarted = new UnityEvent();

        [SerializeField]
        [FoldoutGroup("Hooks")]
        private UnityEvent onCompleted = new UnityEvent();

        [SerializeField]
        [FoldoutGroup("Hooks")]
        private UnityEvent onSkipped = new UnityEvent();

        [SerializeField]
        [FoldoutGroup("Hooks")]
        private UnityEvent onStopped = new UnityEvent();

        [SerializeField]
        [FoldoutGroup("Hooks")]
        private UnityEvent onFailed = new UnityEvent();

        /// <summary>Creates an empty Hooks Composite.</summary>
        public HooksComposite()
        {
        }

        /// <summary>Creates a Hooks Composite from code.</summary>
        public HooksComposite(ICommand command)
        {
            this.command = command;
        }

        /// <summary>The wrapped Command.</summary>
        public ICommand Inner
        {
            get => command;
            set => command = value;
        }

        /// <summary>Hook UnityEvents, exposed for code.</summary>
        public UnityEvent OnStarted => onStarted;

        /// <inheritdoc cref="OnStarted" />
        public UnityEvent OnCompleted => onCompleted;

        /// <inheritdoc cref="OnStarted" />
        public UnityEvent OnSkipped => onSkipped;

        /// <inheritdoc cref="OnStarted" />
        public UnityEvent OnStopped => onStopped;

        /// <inheritdoc cref="OnStarted" />
        public UnityEvent OnFailed => onFailed;

        /// <inheritdoc />
        public override async Awaitable Execute(CommandContext context)
        {
            onStarted.Invoke();
            CommandStatus status;
            try
            {
                status = await context.RunSingle(command);
            }
            catch (CommandFailedException)
            {
                onFailed.Invoke();
                throw;
            }
            catch (OperationCanceledException)
            {
                (context.IsSequenceStopping ? onStopped : onSkipped).Invoke();
                throw;
            }

            switch (status)
            {
                case CommandStatus.Completed:
                    onCompleted.Invoke();
                    break;
                case CommandStatus.Skipped:
                case CommandStatus.Disabled:
                    onSkipped.Invoke();
                    break;
                case CommandStatus.Stopped:
                    onStopped.Invoke();
                    break;
            }
        }
    }
}

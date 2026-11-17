using System;
using System.Collections.Generic;
using RonJames.Foundation.Values;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>Waits for a number of seconds, reporting progress.</summary>
    public class DelayCommand : Command
    {
        [SerializeField]
        private VariableReference<float> seconds = new VariableReference<float>(1f);

        [SerializeField]
        [Tooltip("Ignore Time.timeScale (keeps counting while the game is paused).")]
        private bool unscaledTime;

        /// <summary>Creates a one-second delay.</summary>
        public DelayCommand()
        {
        }

        /// <summary>Creates a delay from code.</summary>
        public DelayCommand(float seconds, bool unscaledTime = false)
        {
            this.seconds = new VariableReference<float>(seconds);
            this.unscaledTime = unscaledTime;
        }

        /// <inheritdoc />
        public override async Awaitable Execute(CommandContext context)
        {
            float duration = seconds.Value;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                context.ReportProgress(elapsed / duration, $"{duration - elapsed:0.0}s left");
                await Awaitable.NextFrameAsync(context.Token);
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }

            context.ReportProgress(1f);
        }
    }

    /// <summary>Runs one of two Commands depending on a Condition.</summary>
    public class BranchCommand : Command
    {
        [OdinSerialize]
        [Required("A Branch needs a Condition.")]
        private ICondition condition;

        [OdinSerialize]
        [LabelText("Then")]
        private ICommand thenCommand;

        [OdinSerialize]
        [LabelText("Else")]
        private ICommand elseCommand;

        /// <summary>Creates an empty Branch.</summary>
        public BranchCommand()
        {
        }

        /// <summary>Creates a Branch from code.</summary>
        public BranchCommand(ICondition condition, ICommand thenCommand, ICommand elseCommand = null)
        {
            this.condition = condition;
            this.thenCommand = thenCommand;
            this.elseCommand = elseCommand;
        }

        /// <inheritdoc />
        public override async Awaitable Execute(CommandContext context)
        {
            if (condition == null)
            {
                throw new InvalidOperationException("Branch has no Condition.");
            }

            ICommand chosen = condition.IsMet() ? thenCommand : elseCommand;
            if (chosen != null)
            {
                await context.RunSingle(chosen);
            }
        }
    }

    /// <summary>Runs the Command at an index, or a default Command when the index has no case.</summary>
    public class SwitchCommand : Command
    {
        [SerializeField]
        private VariableReference<int> index = new VariableReference<int>();

        [OdinSerialize]
        [ListDrawerSettings(ShowIndexLabels = true)]
        private List<ICommand> cases = new List<ICommand>();

        [OdinSerialize]
        [LabelText("Default")]
        private ICommand defaultCase;

        /// <summary>Creates an empty Switch.</summary>
        public SwitchCommand()
        {
        }

        /// <summary>Creates a Switch from code.</summary>
        public SwitchCommand(VariableReference<int> index, ICommand defaultCase, params ICommand[] cases)
        {
            this.index = index;
            this.defaultCase = defaultCase;
            this.cases = new List<ICommand>(cases);
        }

        /// <inheritdoc />
        public override async Awaitable Execute(CommandContext context)
        {
            int i = index.Value;
            ICommand chosen = i >= 0 && i < cases.Count ? cases[i] : defaultCase;
            if (chosen != null)
            {
                await context.RunSingle(chosen);
            }
        }
    }

    /// <summary>Runs another Command Sequence's Commands as part of this Run (they appear nested in this Run's tree).</summary>
    public class RunCommandSequenceCommand : Command
    {
        [SerializeField]
        [Required]
        private CommandSequence target;

        /// <summary>Creates an empty command.</summary>
        public RunCommandSequenceCommand()
        {
        }

        /// <summary>Creates the command from code.</summary>
        public RunCommandSequenceCommand(CommandSequence target)
        {
            this.target = target;
        }

        /// <inheritdoc />
        public override Awaitable Execute(CommandContext context)
        {
            if (target == null)
            {
                throw new InvalidOperationException("Run Command Sequence has no target.");
            }

            return context.RunSequence(target.Commands, target.ContinueOnFailure);
        }
    }

    /// <summary>Waits until an Event (or Variable Event) is Raised.</summary>
    public class WaitForEventCommand : Command
    {
        [OdinSerialize]
        [Required]
        private Events.IEvent target;

        /// <summary>Creates an empty command.</summary>
        public WaitForEventCommand()
        {
        }

        /// <summary>Creates the command from code.</summary>
        public WaitForEventCommand(Events.IEvent target)
        {
            this.target = target;
        }

        /// <inheritdoc />
        public override async Awaitable Execute(CommandContext context)
        {
            if (UnityNull.IsNull(target))
            {
                throw new InvalidOperationException("Wait For Event has no Event.");
            }

            var raised = new AwaitableCompletionSource();
            Action onRaised = () => raised.TrySetResult();
            target.Subscribe(onRaised);
            try
            {
                using (context.Token.Register(() => raised.TrySetCanceled()))
                {
                    await raised.Awaitable;
                }
            }
            finally
            {
                target.Unsubscribe(onRaised);
            }
        }
    }

    /// <summary>
    /// Waits until a Condition is met. Conditions that can notify (bool and filter conditions over
    /// Variable Events) are re-checked only when their values change; others are polled every frame.
    /// </summary>
    public class WaitUntilCommand : Command
    {
        [OdinSerialize]
        [Required("Wait Until needs a Condition.")]
        private ICondition condition;

        /// <summary>Creates an empty command.</summary>
        public WaitUntilCommand()
        {
        }

        /// <summary>Creates the command from code.</summary>
        public WaitUntilCommand(ICondition condition)
        {
            this.condition = condition;
        }

        /// <inheritdoc />
        public override async Awaitable Execute(CommandContext context)
        {
            if (condition == null)
            {
                throw new InvalidOperationException("Wait Until has no Condition.");
            }

            if (condition.IsMet())
            {
                return;
            }

            if (condition is IObservableCondition observable)
            {
                var met = new AwaitableCompletionSource();
                Action check = () =>
                {
                    if (condition.IsMet())
                    {
                        met.TrySetResult();
                    }
                };

                if (observable.TryAddChangedListener(check))
                {
                    try
                    {
                        using (context.Token.Register(() => met.TrySetCanceled()))
                        {
                            await met.Awaitable;
                        }
                    }
                    finally
                    {
                        observable.RemoveChangedListener(check);
                    }

                    return;
                }
            }

            while (!condition.IsMet())
            {
                await Awaitable.NextFrameAsync(context.Token);
            }
        }
    }
}

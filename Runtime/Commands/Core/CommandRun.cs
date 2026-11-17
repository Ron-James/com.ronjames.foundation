using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>How a Run ended (or that it is still going).</summary>
    public enum RunStatus
    {
        Running,
        Completed,
        Stopped,
        Failed
    }

    /// <summary>
    /// One execution of a Sequence from start to finish. It owns all live state (ADR 0003); its
    /// <see cref="Root"/> node is the Run tree the inspector shows.
    /// </summary>
    public sealed class CommandRun
    {
        private readonly CancellationTokenSource stopSource = new CancellationTokenSource();
        private readonly ICommand[] commands;
        private readonly bool continueOnFailure;
        private CommandScope topScope;

        internal CommandRun(CommandSequence owner, IReadOnlyList<ICommand> commands, bool continueOnFailure, int number)
        {
            Owner = owner;
            Number = number;
            this.continueOnFailure = continueOnFailure;
            this.commands = new ICommand[commands?.Count ?? 0];
            for (int i = 0; i < this.commands.Length; i++)
            {
                this.commands[i] = commands[i];
            }

            Root = new RunNode(null, owner != null ? owner.name : "Run", null);
        }

        /// <summary>The Command Sequence running this Run.</summary>
        public CommandSequence Owner { get; }

        /// <summary>1 for the Command Sequence's first Run, 2 for its second, and so on.</summary>
        public int Number { get; }

        /// <summary>The root of the Run tree; its children are the top-level Commands.</summary>
        public RunNode Root { get; }

        /// <summary>Running, or how the Run ended.</summary>
        public RunStatus Status { get; private set; } = RunStatus.Running;

        /// <summary>True until the Run ends.</summary>
        public bool IsRunning => Status == RunStatus.Running;

        /// <summary>Ends the Run. Every running Command's cleanup still runs.</summary>
        public void Stop()
        {
            if (!stopSource.IsCancellationRequested)
            {
                stopSource.Cancel();
            }
        }

        /// <summary>Ends the current top-level Command early (a Composite counts as one Command); the Run continues.</summary>
        public void Skip()
        {
            topScope?.Current?.RequestSkip();
        }

        internal async Awaitable Execute()
        {
            Root.Begin(null);
            var nodes = new List<RunNode>(commands.Length);
            foreach (ICommand command in commands)
            {
                nodes.Add(Root.AddChild(command));
            }

            using (var scope = new CommandScope(this, stopSource.Token, null, null, continueOnFailure))
            {
                topScope = scope;
                try
                {
                    await CommandExecution.RunSequence(scope, nodes);
                }
                finally
                {
                    topScope = null;
                    if (scope.Failed)
                    {
                        Status = RunStatus.Failed;
                        Root.End(CommandStatus.Failed);
                    }
                    else if (stopSource.IsCancellationRequested || scope.StopRequested)
                    {
                        Status = RunStatus.Stopped;
                        Root.End(CommandStatus.Stopped);
                    }
                    else
                    {
                        Status = RunStatus.Completed;
                        Root.End(CommandStatus.Completed);
                    }
                }
            }
        }
    }
}

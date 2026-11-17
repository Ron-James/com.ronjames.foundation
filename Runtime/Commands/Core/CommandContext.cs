using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>
    /// What a Command is given when it executes: the cancellation token, progress reporting, the
    /// three control requests, and the helpers Composites use to run their children.
    /// </summary>
    public sealed class CommandContext
    {
        /// <summary>Deeper nesting than this is refused, which catches a Sequence that runs itself.</summary>
        public const int MaxNestingDepth = 64;

        private readonly CommandScope scope;

        internal CommandContext(CommandScope scope, RunNode node, CancellationToken token)
        {
            this.scope = scope;
            Node = node;
            Token = token;
        }

        /// <summary>
        /// Cancelled when this Command is Skipped or its Sequence is Stopped. Pass it to every await,
        /// e.g. <c>await Awaitable.NextFrameAsync(context.Token)</c>.
        /// </summary>
        public CancellationToken Token { get; }

        /// <summary>This Command's live node in the Run tree.</summary>
        public RunNode Node { get; }

        /// <summary>The Run this Command belongs to.</summary>
        public CommandRun Run => scope.Run;

        /// <summary>The Command Sequence running this Run.</summary>
        public CommandSequence Owner => scope.Run.Owner;

        /// <summary>The GameObject of the Command Sequence running this Run.</summary>
        public GameObject GameObject => Owner != null ? Owner.gameObject : null;

        /// <summary>True when the Sequence this Command is in is Stopping (as opposed to just this Command being Skipped).</summary>
        public bool IsSequenceStopping => scope.Token.IsCancellationRequested;

        /// <summary>Reports progress (0 to 1) and an optional label to the Run tree.</summary>
        public void ReportProgress(float progress, string label = null) => Node.ReportProgress(progress, label);

        /// <summary>Stop Sequence: ends the Sequence this Command is in.</summary>
        public void StopSequence() => scope.Stop();

        /// <summary>Skip Parent: ends the Composite enclosing this Command; that Composite's Sequence continues.</summary>
        public void SkipParent() => scope.SkipOwner();

        /// <summary>Stop Parent: Stops the Sequence that contains the Composite enclosing this Command.</summary>
        public void StopParent() => scope.StopParent();

        /// <summary>Runs <paramref name="commands"/> one after another as this Command's children.</summary>
        public async Awaitable RunSequence(IReadOnlyList<ICommand> commands, bool continueOnFailure = false)
        {
            IReadOnlyList<RunNode> nodes = CreateChildren(commands);
            using (var child = new CommandScope(scope.Run, Token, scope, Node, continueOnFailure))
            {
                await CommandExecution.RunSequence(child, nodes);
                Finish(child);
            }
        }

        /// <summary>Runs <paramref name="commands"/> at the same time and finishes when all have finished.</summary>
        public async Awaitable RunParallel(IReadOnlyList<ICommand> commands)
        {
            IReadOnlyList<RunNode> nodes = CreateChildren(commands);
            using (var child = new CommandScope(scope.Run, Token, scope, Node, false))
            {
                await CommandExecution.RunParallel(child, nodes);
                Finish(child);
            }
        }

        /// <summary>Runs <paramref name="commands"/> at the same time, finishes with the first, and Skips the rest.</summary>
        public async Awaitable RunRace(IReadOnlyList<ICommand> commands)
        {
            IReadOnlyList<RunNode> nodes = CreateChildren(commands);
            using (var child = new CommandScope(scope.Run, Token, scope, Node, false))
            {
                await CommandExecution.RunRace(child, nodes);
                Finish(child);
            }
        }

        /// <summary>
        /// Runs one child Command and returns how it ended (Completed, Skipped, Stopped or Disabled).
        /// Throws if the child Failed, or if this Command itself was Skipped or Stopped meanwhile.
        /// </summary>
        public async Awaitable<CommandStatus> RunSingle(ICommand command)
        {
            IReadOnlyList<RunNode> nodes = CreateChildren(new[] { command });
            using (var child = new CommandScope(scope.Run, Token, scope, Node, false))
            {
                await CommandExecution.ExecuteNode(child, nodes[0]);
                Finish(child);
                return nodes[0].Status;
            }
        }

        private IReadOnlyList<RunNode> CreateChildren(IReadOnlyList<ICommand> commands)
        {
            if (Node.Depth >= MaxNestingDepth)
            {
                throw new InvalidOperationException(
                    $"Commands are nested more than {MaxNestingDepth} deep at {Node.Path}. Is a Run Command Sequence running its own Sequence?");
            }

            var nodes = new List<RunNode>(commands?.Count ?? 0);
            if (commands != null)
            {
                foreach (ICommand command in commands)
                {
                    nodes.Add(Node.AddChild(command));
                }
            }

            return nodes;
        }

        private void Finish(CommandScope child)
        {
            if (child.Failed && !child.ContinueOnFailure)
            {
                throw new CommandFailedException(child.FirstFailure);
            }

            Token.ThrowIfCancellationRequested();
        }
    }
}

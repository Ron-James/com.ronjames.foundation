using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>
    /// Runs Commands inside a scope and turns how each one ended into a status. This is the only
    /// place that decides Skipped vs Stopped vs Failed, so every Composite behaves the same way.
    /// </summary>
    internal static class CommandExecution
    {
        /// <summary>
        /// Executes one node. Never throws: every outcome is recorded on the node, and failures mark
        /// the scope (which Stops it unless the scope continues on failure).
        /// </summary>
        public static async Awaitable ExecuteNode(CommandScope scope, RunNode node)
        {
            ICommand command = node.Command;
            if (command == null)
            {
                node.Begin(null);
                node.End(CommandStatus.Skipped);
                Debug.LogWarning($"[RonJames Foundation] Empty Command slot passed over: {node.Path}", scope.Run.Owner);
                return;
            }

            if (!command.Enabled)
            {
                node.MarkDisabled();
                return;
            }

            if (scope.Token.IsCancellationRequested)
            {
                return;
            }

            var skipSource = CancellationTokenSource.CreateLinkedTokenSource(scope.Token);
            node.Begin(skipSource);
            var context = new CommandContext(scope, node, skipSource.Token);
            try
            {
                Awaitable work = command.Execute(context);
                if (work == null)
                {
                    throw new InvalidOperationException(
                        $"{command.GetType().Name}.Execute returned null. Return an Awaitable, or derive from InstantCommand for synchronous work.");
                }

                await work;
                node.End(skipSource.IsCancellationRequested ? EndedEarlyStatus(scope) : CommandStatus.Completed);
            }
            catch (OperationCanceledException) when (skipSource.IsCancellationRequested)
            {
                node.End(EndedEarlyStatus(scope));
            }
            catch (CommandFailedException failure)
            {
                node.End(CommandStatus.Failed);
                RecordFailure(scope, failure.FailedNode);
            }
            catch (Exception exception)
            {
                node.End(CommandStatus.Failed, exception);
                Debug.LogError($"[RonJames Foundation] Command failed: {node.Path}\n{exception}", scope.Run.Owner);
                RecordFailure(scope, node);
            }
            finally
            {
                skipSource.Dispose();
            }
        }

        /// <summary>Runs nodes one after another until the scope Stops.</summary>
        public static async Awaitable RunSequence(CommandScope scope, IReadOnlyList<RunNode> nodes)
        {
            foreach (RunNode node in nodes)
            {
                if (scope.Token.IsCancellationRequested)
                {
                    break;
                }

                scope.Current = node;
                await ExecuteNode(scope, node);
                scope.Current = null;
            }
        }

        /// <summary>Starts every node at once and finishes when all have finished.</summary>
        public static async Awaitable RunParallel(CommandScope scope, IReadOnlyList<RunNode> nodes)
        {
            var running = new List<Awaitable>(nodes.Count);
            foreach (RunNode node in nodes)
            {
                running.Add(ExecuteNode(scope, node));
            }

            foreach (Awaitable branch in running)
            {
                await branch;
            }
        }

        /// <summary>
        /// Starts every enabled node at once, finishes when the first one finishes, and Skips the rest.
        /// The rest are awaited, so their cleanup has run before the race completes.
        /// </summary>
        public static async Awaitable RunRace(CommandScope scope, IReadOnlyList<RunNode> nodes)
        {
            var contenders = new List<RunNode>(nodes.Count);
            foreach (RunNode node in nodes)
            {
                if (node.Command != null && node.Command.Enabled)
                {
                    contenders.Add(node);
                }
                else
                {
                    await ExecuteNode(scope, node);
                }
            }

            if (contenders.Count == 0)
            {
                return;
            }

            var firstFinished = new AwaitableCompletionSource();
            bool decided = false;

            async Awaitable Branch(RunNode node)
            {
                await ExecuteNode(scope, node);
                if (!decided)
                {
                    decided = true;
                    firstFinished.SetResult();
                }
            }

            var running = new List<Awaitable>(contenders.Count);
            foreach (RunNode node in contenders)
            {
                running.Add(Branch(node));
            }

            await firstFinished.Awaitable;
            foreach (RunNode node in contenders)
            {
                node.RequestSkip();
            }

            foreach (Awaitable branch in running)
            {
                await branch;
            }
        }

        private static CommandStatus EndedEarlyStatus(CommandScope scope)
        {
            return scope.Token.IsCancellationRequested ? CommandStatus.Stopped : CommandStatus.Skipped;
        }

        private static void RecordFailure(CommandScope scope, RunNode failedNode)
        {
            if (!scope.Failed)
            {
                scope.Failed = true;
                scope.FirstFailure = failedNode;
            }

            if (!scope.ContinueOnFailure)
            {
                scope.Stop();
            }
        }
    }
}

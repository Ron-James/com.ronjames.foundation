using System;
using System.Threading;
using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>
    /// The Sequence (or parallel group) a Command is running in. It owns the "stop" token for that
    /// group and knows the Composite that owns it, which is what makes Stop Sequence, Skip Parent and
    /// Stop Parent reach exactly one level up.
    /// </summary>
    internal sealed class CommandScope : IDisposable
    {
        private readonly CancellationTokenSource stopSource;

        internal CommandScope(CommandRun run, CancellationToken outerToken, CommandScope parent, RunNode ownerNode, bool continueOnFailure)
        {
            Run = run;
            Parent = parent;
            OwnerNode = ownerNode;
            ContinueOnFailure = continueOnFailure;
            stopSource = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        }

        public CommandRun Run { get; }

        /// <summary>The scope the owning Composite is in; null for the Run's top-level Sequence.</summary>
        public CommandScope Parent { get; }

        /// <summary>The Composite that runs this scope; null for the Run's top-level Sequence.</summary>
        public RunNode OwnerNode { get; }

        public bool ContinueOnFailure { get; }

        /// <summary>Cancelled when this scope is Stopped, or when anything enclosing it ends.</summary>
        public CancellationToken Token => stopSource.Token;

        /// <summary>True once a Command in this scope Failed.</summary>
        public bool Failed { get; set; }

        /// <summary>The node whose error first Failed this scope.</summary>
        public RunNode FirstFailure { get; set; }

        /// <summary>True once this scope itself was asked to Stop.</summary>
        public bool StopRequested { get; private set; }

        /// <summary>The Command running now, for Sequences (parallel groups leave it null).</summary>
        public RunNode Current { get; set; }

        public void Stop()
        {
            StopRequested = true;
            if (!stopSource.IsCancellationRequested)
            {
                stopSource.Cancel();
            }
        }

        /// <summary>Skip Parent: ends the Composite that owns this scope.</summary>
        public void SkipOwner()
        {
            if (OwnerNode == null)
            {
                Debug.LogWarning("[RonJames Foundation] Skip Parent was requested by a top-level Command, which has no enclosing Composite. The Run was Stopped instead.", Run.Owner);
                Stop();
                return;
            }

            OwnerNode.RequestSkip();
        }

        /// <summary>Stop Parent: Stops the Sequence that contains the owning Composite.</summary>
        public void StopParent()
        {
            if (Parent == null)
            {
                Debug.LogWarning("[RonJames Foundation] Stop Parent was requested by a top-level Command, which has no enclosing Composite. The Run was Stopped instead.", Run.Owner);
                Stop();
                return;
            }

            Parent.Stop();
        }

        public void Dispose() => stopSource.Dispose();
    }
}

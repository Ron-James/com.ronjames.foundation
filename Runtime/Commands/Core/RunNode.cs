using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>Where a Command is in a Run.</summary>
    public enum CommandStatus
    {
        /// <summary>Not reached yet (or never reached, because its Sequence Stopped first).</summary>
        Pending,

        /// <summary>Executing now.</summary>
        Running,

        /// <summary>Finished normally.</summary>
        Completed,

        /// <summary>Ended early; its Sequence carried on.</summary>
        Skipped,

        /// <summary>Ended because its Sequence was Stopped.</summary>
        Stopped,

        /// <summary>Ended with an error.</summary>
        Failed,

        /// <summary>Passed over because it is a Disabled Command.</summary>
        Disabled
    }

    /// <summary>
    /// The live state of one Command within a Run: status, timing, reported progress and nested
    /// children. The Run tree the inspector shows is made of these.
    /// </summary>
    public sealed class RunNode
    {
        private readonly List<RunNode> children = new List<RunNode>();
        private CancellationTokenSource skipSource;
        private double startTime;
        private double endTime;

        internal RunNode(ICommand command, string name, RunNode parent)
        {
            Command = command;
            Name = name;
            Parent = parent;
            Depth = parent == null ? 0 : parent.Depth + 1;
        }

        /// <summary>The Command (null for the root node, which stands for the Command Sequence).</summary>
        public ICommand Command { get; }

        /// <summary>Display name.</summary>
        public string Name { get; }

        /// <summary>The enclosing node, or null for the root.</summary>
        public RunNode Parent { get; }

        /// <summary>Nesting depth; the root is 0.</summary>
        public int Depth { get; }

        /// <summary>Nested nodes, created when a Composite runs its children.</summary>
        public IReadOnlyList<RunNode> Children => children;

        /// <summary>Current status.</summary>
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;

        /// <summary>Last reported progress from 0 to 1, or a negative number when none was reported.</summary>
        public float Progress { get; private set; } = -1f;

        /// <summary>Last reported progress label, if any.</summary>
        public string ProgressLabel { get; private set; }

        /// <summary>The error a Failed Command ended with (null when it only failed because a child failed).</summary>
        public Exception Exception { get; private set; }

        /// <summary>Seconds spent running (still counting while Running).</summary>
        public double Elapsed
        {
            get
            {
                switch (Status)
                {
                    case CommandStatus.Pending:
                    case CommandStatus.Disabled:
                        return 0d;
                    case CommandStatus.Running:
                        return Time.realtimeSinceStartupAsDouble - startTime;
                    default:
                        return endTime - startTime;
                }
            }
        }

        /// <summary>The names from the root down to this node, e.g. "Door > Open > Delay".</summary>
        public string Path => Parent == null ? Name : $"{Parent.Path} > {Name}";

        internal RunNode AddChild(ICommand command)
        {
            var child = new RunNode(command, command?.Name ?? "(missing command)", this);
            children.Add(child);
            return child;
        }

        internal void Begin(CancellationTokenSource skip)
        {
            skipSource = skip;
            startTime = Time.realtimeSinceStartupAsDouble;
            Status = CommandStatus.Running;
        }

        internal void End(CommandStatus status, Exception exception = null)
        {
            skipSource = null;
            endTime = Time.realtimeSinceStartupAsDouble;
            Status = status;
            Exception = exception;
        }

        internal void MarkDisabled() => Status = CommandStatus.Disabled;

        internal void ReportProgress(float progress, string label)
        {
            Progress = Mathf.Clamp01(progress);
            ProgressLabel = label;
        }

        /// <summary>Ends this Command early, if it is running. Its Sequence continues.</summary>
        internal void RequestSkip()
        {
            CancellationTokenSource source = skipSource;
            if (source != null && !source.IsCancellationRequested)
            {
                source.Cancel();
            }
        }

        /// <summary>The deepest running descendant, or this node itself.</summary>
        public RunNode ActiveLeaf
        {
            get
            {
                foreach (RunNode child in children)
                {
                    if (child.Status == CommandStatus.Running)
                    {
                        return child.ActiveLeaf;
                    }
                }

                return this;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"{Name} [{Status}]";
    }
}

using System;

namespace RonJames.Foundation.Commands
{
    /// <summary>
    /// Thrown by a Composite when one of its children Failed, so the failure propagates up the Run
    /// without being logged again. The original error was already logged with the child's path.
    /// </summary>
    public sealed class CommandFailedException : Exception
    {
        /// <summary>The node that originally Failed.</summary>
        public RunNode FailedNode { get; }

        internal CommandFailedException(RunNode failedNode)
            : base($"Command failed: {failedNode?.Path}")
        {
            FailedNode = failedNode;
        }
    }
}

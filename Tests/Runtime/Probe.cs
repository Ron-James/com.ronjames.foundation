using System;
using System.Collections.Generic;
using RonJames.Foundation.Commands;
using UnityEngine;

namespace RonJames.Foundation.Tests
{
    /// <summary>
    /// A test Command that records "start:id", "end:id" and "cleanup:id" into a shared log, optionally
    /// waits a number of frames, and can act on its context when it starts.
    /// </summary>
    public sealed class Probe : Command
    {
        private readonly string id;
        private readonly List<string> log;
        private readonly int frames;
        private readonly Action<CommandContext> onStart;

        public Probe(string id, List<string> log, int frames = 0, Action<CommandContext> onStart = null)
        {
            this.id = id;
            this.log = log;
            this.frames = frames;
            this.onStart = onStart;
            Named(id);
        }

        public override async Awaitable Execute(CommandContext context)
        {
            log.Add("start:" + id);
            try
            {
                onStart?.Invoke(context);
                for (int i = 0; i < frames; i++)
                {
                    await Awaitable.NextFrameAsync(context.Token);
                }

                log.Add("end:" + id);
            }
            finally
            {
                log.Add("cleanup:" + id);
            }
        }
    }

    /// <summary>A test Command that always throws.</summary>
    public sealed class Throws : InstantCommand
    {
        protected override void Run(CommandContext context) => throw new InvalidOperationException("probe failure");
    }
}

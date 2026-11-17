using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RonJames.Foundation.Commands;
using RonJames.Foundation.Events;
using RonJames.Foundation.Values;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RonJames.Foundation.Tests
{
    public class CommandRunTests
    {
        private const int LongFrames = 100000;

        private readonly List<string> log = new List<string>();
        private readonly List<Object> created = new List<Object>();
        private CommandSequence sequence;

        [SetUp]
        public void SetUp()
        {
            log.Clear();
            var gameObject = new GameObject("Sequence Under Test");
            created.Add(gameObject);
            sequence = gameObject.AddComponent<CommandSequence>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.Destroy(item);
                }
            }

            created.Clear();
        }

        private Probe P(string id, int frames = 0, System.Action<CommandContext> onStart = null) => new Probe(id, log, frames, onStart);

        private IEnumerator RunToIdle(params ICommand[] commands)
        {
            sequence.SetCommands(commands);
            sequence.Run();
            yield return WaitIdle();
        }

        private IEnumerator WaitIdle(float timeoutSeconds = 10f)
        {
            // Real time, not frames: batch-mode frames are so short that a frame budget runs out
            // before game time has advanced.
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (sequence.IsRunning && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(sequence.IsRunning, "The Command Sequence did not finish in time.");
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private RunNode Node(int index) => sequence.LastRun.Root.Children[index];

        [UnityTest]
        public IEnumerator Commands_RunInOrder()
        {
            yield return RunToIdle(P("a"), P("b", frames: 2), P("c"));

            Assert.AreEqual(new[] { "start:a", "end:a", "cleanup:a", "start:b", "end:b", "cleanup:b", "start:c", "end:c", "cleanup:c" }, log);
            Assert.AreEqual(RunStatus.Completed, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator Delay_WaitsForItsDuration()
        {
            float started = Time.time;
            yield return RunToIdle(new DelayCommand(0.2f), P("after"));

            Assert.GreaterOrEqual(Time.time - started, 0.19f);
            Assert.Contains("end:after", log);
        }

        [UnityTest]
        public IEnumerator Skip_EndsTheCurrentCommand_AndTheRunContinues()
        {
            sequence.SetCommands(new ICommand[] { P("long", LongFrames), P("next") });
            sequence.Run();
            yield return Frames(3);

            sequence.Skip();
            yield return WaitIdle();

            Assert.AreEqual(CommandStatus.Skipped, Node(0).Status);
            Assert.Contains("cleanup:long", log);
            Assert.Contains("end:next", log);
            Assert.AreEqual(RunStatus.Completed, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator Stop_EndsTheRun_AndCleanupStillRuns()
        {
            sequence.SetCommands(new ICommand[] { P("long", LongFrames), P("never") });
            sequence.Run();
            yield return Frames(3);

            sequence.Stop();
            yield return WaitIdle();

            Assert.AreEqual(CommandStatus.Stopped, Node(0).Status);
            Assert.AreEqual(CommandStatus.Pending, Node(1).Status);
            Assert.Contains("cleanup:long", log);
            Assert.IsFalse(log.Contains("start:never"));
            Assert.AreEqual(RunStatus.Stopped, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator StopSequence_EndsOnlyTheSequenceTheCommandIsIn()
        {
            yield return RunToIdle(
                new SequenceComposite(P("stopper", onStart: context => context.StopSequence()), P("inner-never")),
                P("outer-after"));

            Assert.IsFalse(log.Contains("start:inner-never"));
            Assert.Contains("end:outer-after", log);
            Assert.AreEqual(RunStatus.Completed, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator SkipParent_EndsTheEnclosingComposite_AndItsSequenceContinues()
        {
            yield return RunToIdle(
                new SequenceComposite(P("skipper", onStart: context => context.SkipParent()), P("inner-never")),
                P("outer-after"));

            Assert.AreEqual(CommandStatus.Skipped, Node(0).Status);
            Assert.IsFalse(log.Contains("start:inner-never"));
            Assert.Contains("end:outer-after", log);
            Assert.AreEqual(RunStatus.Completed, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator StopParent_StopsTheSequenceContainingTheComposite()
        {
            yield return RunToIdle(
                new SequenceComposite(P("stopper", onStart: context => context.StopParent()), P("inner-never")),
                P("outer-never"));

            Assert.IsFalse(log.Contains("start:inner-never"));
            Assert.IsFalse(log.Contains("start:outer-never"));
            Assert.AreEqual(RunStatus.Stopped, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator Failure_StopsTheSequence_AndFailsTheRun()
        {
            LogAssert.Expect(LogType.Error, new Regex("Command failed"));
            yield return RunToIdle(new Throws(), P("never"));

            Assert.AreEqual(CommandStatus.Failed, Node(0).Status);
            Assert.IsNotNull(Node(0).Exception);
            Assert.IsFalse(log.Contains("start:never"));
            Assert.AreEqual(RunStatus.Failed, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator ContinueOnFailure_KeepsGoing()
        {
            sequence.ContinueOnFailure = true;
            LogAssert.Expect(LogType.Error, new Regex("Command failed"));
            yield return RunToIdle(new Throws(), P("after"));

            Assert.Contains("end:after", log);
            Assert.AreEqual(RunStatus.Failed, sequence.LastRun.Status, "The Run still reports that something failed.");
        }

        [UnityTest]
        public IEnumerator NestedFailure_PropagatesThroughComposites()
        {
            LogAssert.Expect(LogType.Error, new Regex("Command failed"));
            yield return RunToIdle(new SequenceComposite(new SequenceComposite(new Throws())), P("never"));

            Assert.AreEqual(CommandStatus.Failed, Node(0).Status);
            Assert.IsFalse(log.Contains("start:never"));
            Assert.AreEqual(RunStatus.Failed, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator Parallel_FinishesWhenAllChildrenHaveFinished()
        {
            yield return RunToIdle(new ParallelComposite(P("short", 2), P("long", 6)), P("after"));

            int afterStart = log.IndexOf("start:after");
            Assert.Greater(afterStart, log.IndexOf("end:short"));
            Assert.Greater(afterStart, log.IndexOf("end:long"));
        }

        [UnityTest]
        public IEnumerator Race_FinishesWithTheFirst_AndSkipsTheRest()
        {
            yield return RunToIdle(new RaceComposite(P("fast", 2), P("slow", LongFrames)), P("after"));

            RunNode race = Node(0);
            Assert.AreEqual(CommandStatus.Completed, race.Children[0].Status);
            Assert.AreEqual(CommandStatus.Skipped, race.Children[1].Status);
            Assert.Less(log.IndexOf("cleanup:slow"), log.IndexOf("start:after"), "The losers' cleanup runs before the race completes.");
        }

        [UnityTest]
        public IEnumerator DisabledCommands_ArePassedOver()
        {
            Probe off = P("off");
            off.Enabled = false;
            yield return RunToIdle(off, P("on"));

            Assert.AreEqual(CommandStatus.Disabled, Node(0).Status);
            Assert.IsFalse(log.Contains("start:off"));
            Assert.Contains("end:on", log);
        }

        [UnityTest]
        public IEnumerator Queue_EachRequestRunsOnceInOrder()
        {
            sequence.SetCommands(new ICommand[] { P("a", 2) });
            sequence.Run();
            sequence.Run();
            sequence.Run();
            Assert.AreEqual(2, sequence.QueuedRunCount);

            yield return WaitIdle();

            Assert.AreEqual(3, log.Count(entry => entry == "end:a"));
            Assert.AreEqual(3, sequence.LastRun.Number);
        }

        [UnityTest]
        public IEnumerator Restart_StopsTheCurrentRun_AndStartsOver()
        {
            var ended = new List<CommandRun>();
            sequence.RunEnded += ended.Add;
            sequence.WhenAlreadyRunning = WhenAlreadyRunning.Restart;
            sequence.SetCommands(new ICommand[] { P("long", 5) });

            sequence.Run();
            yield return Frames(2);
            sequence.Run();
            sequence.Run();
            yield return WaitIdle();

            Assert.AreEqual(2, ended.Count, "Restart discards extra requests: one stopped Run, one fresh Run.");
            Assert.AreEqual(RunStatus.Stopped, ended[0].Status);
            Assert.AreEqual(RunStatus.Completed, ended[1].Status);
        }

        [UnityTest]
        public IEnumerator DisablingTheComponent_StopsTheRun()
        {
            sequence.SetCommands(new ICommand[] { P("long", LongFrames) });
            sequence.Run();
            yield return Frames(2);

            sequence.enabled = false;
            yield return WaitIdle();

            Assert.Contains("cleanup:long", log);
            Assert.AreEqual(RunStatus.Stopped, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator WaitForEvent_WaitsUntilTheEventIsRaised()
        {
            var signal = ScriptableObject.CreateInstance<EventAsset>();
            created.Add(signal);
            sequence.SetCommands(new ICommand[] { new WaitForEventCommand(signal), P("after") });
            sequence.Run();
            yield return Frames(3);
            Assert.IsFalse(log.Contains("start:after"));

            signal.Raise();
            yield return WaitIdle();

            Assert.Contains("end:after", log);
            Assert.AreEqual(0, signal.ListenerCount, "The wait unsubscribes when it ends.");
        }

        [UnityTest]
        public IEnumerator WaitForEvent_CanWaitForSeveralRaises()
        {
            var signal = ScriptableObject.CreateInstance<EventAsset>();
            created.Add(signal);
            sequence.SetCommands(new ICommand[] { new WaitForEventCommand(signal, requiredRaises: 2), P("after") });
            sequence.Run();
            yield return Frames(2);

            signal.Raise();
            yield return Frames(2);
            Assert.IsFalse(log.Contains("start:after"), "One Raise of two is not enough.");

            signal.Raise();
            yield return WaitIdle();
            Assert.Contains("end:after", log);
        }

        [UnityTest]
        public IEnumerator ControlCommand_MakesControlRequestsFromTheInspector()
        {
            yield return RunToIdle(
                new SequenceComposite(P("inner"), new ControlCommand(ControlRequest.SkipParent), P("inner-never")),
                P("outer-after"),
                new ControlCommand(ControlRequest.StopSequence),
                P("outer-never"));

            Assert.AreEqual("Skip Parent", Node(0).Children[1].Name);
            Assert.AreEqual(CommandStatus.Skipped, Node(0).Status);
            Assert.IsFalse(log.Contains("start:inner-never"));
            Assert.Contains("end:outer-after", log);
            Assert.IsFalse(log.Contains("start:outer-never"));
            Assert.AreEqual(RunStatus.Stopped, sequence.LastRun.Status);
        }

        [UnityTest]
        public IEnumerator WaitUntil_UsesChangeNotificationsFromVariableEvents()
        {
            var flag = ScriptableObject.CreateInstance<BoolVariableEvent>();
            created.Add(flag);
            sequence.SetCommands(new ICommand[]
            {
                new WaitUntilCommand(new BoolCondition(VariableReference<bool>.FromSource(flag))),
                P("after")
            });
            sequence.Run();
            yield return Frames(3);
            Assert.AreEqual(1, flag.ListenerCount, "An observable condition subscribes instead of polling.");

            flag.Set(true);
            yield return WaitIdle();

            Assert.Contains("end:after", log);
            Assert.AreEqual(0, flag.ListenerCount);
        }

        [UnityTest]
        public IEnumerator SetVariable_Branch_AndRaiseEvent_WorkTogether()
        {
            var number = ScriptableObject.CreateInstance<IntVariableEvent>();
            var signal = ScriptableObject.CreateInstance<EventAsset>();
            created.Add(number);
            created.Add(signal);
            int raised = 0;
            signal.Subscribe((System.Action)(() => raised++));

            yield return RunToIdle(
                new SetVariableCommand<int>(number, 5),
                new BranchCommand(
                    new FilterCondition<int>(VariableReference<int>.FromSource(number), new ComparisonFilter<int>(Comparison.GreaterThan, 3)),
                    new RaiseEventCommand(signal),
                    P("else-never")));

            Assert.AreEqual(5, number.Value);
            Assert.AreEqual(1, raised);
            Assert.IsFalse(log.Contains("start:else-never"));
        }

        [UnityTest]
        public IEnumerator EventListener_WithCondition_StartsACommandSequence()
        {
            var signal = ScriptableObject.CreateInstance<EventAsset>();
            var gate = ScriptableObject.CreateInstance<BoolVariableEvent>();
            created.Add(signal);
            created.Add(gate);
            sequence.SetCommands(new ICommand[] { P("opened") });

            var listenerObject = new GameObject("Listener");
            created.Add(listenerObject);
            listenerObject.SetActive(false);
            var listener = listenerObject.AddComponent<EventListener>();
            listener.Events.Add(signal);
            listener.Condition = new BoolCondition(VariableReference<bool>.FromSource(gate));
            listener.Responses.Add(new RunCommandSequenceResponse(sequence));
            listenerObject.SetActive(true);

            signal.Raise();
            yield return WaitIdle();
            Assert.IsFalse(log.Contains("start:opened"), "The Condition gates the Response.");

            gate.Set(true);
            signal.Raise();
            yield return WaitIdle();
            Assert.Contains("end:opened", log);
        }
    }
}

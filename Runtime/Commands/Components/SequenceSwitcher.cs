using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>
    /// Switches between Command Sequences so only the most recently requested one runs. Switching
    /// Stops the active Sequence, waits for it to clean up, runs its cleanup Sequence (if any), and
    /// then starts the requested one. Requests that arrive mid-switch collapse to the latest.
    /// </summary>
    [AddComponentMenu("RonJames Foundation/Sequence Switcher")]
    public class SequenceSwitcher : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            [HorizontalGroup("Row", Width = 0.3f)]
            [HideLabel]
            public string name;

            [HorizontalGroup("Row")]
            [HideLabel]
            public CommandSequence sequence;

            [Tooltip("Optional. Runs after the Sequence has Stopped, for cleanup.")]
            public CommandSequence cleanup;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        [SerializeField]
        [Tooltip("When on, requesting the entry that is already active Stops it and starts it again.")]
        private bool restartWhenRequestingActive = false;

        [ShowInInspector]
        [ReadOnly]
        private int activeIndex = -1;

        [ShowInInspector]
        [ReadOnly]
        private int requestedIndex = -1;

        private int requestVersion;
        private int handledVersion;
        private bool switching;

        /// <summary>The entries that can be switched between.</summary>
        public List<Entry> Entries => entries;

        /// <summary>The index of the running entry, or -1.</summary>
        public int ActiveIndex => activeIndex;

        /// <summary>True while a switch is in progress.</summary>
        public bool IsSwitching => switching;

        /// <summary>Requests the entry at <paramref name="index"/>. Safe to wire to UnityEvents.</summary>
        public void Request(int index)
        {
            if (index < 0 || index >= entries.Count)
            {
                Debug.LogWarning($"[RonJames Foundation] {name}: Sequence Switcher has no entry {index}.", this);
                return;
            }

            requestedIndex = index;
            requestVersion++;
            ProcessIfIdle();
        }

        /// <summary>Requests the entry with this name.</summary>
        public void Request(string entryName)
        {
            int index = entries.FindIndex(entry => entry.name == entryName);
            if (index < 0)
            {
                Debug.LogWarning($"[RonJames Foundation] {name}: Sequence Switcher has no entry named '{entryName}'.", this);
                return;
            }

            Request(index);
        }

        /// <summary>Stops the active entry (running its cleanup) and starts nothing.</summary>
        public void Clear()
        {
            requestedIndex = -1;
            requestVersion++;
            ProcessIfIdle();
        }

        private void ProcessIfIdle()
        {
            if (!switching)
            {
                ProcessRequests();
            }
        }

        private async void ProcessRequests()
        {
            switching = true;
            try
            {
                while (handledVersion != requestVersion)
                {
                    int target = requestedIndex;
                    int version = requestVersion;

                    if (target == activeIndex && target >= 0 && !restartWhenRequestingActive)
                    {
                        handledVersion = version;
                        continue;
                    }

                    await StopActive();

                    if (version != requestVersion)
                    {
                        continue;
                    }

                    if (target >= 0 && entries[target].sequence != null)
                    {
                        activeIndex = target;
                        entries[target].sequence.Run();
                    }

                    handledVersion = version;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                switching = false;
                if (handledVersion != requestVersion)
                {
                    ProcessIfIdle();
                }
            }
        }

        private async Awaitable StopActive()
        {
            if (activeIndex < 0 || activeIndex >= entries.Count)
            {
                activeIndex = -1;
                return;
            }

            Entry active = entries[activeIndex];
            activeIndex = -1;

            if (active.sequence != null)
            {
                await active.sequence.StopAndWaitAsync(destroyCancellationToken);
            }

            if (active.cleanup != null && active.cleanup.isActiveAndEnabled)
            {
                await active.cleanup.RunAndWaitAsync(destroyCancellationToken);
            }
        }
    }
}

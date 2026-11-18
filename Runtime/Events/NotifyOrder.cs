namespace RonJames.Foundation.Events
{
    /// <summary>The order an Event or Variable Event notifies its listeners in.</summary>
    public enum NotifyOrder
    {
        /// <summary>The order they subscribed in: the first to subscribe is notified first. The default.</summary>
        OldestFirst = 0,

        /// <summary>
        /// The reverse: the most recent subscriber is notified first. On a Variable Event, listeners that
        /// receive the value are all notified (newest first) before listeners that don't (newest first).
        /// For projects migrated from an event system that worked this way, where the last listener to run
        /// decides the outcome.
        /// </summary>
        NewestFirst = 1
    }
}

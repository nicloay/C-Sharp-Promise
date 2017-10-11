namespace RSG
{
    /// <summary>
    /// Specifies the state of a promise.
    /// </summary>
    public enum PromiseState
    {
        Pending,    // The promise is in-flight.
        Rejected,   // The promise has been rejected.
        Resolved,    // The promise has been resolved.
        Canceled    //The promise has been canceled.
    };
}
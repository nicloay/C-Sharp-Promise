namespace RSG.Promises
{
    public delegate void ActionResolve();
    
    /// <summary>
    /// Interface for a promise that can be rejected or resolved.
    /// </summary>
    public interface IPendingPromise : IRejectable
    {
        /// <summary>
        /// ID of the promise, useful for debugging.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Resolve the promise with a particular value.
        /// </summary>
        void Resolve();
    }
}
using System;

namespace RSG.Promises
{
    
    public delegate void Reject(Exception exception);
    
    /// <summary>
    /// Represents a handler invoked when the promise is rejected.
    /// </summary>
    public struct RejectHandler
    {
        /// <summary>
        /// Callback fn.
        /// </summary>
        public Reject callback;

        /// <summary>
        /// The promise that is rejected when there is an error while invoking the handler.
        /// </summary>
        public IRejectable rejectable;
    }
}
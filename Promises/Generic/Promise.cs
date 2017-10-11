using RSG.Promises;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RSG.Promises.Generic
{
    public delegate void Handler<T>(); 
    
    /// <summary>
    /// Implements a C# promise.
    /// https://developer.mozilla.org/en/docs/Web/JavaScript/Reference/Global_Objects/Promise
    /// </summary>
    public class Promise<T> : IPromise<T>, IPendingPromise<T>, IPromiseInfo, ICancelable
    {
        /// <summary>
        /// The exception when the promise is rejected.
        /// </summary>
        private Exception rejectionException;

        /// <summary>
        /// The value when the promises is resolved.
        /// </summary>
        private T resolveValue;

        /// <summary>
        /// Error handler.
        /// </summary>
        private List<RejectHandler> rejectHandlers;

        /// <summary>
        /// Completed handlers that accept a value.
        /// </summary>
        private List<Action<T>> resolveCallbacks;
        private List<IRejectable> resolveRejectables;

        /// <summary>
        /// handlerr called by Cancel method
        /// </summary>
        private ActionCancel cancelHandler;
        
        /// <summary>
        /// ID of the promise, useful for debugging.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Name of the promise, when set, useful for debugging.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Tracks the current state of the promise.
        /// </summary>
        public PromiseState CurState { get; private set; }

        public Promise(ActionCancel cancelHandler= null)
        {
            this.CurState = PromiseState.Pending;
            this.Id = Promise.NextId();
            this.cancelHandler = cancelHandler;
            
            if (Promise.EnablePromiseTracking)
            {
                Promise.pendingPromises.Add(this);
            }
        }

        public Promise(Action<Action<T>, Action<Exception>> resolver, ActionCancel cancelHandler = null)
        {
            this.CurState = PromiseState.Pending;
            this.Id = Promise.NextId();
            this.cancelHandler = cancelHandler;
            if (Promise.EnablePromiseTracking)
            {
                Promise.pendingPromises.Add(this);
            }

            try
            {
                resolver(
                    // Resolve
                    value => Resolve(value),

                    // Reject
                    ex => Reject(ex)
                );
            }
            catch (Exception ex)
            {
                Reject(ex);
            }
        }

        /// <summary>
        /// Add a rejection handler for this promise.
        /// </summary>
        private void AddRejectHandler(ActionReject onRejected, IRejectable rejectable)
        {
            if (rejectHandlers == null)
            {
                rejectHandlers = new List<RejectHandler>();
            }

            rejectHandlers.Add(new RejectHandler() { callback = onRejected, rejectable = rejectable }); ;
        }

        /// <summary>
        /// Add a resolve handler for this promise.
        /// </summary>
        private void AddResolveHandler(Action<T> onResolved, IRejectable rejectable)
        {
            if (resolveCallbacks == null)
            {
                resolveCallbacks = new List<Action<T>>();
            }

            if (resolveRejectables == null)
            {
                resolveRejectables = new List<IRejectable>();
            }

            resolveCallbacks.Add(onResolved);
            resolveRejectables.Add(rejectable);
        }

        /// <summary>
        /// Invoke a single handler.
        /// </summary>
        private void InvokeHandler<T>(Action<T> callback, IRejectable rejectable, T value)
        {
            try
            {
                callback(value);
            }
            catch (Exception ex)
            {
                rejectable.Reject(ex);
            }
        }

        /// <summary>
        /// Invoke a Reject single handler.
        /// </summary>
        private void InvokeRejectHandler(ActionReject callback, IRejectable rejectable, Exception exception)
        {
            try
            {
                callback(exception);
            }
            catch (Exception ex)
            {
                rejectable.Reject(ex);
            }
        }

        
        
        
        /// <summary>
        /// Helper function clear out all handlers after resolution or rejection.
        /// </summary>
        private void ClearHandlers()
        {
            rejectHandlers = null;
            resolveCallbacks = null;
            resolveRejectables = null;
            cancelHandler = null;
        }

        /// <summary>
        /// Invoke all reject handlers.
        /// </summary>
        private void InvokeRejectHandlers(Exception ex)
        {
            if (rejectHandlers != null)
            {
                rejectHandlers.Each(handler => InvokeRejectHandler(handler.callback, handler.rejectable, ex));
            }
            ClearHandlers();
        }

        /// <summary>
        /// Invoke all resolve handlers.
        /// </summary>
        private void InvokeResolveHandlers(T value)
        {
            if (resolveCallbacks != null)
            {
                for (int i = 0, maxI = resolveCallbacks.Count; i < maxI; i++) {
                    InvokeHandler(resolveCallbacks[i], resolveRejectables[i], value);
                }
            }

            ClearHandlers();
        }

        /// <summary>
        /// Reject the promise with an exception.
        /// </summary>
        public void Reject(Exception ex)
        {
            if (CurState != PromiseState.Pending)
            {
                throw new ApplicationException("Attempt to reject a promise that is already in state: " + CurState + ", a promise can only be rejected when it is still in state: " + PromiseState.Pending);
            }

            rejectionException = ex;
            CurState = PromiseState.Rejected;

            if (Promise.EnablePromiseTracking)
            {
                Promise.pendingPromises.Remove(this);
            }

            InvokeRejectHandlers(ex);
        }

        /// <summary>
        /// Resolve the promise with a particular value.
        /// </summary>
        public void Resolve(T value)
        {
            if (CurState != PromiseState.Pending)
            {
                throw new ApplicationException("Attempt to resolve a promise that is already in state: " + CurState + ", a promise can only be resolved when it is still in state: " + PromiseState.Pending);
            }

            resolveValue = value;
            CurState = PromiseState.Resolved;

            if (Promise.EnablePromiseTracking)
            {
                Promise.pendingPromises.Remove(this);
            }

            InvokeResolveHandlers(value);
        }

        /// <summary>
        /// Completes the promise. 
        /// onResolved is called on successful completion.
        /// onRejected is called on error.
        /// </summary>
        public void Done(Action<T> onResolved, Action<Exception> onRejected)
        {
            Then(onResolved, onRejected)
                .Catch(ex =>
                    Promise.PropagateUnhandledException(this, ex)
                );
        }

        /// <summary>
        /// Completes the promise. 
        /// onResolved is called on successful completion.
        /// Adds a default error handler.
        /// </summary>
        public void Done(Action<T> onResolved)
        {
            Then(onResolved)
                .Catch(ex =>
                    Promise.PropagateUnhandledException(this, ex)
                );
        }

        /// <summary>
        /// Complete the promise. Adds a default error handler.
        /// </summary>
        public void Done()
        {
            Catch(ex =>
                Promise.PropagateUnhandledException(this, ex)
            );
        }

        /// <summary>
        /// Set the name of the promise, useful for debugging.
        /// </summary>
        public IPromise<T> WithName(string name)
        {
            this.Name = name;
            return this;
        }

        /// <summary>
        /// Handle errors for the promise. 
        /// </summary>
        public IPromise<T> Catch(Action<Exception> onRejected)
        {
//            Argument.NotNull(() => onRejected);

            var resultPromise = new Promise<T>();
            resultPromise.WithName(Name);

            Action<T> resolveHandler = v =>
            {
                resultPromise.Resolve(v);
            };

            ActionReject rejectHandler = ex =>
            {
                onRejected(ex);

                resultPromise.Reject(ex);
            };

            ActionHandlers(resultPromise, resolveHandler, rejectHandler);

            return resultPromise;
        }

        /// <summary>
        /// Add a resolved callback that chains a value promise (optionally converting to a different value type).
        /// </summary>
        public IPromise<ConvertedT> Then<ConvertedT>(Func<T, IPromise<ConvertedT>> onResolved)
        {
            return Then(onResolved, null);
        }

        /// <summary>
        /// Add a resolved callback that chains a non-value promise.
        /// </summary>
        public IPromise Then(Func<T, IPromise> onResolved)
        {
            return Then(onResolved, null);
        }

        /// <summary>
        /// Add a resolved callback.
        /// </summary>
        public IPromise<T> Then(Action<T> onResolved)
        {
            return Then(onResolved, null);
        }

        /// <summary>
        /// Add a resolved callback and a rejected callback.
        /// The resolved callback chains a value promise (optionally converting to a different value type).
        /// </summary>
        public IPromise<ConvertedT> Then<ConvertedT>(Func<T, IPromise<ConvertedT>> onResolved, Action<Exception> onRejected)
        {
            // This version of the function must supply an onResolved.
            // Otherwise there is now way to get the converted value to pass to the resulting promise.
//            Argument.NotNull(() => onResolved); 

            var resultPromise = new Promise<ConvertedT>();
            resultPromise.WithName(Name);

            Action<T> resolveHandler = v =>
            {
                onResolved(v)
                    .Then(
                        // Should not be necessary to specify the arg type on the next line, but Unity (mono) has an internal compiler error otherwise.
                        (ConvertedT chainedValue) => resultPromise.Resolve(chainedValue),
                        ex => resultPromise.Reject(ex)
                    );
            };

            ActionReject rejectHandler = ex =>
            {
                if (onRejected != null)
                {
                    onRejected(ex);
                }

                resultPromise.Reject(ex);
            };

            ActionHandlers(resultPromise, resolveHandler, rejectHandler);

            return resultPromise;
        }

        /// <summary>
        /// Add a resolved callback and a rejected callback.
        /// The resolved callback chains a non-value promise.
        /// </summary>
        public IPromise Then(Func<T, IPromise> onResolved, Action<Exception> onRejected)
        {
            var resultPromise = new Promise();
            resultPromise.WithName(Name);

            Action<T> resolveHandler = v =>
            {
                if (onResolved != null)
                {
                    onResolved(v)
                        .Then(
                            () => resultPromise.Resolve(),
                            ex => resultPromise.Reject(ex)
                        );
                }
                else
                {
                    resultPromise.Resolve();
                }
            };

            ActionReject rejectHandler = ex =>
            {
                if (onRejected != null)
                {
                    onRejected(ex);
                }

                resultPromise.Reject(ex);
            };

            ActionHandlers(resultPromise, resolveHandler, rejectHandler);

            return resultPromise;
        }

        /// <summary>
        /// Add a resolved callback and a rejected callback.
        /// </summary>
        public IPromise<T> Then(Action<T> onResolved, Action<Exception> onRejected)
        {
            var resultPromise = new Promise<T>();
            resultPromise.WithName(Name);

            Action<T> resolveHandler = v =>
            {
                if (onResolved != null)
                {
                    onResolved(v);
                }

                resultPromise.Resolve(v);
            };

            ActionReject rejectHandler = ex =>
            {
                if (onRejected != null)
                {
                    onRejected(ex);
                }

                resultPromise.Reject(ex);
            };

            ActionHandlers(resultPromise, resolveHandler, rejectHandler);

            return resultPromise;
        }

        /// <summary>
        /// Return a new promise with a different value.
        /// May also change the type of the value.
        /// </summary>
        public IPromise<ConvertedT> Then<ConvertedT>(Func<T, ConvertedT> transform)
        {
//            Argument.NotNull(() => transform);
            return Then(value => Promise<ConvertedT>.Resolved(transform(value)));
        }

        /// <summary>
        /// Return a new promise with a different value.
        /// May also change the type of the value.
        /// </summary>
        [Obsolete("Use Then instead")]
        public IPromise<ConvertedT> Transform<ConvertedT>(Func<T, ConvertedT> transform)
        {
//            Argument.NotNull(() => transform);
            return Then(value => Promise<ConvertedT>.Resolved(transform(value)));
        }

        /// <summary>
        /// Helper function to invoke or register resolve/reject handlers.
        /// </summary>
        private void ActionHandlers(IRejectable resultPromise, Action<T> resolveHandler, ActionReject rejectHandler)
        {
            if (CurState == PromiseState.Resolved)
            {
                InvokeHandler(resolveHandler, resultPromise, resolveValue);
            }
            else if (CurState == PromiseState.Rejected)
            {
                InvokeRejectHandler(rejectHandler, resultPromise, rejectionException);
            }
            else
            {
                AddResolveHandler(resolveHandler, resultPromise);
                AddRejectHandler(rejectHandler, resultPromise);
            }
        }

        /// <summary>
        /// Chain an enumerable of promises, all of which must resolve.
        /// Returns a promise for a collection of the resolved results.
        /// The resulting promise is resolved when all of the promises have resolved.
        /// It is rejected as soon as any of the promises have been rejected.
        /// </summary>
        public IPromise<IEnumerable<ConvertedT>> ThenAll<ConvertedT>(Func<T, IEnumerable<IPromise<ConvertedT>>> chain)
        {
            return Then(value => Promise<ConvertedT>.All(chain(value)));
        }

        /// <summary>
        /// Chain an enumerable of promises, all of which must resolve.
        /// Converts to a non-value promise.
        /// The resulting promise is resolved when all of the promises have resolved.
        /// It is rejected as soon as any of the promises have been rejected.
        /// </summary>
        public IPromise ThenAll(Func<T, IEnumerable<IPromise>> chain)
        {
            return Then(value => Promise.All(chain(value)));
        }

        /// <summary>
        /// Returns a promise that resolves when all of the promises in the enumerable argument have resolved.
        /// Returns a promise of a collection of the resolved results.
        /// </summary>
        public static IPromise<IEnumerable<T>> All(params IPromise<T>[] promises)
        {
            return All((IEnumerable<IPromise<T>>)promises); // Cast is required to force use of the other All function.
        }

        /// <summary>
        /// Returns a promise that resolves when all of the promises in the enumerable argument have resolved.
        /// Returns a promise of a collection of the resolved results.
        /// </summary>
        public static IPromise<IEnumerable<T>> All(IEnumerable<IPromise<T>> promises)
        {
            var promisesArray = promises.ToArray();
            if (promisesArray.Length == 0)
            {
                return Promise<IEnumerable<T>>.Resolved(EnumerableExt.Empty<T>());
            }

            var remainingCount = promisesArray.Length;
            var results = new T[remainingCount];
            var resultPromise = new Promise<IEnumerable<T>>();
            resultPromise.WithName("All");

            promisesArray.Each((promise, index) =>
            {
                promise
                    .Catch(ex =>
                    {
                        if (resultPromise.CurState == PromiseState.Pending)
                        {
                            // If a promise errorred and the result promise is still pending, reject it.
                            resultPromise.Reject(ex);
                        }
                    })
                    .Then(result =>
                    {
                        results[index] = result;

                        --remainingCount;
                        if (remainingCount <= 0)
                        {
                            // This will never happen if any of the promises errorred.
                            resultPromise.Resolve(results);
                        }
                    })
                    .Done();
            });

            return resultPromise;
        }

        /// <summary>
        /// Takes a function that yields an enumerable of promises.
        /// Returns a promise that resolves when the first of the promises has resolved.
        /// Yields the value from the first promise that has resolved.
        /// </summary>
        public IPromise<ConvertedT> ThenRace<ConvertedT>(Func<T, IEnumerable<IPromise<ConvertedT>>> chain)
        {
            return Then(value => Promise<ConvertedT>.Race(chain(value)));
        }

        /// <summary>
        /// Takes a function that yields an enumerable of promises.
        /// Converts to a non-value promise.
        /// Returns a promise that resolves when the first of the promises has resolved.
        /// Yields the value from the first promise that has resolved.
        /// </summary>
        public IPromise ThenRace(Func<T, IEnumerable<IPromise>> chain)
        {
            return Then(value => Promise.Race(chain(value)));
        }

        /// <summary>
        /// Returns a promise that resolves when the first of the promises in the enumerable argument have resolved.
        /// Returns the value from the first promise that has resolved.
        /// </summary>
        public static IPromise<T> Race(params IPromise<T>[] promises)
        {
            return Race((IEnumerable<IPromise<T>>)promises); // Cast is required to force use of the other function.
        }

        /// <summary>
        /// Returns a promise that resolves when the first of the promises in the enumerable argument have resolved.
        /// Returns the value from the first promise that has resolved.
        /// </summary>
        public static IPromise<T> Race(IEnumerable<IPromise<T>> promises, bool cancelRemaining = false)
        {
            var promisesArray = promises.ToArray();
            if (promisesArray.Length == 0)
            {
                throw new ApplicationException("At least 1 input promise must be provided for Race");
            }

            var resultPromise = new Promise<T>();
            resultPromise.WithName("Race");

            promisesArray.Each((promise, index) =>
            {
                promise
                    .Catch(ex =>
                    {
                        if (resultPromise.CurState == PromiseState.Pending)
                        {
                            // If a promise errorred and the result promise is still pending, reject it.
                            resultPromise.Reject(ex);
                        }
                    })
                    .Then(result =>
                    {
                        if (resultPromise.CurState == PromiseState.Pending)
                        {                            
                            foreach (var promise1 in promisesArray)
                            {
                                if (promise1 != resultPromise && promise1 is ICancelable)
                                {
                                    (promise1 as ICancelable).Cancel();
                                }
                            }
                            resultPromise.Resolve(result);
                        }
                    })
                    .Done();
            });

            return resultPromise;
        }

        /// <summary>
        /// Convert a simple value directly into a resolved promise.
        /// </summary>
        public static IPromise<T> Resolved(T promisedValue)
        {
            var promise = new Promise<T>();
            promise.Resolve(promisedValue);
            return promise;
        }

        /// <summary>
        /// Convert an exception directly into a rejected promise.
        /// </summary>
        public static IPromise<T> Rejected(Exception ex)
        {
            var promise = new Promise<T>();
            promise.Reject(ex);
            return promise;
        }

        public IPromise Finally(Action onComplete)
        {
            Promise promise = new Promise();
            promise.WithName(Name);

            this.Then((x) => { promise.Resolve(); });
            this.Catch((e) => { promise.Resolve(); });

            return promise.Then(onComplete);
        }

        public IPromise Finally(Func<IPromise> onComplete)
        {
            Promise promise = new Promise();
            promise.WithName(Name);

            this.Then((x) => { promise.Resolve(); });
            this.Catch((e) => { promise.Resolve(); });

            return promise.Then(onComplete);
        }

        public IPromise<ConvertedT> Finally<ConvertedT>(Func<IPromise<ConvertedT>> onComplete)
        {
            Promise promise = new Promise();
            promise.WithName(Name);

            this.Then((x) => { promise.Resolve(); });
            this.Catch((e) => { promise.Resolve(); });

            return promise.Then(onComplete);
        }

        public void Cancel()
        {
            if (cancelHandler != null)
            {
                cancelHandler();
            }
            ClearHandlers();
        }

        public void AddOnCancel(ActionCancel cancelHandler)
        {
            this.cancelHandler = cancelHandler;
        }
    }
}
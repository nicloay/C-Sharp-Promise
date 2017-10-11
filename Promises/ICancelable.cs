using System;

namespace RSG.Promises
{
    public delegate void ActionCancel();
    
    public interface ICancelable
    {
        void Cancel();
        void AddOnCancel(ActionCancel cancelHandler);
    }
}
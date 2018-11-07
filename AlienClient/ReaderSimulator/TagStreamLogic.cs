using System;

namespace AlienClient.ReaderSimulator
{
    public class TagStreamLogic : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            return null;
        }
    }
}
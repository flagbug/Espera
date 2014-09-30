using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace Espera.Core
{
    /// <summary>
    /// A semaphore, that holds a one-time lock on a specific key. It also memoizes the keys, so
    /// once released keys aren't locked when waiting on them the next time.
    /// </summary>
    public class KeyedMemoizingSemaphore
    {
        private readonly Dictionary<string, AsyncSubject<Unit>> keyedSemaphore;

        public KeyedMemoizingSemaphore()
        {
            this.keyedSemaphore = new Dictionary<string, AsyncSubject<Unit>>();
        }

        public void Release(string key)
        {
            lock (this.keyedSemaphore)
            {
                AsyncSubject<Unit> semaphore = keyedSemaphore[key];

                semaphore.OnNext(Unit.Default);
                semaphore.OnCompleted();
            }
        }

        public Task Wait(string key)
        {
            lock (this.keyedSemaphore)
            {
                AsyncSubject<Unit> semaphore;

                if (this.keyedSemaphore.TryGetValue(key, out semaphore))
                {
                    return semaphore.ToTask();
                }

                semaphore = new AsyncSubject<Unit>();
                this.keyedSemaphore.Add(key, semaphore);

                return Task.Delay(0);
            }
        }
    }
}
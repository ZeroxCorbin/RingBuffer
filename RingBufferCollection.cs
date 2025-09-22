using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;

namespace RingBuffer.lib
{
    public class RingBufferCollection<T> : ObservableCollection<T>, INotifyPropertyChanged
    {
        private int head = 0;
        private int tail = -1;
        private int count = 0;
        private readonly object syncRoot = new object();

        public int Head
        {
            get => head;
            private set
            {
                if (head != value)
                {
                    head = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(Head)));
                }
            }
        }

        public int Tail
        {
            get => tail;
            private set
            {
                if (tail != value)
                {
                    tail = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(Tail)));
                }
            }
        }

        public new int Count
        {
            get => count;
            private set
            {
                if (count != value)
                {
                    count = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                }
            }
        }

        public int Capacity { get; }

        // Do NOT rely on constructor-time SynchronizationContext (may be null)
        private static SynchronizationContext uiContext;

        public RingBufferCollection(int capacity)
        {
            Capacity = capacity;
            uiContext = SynchronizationContext.Current ?? uiContext;
        }

        private static SynchronizationContext UIContext
        {
            get
            {
                if (uiContext == null)
                    uiContext = SynchronizationContext.Current;
                return uiContext;
            }
        }

        public new void Add(T item)
        {
            lock (syncRoot)
            {
                if (Count < Capacity)
                {
                    Tail = (Tail + 1) % Capacity;

                    if (Tail < Count)
                    {
                        // Overwriting existing slot
                        this[Tail] = item;
                    }
                    else
                    {
                        InsertOnUIThread(Tail, item);
                    }

                    Count++;
                }
                else
                {
                    // Overwrite head (ring move)
                    this[Head] = item;
                    Head = (Head + 1) % Capacity;
                    Tail = (Tail + 1) % Capacity;
                }

                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                RaiseReset();
            }
        }

        public T Remove()
        {
            lock (syncRoot)
            {
                if (Count == 0)
                    throw new InvalidOperationException("The buffer is empty.");

                var item = this[Head];
                RemoveOnUIThread(Head);
                Count--;
                Head = (Head + 1) % Capacity;

                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                RaiseReset();
                return item;
            }
        }

        public T GetHead() =>
            Count == 0 ? throw new InvalidOperationException("The buffer is empty.") : this[Head];

        public T GetTail() =>
            Count == 0 ? throw new InvalidOperationException("The buffer is empty.") : this[Tail];

        protected override void SetItem(int index, T item)
        {
            base.SetItem(index, item);
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            RaiseReset();
        }

        public new void Clear()
        {
            lock (syncRoot)
            {
                base.Clear();
                Head = 0;
                Tail = -1;
                Count = 0;
                RaiseReset();
            }
        }

        private void InsertOnUIThread(int index, T item)
        {
            var ctx = UIContext;
            if (ctx != null)
            {
                if (ctx == SynchronizationContext.Current)
                {
                    base.InsertItem(index, item);
                }
                else
                {
                    ctx.Send(_ => base.InsertItem(index, item), null);
                }
            }
            else
            {
                // Fallback: insert directly (may be non-UI thread; caller should marshal if needed)
                base.InsertItem(index, item);
            }
        }

        private void RemoveOnUIThread(int index)
        {
            var ctx = UIContext;
            if (ctx != null)
            {
                if (ctx == SynchronizationContext.Current)
                {
                    base.RemoveAt(index);
                }
                else
                {
                    ctx.Send(_ => base.RemoveAt(index), null);
                }
            }
            else
            {
                base.RemoveAt(index);
            }
        }

        private void RaiseReset()
        {
            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            var ctx = UIContext;
            if (ctx != null && ctx != SynchronizationContext.Current)
                ctx.Post(_ => OnCollectionChanged(args), null);
            else
                OnCollectionChanged(args);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e) => base.OnPropertyChanged(e);
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) => base.OnCollectionChanged(e);
    }
}

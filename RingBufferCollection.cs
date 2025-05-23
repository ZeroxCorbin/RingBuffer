﻿using System;
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
        private int count = 0; // Track the actual number of items in the buffer
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

        private static SynchronizationContext uiContext;
        public RingBufferCollection(int capacity)
        {
            uiContext = SynchronizationContext.Current;
            Capacity = capacity;
        }

        public new void Add(T item)
        {
            lock (syncRoot)
            {
                if (Count < Capacity)
                {
                    Tail = (Tail + 1) % Capacity;
                    if (Tail < Count)
                        this[Tail] = item;
                    else
                        uiContext?.Send(_ => base.InsertItem(Tail, item), null);
                    Count++;
                }
                else
                {
                    this[Head] = item;
                    Head = (Head + 1) % Capacity;
                    Tail = (Tail + 1) % Capacity;
                }

                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                uiContext?.Send(_ => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)), null);
            }
        }

        public T Remove()
        {
            lock (syncRoot)
            {
                if (Count == 0)
                    throw new InvalidOperationException("The buffer is empty.");


                T item = this[Head];
                uiContext?.Send(_ => base.RemoveAt(Head), null);
                // Adjust for the shifted head position
                Count--;
                Head = (Head + 1) % Capacity;

                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                uiContext?.Send(_ => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)), null);
                return item;
            }
        }

        public T GetHead() => Count == 0 ? throw new InvalidOperationException("The buffer is empty.") : this[Head];
        public T GetTail() => Count == 0 ? throw new InvalidOperationException("The buffer is empty.") : this[Tail];

        protected override void SetItem(int index, T item)
        {
            lock (syncRoot)
            {
                uiContext?.Send(_ => base.SetItem(index, item), null);
            }
        }

        public new void Clear()
        {
            lock (syncRoot)
            {
                base.Clear();
                Head = 0;
                Tail = -1;
                Count = 0;
            }
        }



        protected override void OnPropertyChanged(PropertyChangedEventArgs e) => base.OnPropertyChanged(e);

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) => base.OnCollectionChanged(e);
    }
}

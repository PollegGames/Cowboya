using System;
using System.Collections.Generic;
using UnityEngine;

public class PriorityQueue<T>
{
    private readonly List<T> elements = new List<T>();
    private readonly Comparison<T> comparator;
    public int Count => elements.Count;

    public PriorityQueue(Comparison<T> comparison) 
        => comparator = comparison;

    public void Enqueue(T item)
    {
        elements.Add(item);
        elements.Sort(comparator);
    }

    public T Dequeue()
    {
        if (elements.Count == 0) return default;
        T item = elements[0];
        elements.RemoveAt(0);
        return item;
    }

    public bool Contains(T item) => elements.Contains(item);
}
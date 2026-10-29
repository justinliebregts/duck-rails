using System.Collections.Generic;
using Photon.Pun;

/// <summary>Retains immutable boxes for unchanged stream slots; never mutates data held by PUN.</summary>
public sealed class PhotonValueCache
{
    readonly object[] slots;
    public PhotonValueCache(int capacity = 192) => slots = new object[capacity];
    int cursor;
    public long Reused { get; private set; }
    public long Changed { get; private set; }
    public void Begin() => cursor = 0;
    public void Send<T>(PhotonStream stream, T value)
    {
        int index = cursor++;
        object boxed;
        if (index < slots.Length && slots[index] is T previous
            && EqualityComparer<T>.Default.Equals(previous, value))
        { boxed = slots[index]; Reused++; }
        else
        {
            boxed = value;
            if (index < slots.Length) slots[index] = boxed;
            Changed++;
        }
        stream.SendNext(boxed);
    }
}

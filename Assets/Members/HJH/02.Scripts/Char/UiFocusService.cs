using System.Collections.Generic;
using UnityEngine;

public static class UiFocusService
{
    private static readonly HashSet<Object> Requesters = new HashSet<Object>();

    public static event System.Action<bool> OnUiFocusChanged;

    public static bool IsUiFocused
    {
        get
        {
            Prune();
            return Requesters.Count > 0;
        }
    }

    public static void Acquire(Object requester)
    {
        if (requester == null)
            return;

        bool was = IsUiFocused;
        Requesters.Add(requester);

        if (!was && Requesters.Count > 0)
            Apply(true);
    }

    public static void Release(Object requester)
    {
        if (requester == null)
            return;

        bool was = IsUiFocused;
        Requesters.Remove(requester);

        if (was && Requesters.Count == 0)
            Apply(false);
    }

    public static void ReleaseAll()
    {
        bool was = IsUiFocused;
        Requesters.Clear();

        if (was)
            Apply(false);
    }

    private static void Prune()
    {
        Requesters.RemoveWhere(o => o == null);
    }

    private static void Apply(bool focused)
    {
        Cursor.lockState = focused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = focused;
        OnUiFocusChanged?.Invoke(focused);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Requesters.Clear();
        OnUiFocusChanged = null;
    }
}

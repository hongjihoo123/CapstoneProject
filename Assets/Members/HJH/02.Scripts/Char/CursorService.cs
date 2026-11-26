using System.Collections.Generic;
using UnityEngine;

public static class CursorService
{
    private static readonly HashSet<Object> Hiders = new HashSet<Object>();

    public static bool IsHidden { get; private set; }

    public static void SetHidden(Object requester, bool hidden)
    {
        if (requester == null)
            return;

        if (hidden)
            Hiders.Add(requester);
        else
            Hiders.Remove(requester);

        Refresh();
    }

    public static void Refresh()
    {
        Hiders.RemoveWhere(o => o == null);
        bool hidden = Hiders.Count > 0 && !UiFocusService.IsUiFocused;
        IsHidden = hidden;
        Cursor.visible = !hidden;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Hiders.Clear();
        IsHidden = false;
    }
}

namespace VaultProspector.App;

public static class TrayStatusPolicy
{
    public static string Describe(
        bool networkAvailable,
        bool isBusy,
        bool hasActionableError,
        bool isUnlocked,
        bool azureInteractionRequired)
    {
        if (!isUnlocked)
        {
            if (isBusy)
                return "Locked — syncing metadata";
            if (hasActionableError)
                return "Locked — action required";
            if (!networkAvailable)
                return "Locked — offline";
            return "Locked";
        }

        if (!networkAvailable)
            return "Offline";
        if (isBusy)
            return "Syncing metadata";
        if (hasActionableError)
            return "Action required";
        if (azureInteractionRequired)
            return "Azure interaction required";
        return "Ready";
    }
}

// This script is static, meaning it lives in memory throughout the entire game session.
// You do NOT attach this to a GameObject.
public static class TimeTransfer
{
    public static float SavedTime;
    public static int SavedDay;
    public static bool HasData = false; // Checks if we have saved data before
}
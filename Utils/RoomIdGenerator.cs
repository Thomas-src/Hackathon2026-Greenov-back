namespace Backend.Utils;

public static class RoomIdGenerator
{
    public static string Generate()
    {
        return Guid.NewGuid().ToString("N")[..6].ToUpper();
    }
}
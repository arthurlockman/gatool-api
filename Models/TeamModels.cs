namespace GAToolAPI.Models;

public class TeamAvatar
{
    public string? EncodedAvatar { get; set; }
    public int TeamNumber { get; set; }
}

public class TeamAvatarResponse
{
    public List<TeamAvatar> Teams { get; set; } = new();
}
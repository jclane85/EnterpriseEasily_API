namespace EnterpriseEasily.API.Data.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Auth0Sub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserFavorite> Favorites { get; set; } = new List<UserFavorite>();
}

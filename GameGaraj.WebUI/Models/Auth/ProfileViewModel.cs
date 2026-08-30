namespace GameGaraj.WebUI.Models.Auth
{
    public class ProfileViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName => $"{Name} {Surname}".Trim();
        public List<string> Roles { get; set; } = new();
        public int OrderCount { get; set; }
        public int ReviewCount { get; set; }
        public int AddressCount { get; set; }
        public int FavoriteCount { get; set; }
    }
}

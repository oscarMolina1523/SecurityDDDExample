namespace DDDExample.Infrastructure.Configuration
{
    public class RefreshTokenSettings
    {
        public int RefreshTokenExpirationDays { get; set; } = 30;
        public bool RotationEnabled { get; set; } = true;
        public int MaxActiveTokens { get; set; } = 5;
        public int CleanupIntervalHours { get; set; } = 24;
    }
}

namespace DDDExample.Infrastructure.Configuration { 
    public class MfaSettings
    {
        public string Issuer { get; set; } = "DDDExample";
        public int Digits { get; set; } = 6;
        public int Period { get; set; } = 30;
        public int Window { get; set; } = 1;
        public int BackupCodesCount { get; set; } = 10;
        public int RememberDeviceDays { get; set; } = 30;
    }
}

namespace text_survival_rpg_web
{
    public static class Config
    {
        public enum IOType
        {
            Console,
            Web
        }
        public static IOType io = IOType.Web;
        public const string WebApiUrl = "https://localhost:4321/api/TextSurvival";
    }
}

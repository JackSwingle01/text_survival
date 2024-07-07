namespace text_survival.Interfaces
{
    public interface IClonable<T>
    {
        public delegate T CloneDelegate();
        public CloneDelegate Clone { get; set; }
    }
}

namespace text_survival
{
    public interface ICommand
    {
        public string Name { get; set; }
        public void Execute();
    }
    // public class Command : ICommand
    // {
    //     public string Name { get; set; }
    //     public Action Act { get; set; }
    //     public Command(string name, Action act)
    //     {
    //         Name = name;
    //         Act = act;
    //     }
    //     // public Command(Action act)
    //     // {
    //     //     Act = act;
    //     // }
    //     public void Execute()
    //     {
    //         Act.Invoke();
    //     }
    // }

    public class Command<TPlayer> : ICommand
    {
        public string Name { get; set; }
        public Action<TPlayer> Act { get; set; }
        public TPlayer? Player { get; set; }

        public Command(string name, Action<TPlayer> act)
        {
            Name = name;
            Act = act;
        }

        public void Execute()
        {
            if (Player == null)
            {
                throw new Exception("Player is null");
            }
            Act.Invoke(Player);
        }

        public override string ToString() => Name;

    }

    //public class Command<TPlayer, T> : ICommand
    //{
    //    public string Name { get; set; }
    //    public Action<TPlayer, T> Act { get; set; }
    //    public TPlayer? Player { get; set; }
    //    public T Arg { get; set; }

    //    public Command(string name, Action<TPlayer, T> act)
    //    {
    //        Name = name;
    //        Act = act;
    //    }

    //    public void Execute()
    //    {
    //        Act.Invoke(Player, Arg);
    //    }
    //}

}

namespace text_survival.Items;

public class ItemStack
{
    public string DisplayName => Items.Count == 1 ? FirstItem.Name : $"{FirstItem.Name} x{Items.Count}";
    public Item FirstItem { get; private set; }
    public Stack<Item> Items { get; private set; }
    public int Count => Items.Count;
    
    public ItemStack(Item item)
    {
        FirstItem = item;
        Items = new Stack<Item>();
        Items.Push(item);
    }
    
    public void Add(Item item)
    {
        if (item.Name != FirstItem.Name)
        {
            throw new ArgumentException($"Cannot add item '{item.Name}' to stack of '{FirstItem.Name}'");
        }
        
        Items.Push(item);
    }
    
    public Item Take() => Items.Pop();
    
    public override string ToString() => DisplayName;
    
    public static List<ItemStack> CreateStacksFromItems(IEnumerable<Item> items)
    {
        var stacksByName = new Dictionary<string, ItemStack>();
        
        foreach (var item in items)
        {
            if (stacksByName.TryGetValue(item.Name, out var stack))
            {
                stack.Add(item);
            }
            else
            {
                stacksByName[item.Name] = new ItemStack(item);
            }
        }
        
        return stacksByName.Values.ToList();
    }
}
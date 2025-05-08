namespace Game
{
    public class InventorySlot
    {
        public string Id;
        public int Count;

        public InventorySlot(string id = null, int count = 0)
        {
            Id = id;
            Count = count;
        }
    }
}

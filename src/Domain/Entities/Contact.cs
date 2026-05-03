namespace src.Domain.Entities;

public class Contact
{
    public int Column { get; }
    public ContactPosition Position { get; }
    public int NetId { get; }

    public Contact(int column, ContactPosition position, int netId)
    {
        if (column < 0) throw new ArgumentException("Column must be non-negative", nameof(column));
        if (netId <= 0) throw new ArgumentException("Net ID must be positive", nameof(netId));

        Column = column;
        Position = position;
        NetId = netId;
    }
}

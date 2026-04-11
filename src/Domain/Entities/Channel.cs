namespace src.Domain.Entities;

/// <summary>
/// Represents a routing channel with top and bottom contact rows
/// </summary>
public class Channel
{
    public int Width { get; private set; }
    public int[] TopRow { get; private set; }
    public int[] BottomRow { get; private set; }
    public Dictionary<int, Net> Nets { get; private set; }

    public Channel(int width, int[] topRow, int[] bottomRow)
    {
        if (width <= 0)
            throw new ArgumentException("Channel width must be positive", nameof(width));

        if (topRow.Length != width || bottomRow.Length != width)
            throw new ArgumentException("Row lengths must match channel width");

        Width = width;
        TopRow = topRow;
        BottomRow = bottomRow;
        Nets = new Dictionary<int, Net>();

        InitializeNets();
    }

    private void InitializeNets()
    {
        var netIds = TopRow.Concat(BottomRow).Where(id => id != 0).Distinct();

        foreach (var netId in netIds)
        {
            var contacts = new List<Contact>();

            for (int col = 0; col < Width; col++)
            {
                if (TopRow[col] == netId)
                    contacts.Add(new Contact(col, ContactPosition.Top, netId));
                if (BottomRow[col] == netId)
                    contacts.Add(new Contact(col, ContactPosition.Bottom, netId));
            }

            Nets[netId] = new Net(netId, contacts);
        }
    }

    public int GetMaxNetId() => Nets.Keys.DefaultIfEmpty(0).Max();
}
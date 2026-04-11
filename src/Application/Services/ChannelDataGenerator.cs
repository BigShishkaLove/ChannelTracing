using src.Domain.Entities;

namespace src.Application.Services;

/// <summary>
/// Factory for generating test channel data
/// </summary>
public class ChannelDataGenerator
{
    private readonly Random _random;

    public ChannelDataGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Generates a simple channel with two-terminal nets (one top, one bottom contact per net)
    /// </summary>
    public Channel GenerateSimpleChannel(int width, int netCount)
    {
        if (width < netCount * 2)
            throw new ArgumentException("Channel too narrow for the number of nets");

        var topRow = new int[width];
        var bottomRow = new int[width];

        var availableTopPositions = Enumerable.Range(0, width).ToList();
        var availableBottomPositions = Enumerable.Range(0, width).ToList();

        for (int netId = 1; netId <= netCount; netId++)
        {
            // Assign random top position
            int topIndex = _random.Next(availableTopPositions.Count);
            int topCol = availableTopPositions[topIndex];
            availableTopPositions.RemoveAt(topIndex);
            topRow[topCol] = netId;

            // Assign random bottom position
            int bottomIndex = _random.Next(availableBottomPositions.Count);
            int bottomCol = availableBottomPositions[bottomIndex];
            availableBottomPositions.RemoveAt(bottomIndex);
            bottomRow[bottomCol] = netId;
        }

        return new Channel(width, topRow, bottomRow);
    }

    /// <summary>
    /// Generates a channel with potential conflicts (cycles)
    /// Creates pairs of nets that cross each other
    /// </summary>
    public Channel GenerateChannelWithConflicts(int width, int netCount, int conflictCount)
    {
        var channel = GenerateSimpleChannel(width, netCount);
        var topRow = channel.TopRow;
        var bottomRow = channel.BottomRow;

        // Create conflicts by swapping bottom contacts of some net pairs
        for (int i = 0; i < conflictCount && i < netCount - 1; i++)
        {
            // Find two nets
            var netIds = channel.Nets.Keys.OrderBy(x => _random.Next()).Take(2).ToList();
            if (netIds.Count < 2) break;

            var net1 = channel.Nets[netIds[0]];
            var net2 = channel.Nets[netIds[1]];

            var bottom1 = net1.Contacts.First(c => c.Position == ContactPosition.Bottom).Column;
            var bottom2 = net2.Contacts.First(c => c.Position == ContactPosition.Bottom).Column;

            // Swap bottom contacts to create crossing
            bottomRow[bottom1] = netIds[1];
            bottomRow[bottom2] = netIds[0];
        }

        return new Channel(width, topRow, bottomRow);
    }

    /// <summary>
    /// Generates a channel with multi-terminal nets (multiple contacts in one row)
    /// </summary>
    public Channel GenerateMultiTerminalChannel(int width, int netCount, double multiTerminalProbability = 0.3)
    {
        var topRow = new int[width];
        var bottomRow = new int[width];

        var availableTopPositions = Enumerable.Range(0, width).ToList();
        var availableBottomPositions = Enumerable.Range(0, width).ToList();

        for (int netId = 1; netId <= netCount; netId++)
        {
            // Decide if this net will have multiple terminals
            bool hasMultipleTop = _random.NextDouble() < multiTerminalProbability;
            bool hasMultipleBottom = _random.NextDouble() < multiTerminalProbability;

            int topTerminals = hasMultipleTop ? _random.Next(2, 4) : 1;
            int bottomTerminals = hasMultipleBottom ? _random.Next(2, 4) : 1;

            // Assign top contacts
            for (int t = 0; t < topTerminals && availableTopPositions.Count > 0; t++)
            {
                int index = _random.Next(availableTopPositions.Count);
                topRow[availableTopPositions[index]] = netId;
                availableTopPositions.RemoveAt(index);
            }

            // Assign bottom contacts
            for (int b = 0; b < bottomTerminals && availableBottomPositions.Count > 0; b++)
            {
                int index = _random.Next(availableBottomPositions.Count);
                bottomRow[availableBottomPositions[index]] = netId;
                availableBottomPositions.RemoveAt(index);
            }
        }

        return new Channel(width, topRow, bottomRow);
    }

    /// <summary>
    /// Generates a channel from custom specifications
    /// </summary>
    public static Channel FromSpecification(int width, int[] topRow, int[] bottomRow)
    {
        return new Channel(width, topRow, bottomRow);
    }
}

/// <summary>
/// Builder pattern for creating complex channel configurations
/// </summary>
public class ChannelBuilder
{
    private int _width;
    private readonly List<int> _topRow;
    private readonly List<int> _bottomRow;

    public ChannelBuilder(int width)
    {
        _width = width;
        _topRow = new List<int>(new int[width]);
        _bottomRow = new List<int>(new int[width]);
    }

    public ChannelBuilder AddNet(int netId, int topColumn, int bottomColumn)
    {
        if (topColumn >= 0 && topColumn < _width)
            _topRow[topColumn] = netId;

        if (bottomColumn >= 0 && bottomColumn < _width)
            _bottomRow[bottomColumn] = netId;

        return this;
    }

    public ChannelBuilder AddTopContact(int netId, int column)
    {
        if (column >= 0 && column < _width)
            _topRow[column] = netId;
        return this;
    }

    public ChannelBuilder AddBottomContact(int netId, int column)
    {
        if (column >= 0 && column < _width)
            _bottomRow[column] = netId;
        return this;
    }

    public Channel Build()
    {
        return new Channel(_width, _topRow.ToArray(), _bottomRow.ToArray());
    }
}
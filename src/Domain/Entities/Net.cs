namespace src.Domain.Entities;

/// <summary>
/// Represents a circuit net connecting multiple contacts
/// </summary>
public class Net
{
    public int Id { get; }
    public List<Contact> Contacts { get; }
    public List<Segment> Segments { get; private set; }
    public int? AssignedTrack { get; set; }

    public Net(int id, List<Contact> contacts)
    {
        if (id <= 0)
            throw new ArgumentException("Net ID must be positive", nameof(id));

        if (contacts == null || contacts.Count == 0)
            throw new ArgumentException("Net must have at least one contact", nameof(contacts));

        Id = id;
        Contacts = contacts;
        Segments = new List<Segment>();
    }

    public int LeftmostColumn => Contacts.Min(c => c.Column);
    public int RightmostColumn => Contacts.Max(c => c.Column);

    public bool HasTopContact => Contacts.Any(c => c.Position == ContactPosition.Top);
    public bool HasBottomContact => Contacts.Any(c => c.Position == ContactPosition.Bottom);

    public void AddSegment(Segment segment)
    {
        if (segment.NetId != Id)
            throw new ArgumentException("Segment net ID must match this net", nameof(segment));

        Segments.Add(segment);
    }

    public override string ToString() => $"Net {Id}: Columns [{LeftmostColumn}-{RightmostColumn}], Track {AssignedTrack?.ToString() ?? "Unassigned"}";
}
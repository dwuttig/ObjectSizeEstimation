namespace ObjectSizeEstimation.Test;

public class Person
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int[] Scores { get; set; } = Array.Empty<int>();

    public Address? Address { get; set; }

    public Person? Self { get; set; }

    public Dictionary<Person, Address>? Contacts { get; set; }
}
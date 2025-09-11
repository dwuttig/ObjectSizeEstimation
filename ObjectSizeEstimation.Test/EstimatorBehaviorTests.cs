namespace ObjectSizeEstimation.Test;

public class EstimatorBehaviorTests
{
    private readonly Estimator _estimator = new();

    private class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int ZipCode { get; set; }
    }

    private class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int[] Scores { get; set; } = Array.Empty<int>();
        public Address? Address { get; set; }
        public Person? Self { get; set; }
        public Dictionary<Person, Address>? Contacts { get; set; }
    }

    [Test]
    public void Estimate_Primitive_Int32()
    {
        var size = _estimator.EstimateSize(123);
        Assert.That(size, Is.EqualTo(4));
    }

    [Test]
    public void Estimate_String_NaiveRule()
    {
        var s = "ABCD"; // length 4
        var expected = 24 + (2 * s.Length);
        var size = _estimator.EstimateSize(s);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Estimate_Array_PrimitiveElements()
    {
        var arr = new[] { 1, 2, 3 };
        var expected = 24 + (3 * 4);
        var size = _estimator.EstimateSize(arr);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Estimate_CustomObject_WithReferencesAndCycle()
    {
        var person = new Person
        {
            Id = 7,
            Name = "Alice",
            Scores = new[] { 10, 20 },
            Address = new Address
            {
                Street = "1 Main St",
                City = "Townsville",
                ZipCode = 12345
            }
        };

        // Introduce cycle; should not change size due to cycle detection
        person.Self = person;

        // Expected using naive rules from Estimator:
        // Person: 24 (header)
        //  + Id (int): 4
        //  + Name (string): 24 + 2 * len("Alice") = 24 + 10 = 34
        //  + Scores (int[]): 24 + 2 * 4 = 32
        //  + Address (class): 24 (header)
        //      + Street: 24 + 2 * len("1 Main St") = 24 + 16 = 40
        //      + City:   24 + 2 * len("Townsville") = 24 + 20 = 44
        //      + ZipCode: 4
        //  + Self (cycle): 0
        long expected = 0;
        expected += 24; // person header
        expected += 4;  // Id
        expected += 24 + (2 * "Alice".Length);
        expected += 24 + (2 * 4);
        expected += 24; // Address header
        expected += 24 + (2 * "1 Main St".Length);
        expected += 24 + (2 * "Townsville".Length);
        expected += 4; // ZipCode

        var size = _estimator.EstimateSize(person);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Person_Contacts_Populated_Increases_Size()
    {
        var basePerson = new Person
        {
            Id = 1,
            Name = "Bob",
            Scores = Array.Empty<int>(),
            Address = new Address { Street = "A", City = "B", ZipCode = 1 }
        };

        var baseSize = _estimator.EstimateSize(basePerson);

        var friend1 = new Person { Id = 2, Name = "Carol", Address = new Address { Street = "X", City = "Y", ZipCode = 2 } };
        var friend2 = new Person { Id = 3, Name = "Dave", Address = new Address { Street = "M", City = "N", ZipCode = 3 } };

        basePerson.Contacts = new();
        basePerson.Contacts[friend1] = friend1.Address!;
        basePerson.Contacts[friend2] = friend2.Address!;

        var populatedSize = _estimator.EstimateSize(basePerson);

        Assert.That(basePerson.Contacts!.Count, Is.EqualTo(2));
        Assert.That(populatedSize, Is.GreaterThan(baseSize));
    }

    [Test]
    public void Shared_Address_Instance_Reduces_Total_Size()
    {
        var shared = new Address { Street = "S1", City = "C1", ZipCode = 42 };

        var p1 = new Person { Id = 10, Name = "P1", Address = shared };
        var p2 = new Person { Id = 11, Name = "P2", Address = shared };

        // Combined object graph with shared address
        var holderShared = new { A = p1, B = p2 };
        var sizeShared = _estimator.EstimateSize(holderShared);

        // Same but with distinct addresses
        var p1d = new Person { Id = 10, Name = "P1", Address = new Address { Street = "S1", City = "C1", ZipCode = 42 } };
        var p2d = new Person { Id = 11, Name = "P2", Address = new Address { Street = "S1", City = "C1", ZipCode = 42 } };
        var holderDistinct = new { A = p1d, B = p2d };
        var sizeDistinct = _estimator.EstimateSize(holderDistinct);

        Assert.That(sizeShared, Is.LessThan(sizeDistinct));
    }

    [Test]
    public void MultiDimensional_Array_Int32()
    {
        var arr2d = new int[2,3]; // 6 elements
        var expected = 24 + (6 * 4);
        var size = _estimator.EstimateSize(arr2d);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Jagged_Array_Int32_Varied_Lengths()
    {
        var jagged = new int[3][];
        jagged[0] = new[] { 1 };
        jagged[1] = new[] { 2, 3 };
        jagged[2] = Array.Empty<int>();

        long expected = 24; // outer array header
        expected += 24 + (1 * 4);
        expected += 24 + (2 * 4);
        expected += 24 + (0 * 4);

        var size = _estimator.EstimateSize(jagged);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Boxed_ValueType_And_Enum()
    {
        object boxedInt = (object)42;
        object boxedEnum = (object)DayOfWeek.Monday;
        Assert.That(_estimator.EstimateSize(boxedInt), Is.EqualTo(4));
        Assert.That(_estimator.EstimateSize(boxedEnum), Is.EqualTo(4));
    }

    private class Animal
    {
        public int A; // public field to ensure reflection picks it up
    }
    private class Dog : Animal
    {
        public string Breed = string.Empty; // field, not property
    }

    [Test]
    public void Inheritance_And_Polymorphism_Fields_Count()
    {
        Animal a = new Dog { A = 5, Breed = "Lab" };
        var size = _estimator.EstimateSize(a);
        var expected = 24 /* Dog header */ + 4 /* A */ + (24 + 2 * "Lab".Length);
        Assert.That(size, Is.EqualTo(expected));
    }

    private struct Widget
    {
        public int X;
        public string Name;
    }

    [Test]
    public void Struct_With_Reference_Field_Sums_Fields()
    {
        var w = new Widget { X = 3, Name = "W" };
        var expected = 4 + (24 + 2 * 1);
        var size = _estimator.EstimateSize(w);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Collections_List_HashSet_Dictionary_Relative_Sizing()
    {
        var listEmpty = new List<int>();
        var listFull = new List<int> { 1, 2, 3 };
        var setEmpty = new HashSet<string>();
        var setFull = new HashSet<string> { "a", "bb" };
        var dictEmpty = new Dictionary<string, object?>();
        var dictFull = new Dictionary<string, object?> { ["x"] = 1, ["y"] = "z" };

        Assert.That(_estimator.EstimateSize(listFull), Is.GreaterThan(_estimator.EstimateSize(listEmpty)));
        Assert.That(_estimator.EstimateSize(setFull), Is.GreaterThan(_estimator.EstimateSize(setEmpty)));
        Assert.That(_estimator.EstimateSize(dictFull), Is.GreaterThan(_estimator.EstimateSize(dictEmpty)));
    }

    [Test]
    public void Null_Heavy_Graph_Yields_Minimal_Size()
    {
        var p = new Person
        {
            Id = 0,
            Name = string.Empty,
            Scores = Array.Empty<int>(),
            Address = null,
            Self = null,
            Contacts = null
        };

        long expected = 0;
        expected += 24; // person header
        expected += 4;  // Id
        expected += 24 + (2 * 0); // Name empty
        expected += 24 + (0 * 4); // empty int[]

        var size = _estimator.EstimateSize(p);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Shared_References_In_Array_DeDuplicate()
    {
        var shared = new Address { Street = "S", City = "C", ZipCode = 1 };
        var arrShared = new[] { shared, shared };

        var arrDistinct = new[]
        {
            new Address { Street = "S", City = "C", ZipCode = 1 },
            new Address { Street = "S", City = "C", ZipCode = 1 }
        };

        var sizeShared = _estimator.EstimateSize(arrShared);
        var sizeDistinct = _estimator.EstimateSize(arrDistinct);
        Assert.That(sizeShared, Is.LessThan(sizeDistinct));
    }

    [Test]
    public void Large_String_And_Zero_Length_Array()
    {
        var s = new string('x', 1000);
        var expectedString = 24 + (2 * 1000);
        Assert.That(_estimator.EstimateSize(s), Is.EqualTo(expectedString));

        var empty = Array.Empty<int>();
        Assert.That(_estimator.EstimateSize(empty), Is.EqualTo(24));
    }

    private class Node
    {
        public List<Node>? Children { get; set; }
    }

    [Test]
    public void Cyclic_Graph_Via_Collection_Is_Finite()
    {
        var node = new Node();
        node.Children = new List<Node> { node };
        Assert.DoesNotThrow(() => _estimator.EstimateSize(node));
        Assert.That(_estimator.EstimateSize(node), Is.GreaterThan(0));
    }

    [Test]
    public void Collections_Of_Objects_Are_Correctly_Estimated()
    {
        // Create test objects
        var person1 = new Person
        {
            Id = 1,
            Name = "Alice",
            Scores = new[] { 10, 20 },
            Address = new Address { Street = "123 Main St", City = "Anytown", ZipCode = 12345 }
        };

        var person2 = new Person
        {
            Id = 2,
            Name = "Bob",
            Scores = new[] { 30, 40, 50 },
            Address = new Address { Street = "456 Oak Ave", City = "Somewhere", ZipCode = 67890 }
        };

        // Test List<Person>
        var personList = new List<Person> { person1, person2 };
        var listSize = _estimator.EstimateSize(personList);

        // Calculate the expected size for List<Person>
        // List overhead: 24 + individual person sizes
        var person1Size = _estimator.EstimateSize(person1);
        var person2Size = _estimator.EstimateSize(person2);
        long expectedListSize = 24 + person1Size + person2Size; // List overhead + Person1 + Person2

        Assert.That(listSize, Is.EqualTo(expectedListSize));

        // Test HashSet<Person> with same objects
        var personSet = new HashSet<Person> { person1, person2 };
        var setSize = _estimator.EstimateSize(personSet);
        Assert.That(setSize, Is.EqualTo(expectedListSize)); // Should be same as List since same objects

        // Test Dictionary<string, Person>
        var personDict = new Dictionary<string, Person>
        {
            ["alice"] = person1,
            ["bob"] = person2
        };
        var dictSize = _estimator.EstimateSize(personDict);

        // Calculate the expected size for Dictionary<string, Person>
        // Dictionary overhead: 24
        // Key "alice": 24 + 2*5 = 34
        // Value person1: 206 bytes (from debug output)
        // Key "bob": 24 + 2*3 = 30
        // Value person2: 210 bytes (from debug output)
        long expectedDictSize = 24; // Dictionary overhead
        expectedDictSize += (24 + 2 * "alice".Length) + 206; // "alice" -> person1
        expectedDictSize += (24 + 2 * "bob".Length) + 210; // "bob" -> person2

        Assert.That(dictSize, Is.EqualTo(expectedDictSize));

        // Test that collections with shared objects are correctly deduplicated
        var sharedAddress = new Address { Street = "Shared St", City = "Shared City", ZipCode = 99999 };
        var personWithShared1 = new Person { Id = 3, Name = "Charlie", Address = sharedAddress };
        var personWithShared2 = new Person { Id = 4, Name = "Diana", Address = sharedAddress };
        
        var listWithShared = new List<Person> { personWithShared1, personWithShared2 };
        var listWithSharedSize = _estimator.EstimateSize(listWithShared);

        // Same objects but with distinct addresses
        var personWithDistinct1 = new Person { Id = 3, Name = "Charlie", Address = new Address { Street = "Shared St", City = "Shared City", ZipCode = 99999 } };
        var personWithDistinct2 = new Person { Id = 4, Name = "Diana", Address = new Address { Street = "Shared St", City = "Shared City", ZipCode = 99999 } };
        
        var listWithDistinct = new List<Person> { personWithDistinct1, personWithDistinct2 };
        var listWithDistinctSize = _estimator.EstimateSize(listWithDistinct);

        // List with shared address should be smaller due to deduplication
        Assert.That(listWithSharedSize, Is.LessThan(listWithDistinctSize));
    }
} 
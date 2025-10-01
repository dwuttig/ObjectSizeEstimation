using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation.Test;

[TestFixture]
public class ReflectionBasedEstimatorTest : BaseEstimatorTest
{
    private readonly ReflectionBasedEstimator _estimator = new();

    protected override IObjectSizeEstimator Estimator => _estimator;

    protected override IObjectSizeEstimator CreateEstimatorWithLogging(ILogger logger)
    {
        return new ReflectionBasedEstimator(logger);
    }

    // Memory layout constants
    private const int OBJECT_HEADER_SIZE = 24;
    private const int INT32_SIZE = 4;
    private const int STRING_OVERHEAD_SIZE = 24;
    private const int BYTES_PER_CHAR = 2;
    private const int ARRAY_OVERHEAD_SIZE = 24;
    private const int COLLECTION_OVERHEAD_SIZE = 24;
    private const int DICTIONARY_OVERHEAD_SIZE = 24;

    [Test]
    public void Estimate_Primitive_Int32_Exact()
    {
        var size = Estimator.EstimateSize(123);
        Assert.That(size, Is.EqualTo(INT32_SIZE));
    }

    [Test]
    public void Estimate_String_NaiveRule()
    {
        var s = "ABCD";
        var expected = STRING_OVERHEAD_SIZE + (BYTES_PER_CHAR * s.Length);
        var size = Estimator.EstimateSize(s);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Estimate_Array_PrimitiveElements_Exact()
    {
        var arr = new[] { 1, 2, 3 };
        var expected = ARRAY_OVERHEAD_SIZE + (3 * INT32_SIZE);
        var size = Estimator.EstimateSize(arr);
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

        person.Self = person;

        long expected = 0;
        expected += OBJECT_HEADER_SIZE;
        expected += INT32_SIZE;
        expected += STRING_OVERHEAD_SIZE + (BYTES_PER_CHAR * "Alice".Length);
        expected += ARRAY_OVERHEAD_SIZE + (2 * INT32_SIZE);
        expected += OBJECT_HEADER_SIZE;
        expected += STRING_OVERHEAD_SIZE + (BYTES_PER_CHAR * "1 Main St".Length);
        expected += STRING_OVERHEAD_SIZE + (BYTES_PER_CHAR * "Townsville".Length);
        expected += INT32_SIZE;

        var size = Estimator.EstimateSize(person);
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

        var baseSize = Estimator.EstimateSize(basePerson);

        var friend1 = new Person
        {
            Id = 2, Name = "Carol",
            Address = new Address { Street = "X", City = "Y", ZipCode = 2 }
        };
        var friend2 = new Person
        {
            Id = 3, Name = "Dave",
            Address = new Address { Street = "M", City = "N", ZipCode = 3 }
        };

        basePerson.Contacts = new();
        basePerson.Contacts[friend1] = friend1.Address!;
        basePerson.Contacts[friend2] = friend2.Address!;

        var populatedSize = Estimator.EstimateSize(basePerson);

        Assert.That(basePerson.Contacts!.Count, Is.EqualTo(2));
        Assert.That(populatedSize, Is.GreaterThan(baseSize));
    }

    [Test]
    public void Shared_Address_Instance_Reduces_Total_Size()
    {
        var shared = new Address { Street = "S1", City = "C1", ZipCode = 42 };

        var p1 = new Person { Id = 10, Name = "P1", Address = shared };
        var p2 = new Person { Id = 11, Name = "P2", Address = shared };

        var holderShared = new { A = p1, B = p2 };
        var sizeShared = Estimator.EstimateSize(holderShared);

        var p1d = new Person
        {
            Id = 10, Name = "P1",
            Address = new Address { Street = "S1", City = "C1", ZipCode = 42 }
        };
        var p2d = new Person
        {
            Id = 11, Name = "P2",
            Address = new Address { Street = "S1", City = "C1", ZipCode = 42 }
        };
        var holderDistinct = new { A = p1d, B = p2d };
        var sizeDistinct = Estimator.EstimateSize(holderDistinct);

        Assert.That(sizeShared, Is.LessThan(sizeDistinct));
    }

    [Test]
    public void MultiDimensional_Array_Int32()
    {
        var arr2d = new int[2, 3];
        var expected = ARRAY_OVERHEAD_SIZE + (6 * INT32_SIZE);
        var size = Estimator.EstimateSize(arr2d);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Jagged_Array_Int32_Varied_Lengths()
    {
        var jagged = new int[3][];
        jagged[0] = new[] { 1 };
        jagged[1] = new[] { 2, 3 };
        jagged[2] = Array.Empty<int>();

        long expected = ARRAY_OVERHEAD_SIZE;
        expected += ARRAY_OVERHEAD_SIZE + (1 * INT32_SIZE);
        expected += ARRAY_OVERHEAD_SIZE + (2 * INT32_SIZE);
        expected += ARRAY_OVERHEAD_SIZE + (0 * INT32_SIZE);

        var size = Estimator.EstimateSize(jagged);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Boxed_ValueType_And_Enum()
    {
        object boxedInt = 42;
        object boxedEnum = DayOfWeek.Monday;
        Assert.That(Estimator.EstimateSize(boxedInt), Is.EqualTo(INT32_SIZE));
        Assert.That(Estimator.EstimateSize(boxedEnum), Is.EqualTo(INT32_SIZE));
    }

    [Test]
    public void Inheritance_And_Polymorphism_Fields_Count()
    {
        Animal a = new Dog { A = 5, Breed = "Lab" };
        var size = Estimator.EstimateSize(a);
        var expected = OBJECT_HEADER_SIZE + INT32_SIZE + (STRING_OVERHEAD_SIZE + BYTES_PER_CHAR * "Lab".Length);
        Assert.That(size, Is.EqualTo(expected));
    }

    [Test]
    public void Struct_With_Reference_Field_Sums_Fields()
    {
        var w = new Widget { X = 3, Name = "W" };
        var expected = INT32_SIZE + (STRING_OVERHEAD_SIZE + BYTES_PER_CHAR * 1);
        var size = Estimator.EstimateSize(w);
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

        Assert.That(Estimator.EstimateSize(listFull), Is.GreaterThan(Estimator.EstimateSize(listEmpty)));
        Assert.That(Estimator.EstimateSize(setFull), Is.GreaterThan(Estimator.EstimateSize(setEmpty)));
        Assert.That(Estimator.EstimateSize(dictFull), Is.GreaterThan(Estimator.EstimateSize(dictEmpty)));
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
        expected += OBJECT_HEADER_SIZE;
        expected += INT32_SIZE;
        expected += STRING_OVERHEAD_SIZE + (BYTES_PER_CHAR * 0);
        expected += ARRAY_OVERHEAD_SIZE + (0 * INT32_SIZE);

        var size = Estimator.EstimateSize(p);
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

        var sizeShared = Estimator.EstimateSize(arrShared);
        var sizeDistinct = Estimator.EstimateSize(arrDistinct);
        Assert.That(sizeShared, Is.LessThan(sizeDistinct));
    }

    [Test]
    public void Large_String_And_Zero_Length_Array()
    {
        var s = new string('x', 1000);
        var expectedString = STRING_OVERHEAD_SIZE + (BYTES_PER_CHAR * 1000);
        Assert.That(Estimator.EstimateSize(s), Is.EqualTo(expectedString));

        var empty = Array.Empty<int>();
        Assert.That(Estimator.EstimateSize(empty), Is.EqualTo(ARRAY_OVERHEAD_SIZE));
    }

    [Test]
    public void Cyclic_Graph_Via_Collection_Is_Finite()
    {
        var node = new Node();
        node.Children = new List<Node> { node };
        Assert.DoesNotThrow(() => Estimator.EstimateSize(node));
        Assert.That(Estimator.EstimateSize(node), Is.GreaterThan(0));
    }

    [Test]
    public void Collections_Of_Objects_Are_Correctly_Estimated()
    {
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

        var personList = new List<Person> { person1, person2 };
        var listSize = Estimator.EstimateSize(personList);

        var person1Size = Estimator.EstimateSize(person1);
        var person2Size = Estimator.EstimateSize(person2);
        var expectedListSize = COLLECTION_OVERHEAD_SIZE + person1Size + person2Size;

        Assert.That(listSize, Is.EqualTo(expectedListSize));

        var personSet = new HashSet<Person> { person1, person2 };
        var setSize = Estimator.EstimateSize(personSet);
        Assert.That(setSize, Is.EqualTo(expectedListSize));

        var personDict = new Dictionary<string, Person>
        {
            ["alice"] = person1,
            ["bob"] = person2
        };
        var dictSize = Estimator.EstimateSize(personDict);

        var aliceKeySize = Estimator.EstimateSize("alice");
        var bobKeySize = Estimator.EstimateSize("bob");
        long expectedDictSize = DICTIONARY_OVERHEAD_SIZE;
        expectedDictSize += aliceKeySize + person1Size;
        expectedDictSize += bobKeySize + person2Size;

        Assert.That(dictSize, Is.EqualTo(expectedDictSize));
    }

    [Test]
    public void Logging_Shows_Object_Names_And_Sizes()
    {
        var logger = new TestLogger();
        var estimatorWithLogging = CreateEstimatorWithLogging(logger);

        var person = new Person
        {
            Id = 42,
            Name = "TestPerson",
            Scores = new[] { 1, 2, 3 },
            Address = new Address
            {
                Street = "Test Street",
                City = "Test City",
                ZipCode = 12345
            }
        };

        var size = estimatorWithLogging.EstimateSize(person);

        Assert.That(size, Is.GreaterThan(0));
        Assert.That(logger.LogEntries.Count, Is.GreaterThan(0));

        var hasObjectLogs = logger.LogEntries.Any(entry =>
            entry.Contains("Estimated object:") && entry.Contains("bytes"));
        Assert.That(hasObjectLogs, Is.True);
    }

    [Test]
    public void Nested_Collections_Are_Properly_Estimated()
    {
        var nestedList = new List<List<int>>
        {
            new List<int> { 1, 2, 3 },
            new List<int> { 4, 5 },
            new List<int> { 6, 7, 8, 9 }
        };

        var nestedListSize = Estimator.EstimateSize(nestedList);

        long expectedSize = COLLECTION_OVERHEAD_SIZE
                            + (COLLECTION_OVERHEAD_SIZE + 3 * INT32_SIZE)
                            + (COLLECTION_OVERHEAD_SIZE + 2 * INT32_SIZE)
                            + (COLLECTION_OVERHEAD_SIZE + 4 * INT32_SIZE);

        Assert.That(nestedListSize, Is.EqualTo(expectedSize));
    }

    [Test]
    public void Dictionary_With_Enum_Keys_Counts_Each_Key()
    {
        var dictWithEnumKeys = new Dictionary<DayOfWeek, string>
        {
            [DayOfWeek.Monday] = "value1",
            [DayOfWeek.Tuesday] = "value2",
            [DayOfWeek.Wednesday] = "value3"
        };

        var size = Estimator.EstimateSize(dictWithEnumKeys);

        long expectedSize = DICTIONARY_OVERHEAD_SIZE
                            + 3 * (INT32_SIZE + (STRING_OVERHEAD_SIZE + BYTES_PER_CHAR * 6));

        Assert.That(size, Is.EqualTo(expectedSize));
    }

    [Test]
    public void Enum_Keys_Are_Treated_As_Primitives()
    {
        var smallEnumDict = new Dictionary<ConsoleColor, int>
        {
            [ConsoleColor.Red] = 1,
            [ConsoleColor.Blue] = 2
        };

        var largeEnumDict = new Dictionary<DateTimeKind, int>
        {
            [DateTimeKind.Utc] = 1,
            [DateTimeKind.Local] = 2
        };

        var smallSize = Estimator.EstimateSize(smallEnumDict);
        var largeSize = Estimator.EstimateSize(largeEnumDict);

        Assert.That(smallSize, Is.GreaterThan(0));
        Assert.That(largeSize, Is.GreaterThan(0));

        var sizeDifference = Math.Abs(largeSize - smallSize);
        Assert.That(sizeDifference, Is.LessThan(TEST_SIZE_DIFFERENCE_TOLERANCE));
    }

    // Helper classes specific to ReflectionBasedEstimatorTest
    private class Animal
    {
        public int A;
    }

    private class Dog : Animal
    {
        public string Breed = string.Empty;
    }

    private struct Widget
    {
        public int X;
        public string Name;
    }

    private class Node
    {
        public List<Node>? Children { get; set; }
    }
}


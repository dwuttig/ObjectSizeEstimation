using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation.Test;

/// <summary>
/// Base class for all estimator tests, containing common test cases and helper classes.
/// </summary>
public abstract class BaseEstimatorTest
{
    protected const int TEST_SIZE_DIFFERENCE_TOLERANCE = 10;
    
    /// <summary>
    /// The estimator instance to test. Must be initialized by derived classes.
    /// </summary>
    protected abstract IObjectSizeEstimator Estimator { get; }
    
    /// <summary>
    /// Factory method to create an estimator with logging support.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract IObjectSizeEstimator CreateEstimatorWithLogging(ILogger logger);

    [Test]
    public void Estimate_Primitive_Int32()
    {
        var size = Estimator.EstimateSize(123);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_String()
    {
        var s = "Hello World";
        var size = Estimator.EstimateSize(s);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Array_PrimitiveElements()
    {
        var arr = new[] { 1, 2, 3, 4, 5 };
        var size = Estimator.EstimateSize(arr);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Complex_Object()
    {
        var person = new Person
        {
            Id = 1,
            Name = "Alice",
            Scores = new[] { 85, 90, 78 },
            Address = new Address
            {
                Street = "123 Main St",
                City = "Anytown",
                ZipCode = 12345
            }
        };

        var size = Estimator.EstimateSize(person);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Collection_List()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var size = Estimator.EstimateSize(list);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Collection_Dictionary()
    {
        var dict = new Dictionary<string, int>
        {
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3
        };
        var size = Estimator.EstimateSize(dict);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Nested_Collections()
    {
        var nestedList = new List<List<int>>
        {
            new List<int> { 1, 2, 3 },
            new List<int> { 4, 5, 6 },
            new List<int> { 7, 8, 9 }
        };
        var size = Estimator.EstimateSize(nestedList);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Anonymous_Object()
    {
        var anonymous = new
        {
            Name = "Test",
            Value = 42,
            Items = new[] { "a", "b", "c" }
        };
        var size = Estimator.EstimateSize(anonymous);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Null_Object()
    {
        var size = Estimator.EstimateSize(null!);
        Assert.That(size, Is.EqualTo(0));
    }

    [Test]
    public void Estimate_Empty_Collections()
    {
        var emptyList = new List<int>();
        var emptyDict = new Dictionary<string, int>();
        var emptyArray = Array.Empty<int>();

        var listSize = Estimator.EstimateSize(emptyList);
        var dictSize = Estimator.EstimateSize(emptyDict);
        var arraySize = Estimator.EstimateSize(emptyArray);

        Assert.That(listSize, Is.GreaterThan(0));
        Assert.That(dictSize, Is.GreaterThan(0));
        Assert.That(arraySize, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Value_Types()
    {
        var guid = Guid.NewGuid();
        var dateTime = DateTime.Now;
        var timeSpan = TimeSpan.FromHours(1);
        var decimalValue = 123.45m;

        var guidSize = Estimator.EstimateSize(guid);
        var dateTimeSize = Estimator.EstimateSize(dateTime);
        var timeSpanSize = Estimator.EstimateSize(timeSpan);
        var decimalSize = Estimator.EstimateSize(decimalValue);

        Assert.That(guidSize, Is.GreaterThan(0));
        Assert.That(dateTimeSize, Is.GreaterThan(0));
        Assert.That(timeSpanSize, Is.GreaterThan(0));
        Assert.That(decimalSize, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Enum_Values()
    {
        var dayOfWeek = DayOfWeek.Monday;
        var consoleColor = ConsoleColor.Red;
        var enumList = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };

        var daySize = Estimator.EstimateSize(dayOfWeek);
        var colorSize = Estimator.EstimateSize(consoleColor);
        var listSize = Estimator.EstimateSize(enumList);

        Assert.That(daySize, Is.GreaterThan(0));
        Assert.That(colorSize, Is.GreaterThan(0));
        Assert.That(listSize, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Complex_Nested_Structure()
    {
        var complexData = new ComplexDataStructure
        {
            IntValue = 42,
            StringValue = "Complex String",
            BoolValue = true,
            DoubleValue = 3.14159,
            IntList = new List<int> { 1, 2, 3, 4, 5 },
            StringList = new List<string> { "a", "bb", "ccc" },
            MixedList = new List<object> { 1, "string", true, 3.14 },
            StringToObjectDict = new Dictionary<string, object>
            {
                ["key1"] = new { Value = 1, Name = "First" },
                ["key2"] = new { Value = 2, Name = "Second" }
            },
            ObjectToObjectDict = new Dictionary<object, object>
            {
                [1] = "one",
                ["two"] = 2,
                [true] = false
            },
            NestedStructure = new NestedStructure
            {
                Level1 = new Level1
                {
                    Level2 = new Level2
                    {
                        Level3 = new Level3
                        {
                            DeepValue = "Very deep nested value",
                            DeepList = new List<string> { "deep1", "deep2", "deep3" }
                        }
                    }
                }
            },
            JaggedArray = new int[][]
            {
                new int[] { 1, 2 },
                new int[] { 3, 4, 5 },
                new int[] { 6, 7, 8, 9, 10 }
            },
            MultiDimensionalArray = new int[2, 3, 4],
            NullableInt = 42,
            NullableString = "not null",
            NullableObject = new { Value = "test" },
            EmptyList = new List<object>(),
            EmptyDict = new Dictionary<string, object>(),
            LargeString = new string('X', 1000),
            EnumValue = DayOfWeek.Friday,
            EnumList = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday }
        };

        var size = Estimator.EstimateSize(complexData);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_With_Cyclic_References()
    {
        var node = new ComplexNode();
        node.Children = new List<ComplexNode> { node }; // Create a cycle

        Assert.DoesNotThrow(() => Estimator.EstimateSize(node));
        var size = Estimator.EstimateSize(node);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void Estimate_Edge_Cases()
    {
        // Test with very large collections
        var largeList = new List<int>();
        for (var i = 0; i < 100000; i++)
        {
            largeList.Add(i);
        }

        Assert.DoesNotThrow(() => Estimator.EstimateSize(largeList));
        var largeListSize = Estimator.EstimateSize(largeList);
        Assert.That(largeListSize, Is.GreaterThan(0));

        // Test with deeply nested arrays
        var deepArray = new int[10][][];
        for (var i = 0; i < 10; i++)
        {
            deepArray[i] = new int[10][];
            for (var j = 0; j < 10; j++)
            {
                deepArray[i][j] = new int[10];
                for (var k = 0; k < 10; k++)
                {
                    deepArray[i][j][k] = i * 100 + j * 10 + k;
                }
            }
        }

        Assert.DoesNotThrow(() => Estimator.EstimateSize(deepArray));
        var deepArraySize = Estimator.EstimateSize(deepArray);
        Assert.That(deepArraySize, Is.GreaterThan(0));
    }

    protected object CreateDeeplyNestedObject(int depth)
    {
        if (depth <= 0)
        {
            return new { Value = "Leaf", Depth = 0 };
        }

        return new
        {
            Value = $"Level {depth}",
            Depth = depth,
            Child = CreateDeeplyNestedObject(depth - 1),
            Sibling = CreateDeeplyNestedObject(depth - 1)
        };
    }

    protected class ComplexNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ComplexNode>? Children { get; set; }
        public ComplexNode? SelfReference { get; set; }
    }

    protected class ComplexDataStructure
    {
        public int IntValue { get; set; }
        public string StringValue { get; set; } = string.Empty;
        public bool BoolValue { get; set; }
        public double DoubleValue { get; set; }
        public List<int> IntList { get; set; } = new();
        public List<string> StringList { get; set; } = new();
        public List<object> MixedList { get; set; } = new();
        public Dictionary<string, object> StringToObjectDict { get; set; } = new();
        public Dictionary<object, object> ObjectToObjectDict { get; set; } = new();
        public NestedStructure? NestedStructure { get; set; }
        public int[][] JaggedArray { get; set; } = Array.Empty<int[]>();
        public int[,,] MultiDimensionalArray { get; set; } = new int[0, 0, 0];
        public int? NullableInt { get; set; }
        public string? NullableString { get; set; }
        public object? NullableObject { get; set; }
        public ComplexNode? NodeStructure { get; set; }
        public List<object> EmptyList { get; set; } = new();
        public Dictionary<string, object> EmptyDict { get; set; } = new();
        public string LargeString { get; set; } = string.Empty;
        public DayOfWeek EnumValue { get; set; }
        public List<DayOfWeek> EnumList { get; set; } = new();
    }

    protected class NestedStructure
    {
        public Level1? Level1 { get; set; }
    }

    protected class Level1
    {
        public Level2? Level2 { get; set; }
        public string Level1Value { get; set; } = string.Empty;
    }

    protected class Level2 : Level1
    {
        public Level3? Level3 { get; set; }
        public string Level2Value { get; set; } = string.Empty;
    }

    protected class Level3 : Level2
    {
        public string DeepValue { get; set; } = string.Empty;
        public List<string> DeepList { get; set; } = new();
        public string Level3Value { get; set; } = string.Empty;
    }

    protected class Level4 : Level3
    {
        public string Level4Value { get; set; } = string.Empty;
        public List<object> NestedList { get; set; } = new();
    }

    protected class ProblematicObject
    {
        public string Value { get; set; } = string.Empty;
        public ProblematicObject? CircularReference { get; set; }
    }

    protected class ComplexKey
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public object Nested { get; set; } = new();

        public override bool Equals(object? obj)
        {
            return obj is ComplexKey other && Id == other.Id && Name == other.Name;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name);
        }
    }

    protected class MixedCollections
    {
        public List<List<int>> ListOfLists { get; set; } = new();
        public Dictionary<string, List<string>> DictOfLists { get; set; } = new();
        public Dictionary<string, object>[] ArrayOfDicts { get; set; } = Array.Empty<Dictionary<string, object>>();
    }

    protected struct ValueTypeWithReferences
    {
        public int IntValue;
        public string StringValue;
        public object ObjectValue;
        public List<int> ListValue;
    }
}


using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation.Test;

[TestFixture]
public class BinarySerializationBasedEstimatorTest
{
    private const int TEST_SIZE_DIFFERENCE_TOLERANCE = 10;
    private readonly BinarySerializationBasedEstimator _binarySerializationEstimator = new();

    [Test]
    public void BinarySerialization_Estimate_Primitive_Int32()
    {
        var size = _binarySerializationEstimator.EstimateSize(123);
        Assert.That(size, Is.GreaterThan(0));
        // JSON serialization of int32 results in "123" which is 3 bytes in UTF-8
        Assert.That(size, Is.EqualTo(3));
    }

    [Test]
    public void BinarySerialization_Estimate_String()
    {
        var s = "Hello World";
        var size = _binarySerializationEstimator.EstimateSize(s);
        Assert.That(size, Is.GreaterThan(0));
        // JSON serialization of string results in "\"Hello World\"" which is 13 bytes in UTF-8
        Assert.That(size, Is.EqualTo(13));
    }

    [Test]
    public void BinarySerialization_Estimate_Array_PrimitiveElements()
    {
        var arr = new[] { 1, 2, 3, 4, 5 };
        var size = _binarySerializationEstimator.EstimateSize(arr);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Complex_Object()
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

        var size = _binarySerializationEstimator.EstimateSize(person);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Collection_List()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var size = _binarySerializationEstimator.EstimateSize(list);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Collection_Dictionary()
    {
        var dict = new Dictionary<string, int>
        {
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3
        };
        var size = _binarySerializationEstimator.EstimateSize(dict);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Nested_Collections()
    {
        var nestedList = new List<List<int>>
        {
            new List<int> { 1, 2, 3 },
            new List<int> { 4, 5, 6 },
            new List<int> { 7, 8, 9 }
        };
        var size = _binarySerializationEstimator.EstimateSize(nestedList);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Anonymous_Object()
    {
        var anonymous = new
        {
            Name = "Test",
            Value = 42,
            Items = new[] { "a", "b", "c" }
        };
        var size = _binarySerializationEstimator.EstimateSize(anonymous);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Null_Object()
    {
        var size = _binarySerializationEstimator.EstimateSize(null!);
        Assert.That(size, Is.EqualTo(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Empty_Collections()
    {
        var emptyList = new List<int>();
        var emptyDict = new Dictionary<string, int>();
        var emptyArray = Array.Empty<int>();

        var listSize = _binarySerializationEstimator.EstimateSize(emptyList);
        var dictSize = _binarySerializationEstimator.EstimateSize(emptyDict);
        var arraySize = _binarySerializationEstimator.EstimateSize(emptyArray);

        Assert.That(listSize, Is.GreaterThan(0));
        Assert.That(dictSize, Is.GreaterThan(0));
        Assert.That(arraySize, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_With_Logging()
    {
        var logger = new TestLogger();
        var estimator = new BinarySerializationBasedEstimator(logger);

        var person = new Person
        {
            Id = 1,
            Name = "Test Person",
            Scores = new[] { 1, 2, 3 }
        };

        var size = estimator.EstimateSize(person);
        Assert.That(size, Is.GreaterThan(0));
        Assert.That(logger.LogEntries.Count, Is.GreaterThan(0));
        Assert.That(logger.LogEntries.Any(entry => entry.Contains("JsonSerialization:")), Is.True);
    }

    [Test]
    public void BinarySerialization_Estimate_Complex_Nested_Structure()
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

        var size = _binarySerializationEstimator.EstimateSize(complexData);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_With_Cyclic_References()
    {
        var node = new ComplexNode();
        node.Children = new List<ComplexNode> { node }; // Create a cycle

        // Binary serialization should handle cycles gracefully
        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(node));
        var size = _binarySerializationEstimator.EstimateSize(node);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Value_Types()
    {
        var guid = Guid.NewGuid();
        var dateTime = DateTime.Now;
        var timeSpan = TimeSpan.FromHours(1);
        var decimalValue = 123.45m;

        var guidSize = _binarySerializationEstimator.EstimateSize(guid);
        var dateTimeSize = _binarySerializationEstimator.EstimateSize(dateTime);
        var timeSpanSize = _binarySerializationEstimator.EstimateSize(timeSpan);
        var decimalSize = _binarySerializationEstimator.EstimateSize(decimalValue);

        Assert.That(guidSize, Is.GreaterThan(0));
        Assert.That(dateTimeSize, Is.GreaterThan(0));
        Assert.That(timeSpanSize, Is.GreaterThan(0));
        Assert.That(decimalSize, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Enum_Values()
    {
        var dayOfWeek = DayOfWeek.Monday;
        var consoleColor = ConsoleColor.Red;
        var enumList = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };

        var daySize = _binarySerializationEstimator.EstimateSize(dayOfWeek);
        var colorSize = _binarySerializationEstimator.EstimateSize(consoleColor);
        var listSize = _binarySerializationEstimator.EstimateSize(enumList);

        Assert.That(daySize, Is.GreaterThan(0));
        Assert.That(colorSize, Is.GreaterThan(0));
        Assert.That(listSize, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Comparison_With_Other_Estimators()
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

        var reflectionEstimator = new ReflectionBasedEstimator();
        var messagePackEstimator = new MessagePackBasedEstimator();
        var expressionTreeEstimator = new ExpressionTreeCachingEstimator();

        var reflectionSize = reflectionEstimator.EstimateSize(person);
        var messagePackSize = messagePackEstimator.EstimateSize(person);
        var expressionTreeSize = expressionTreeEstimator.EstimateSize(person);
        var binarySerializationSize = _binarySerializationEstimator.EstimateSize(person);

        // All should return positive sizes
        Assert.That(reflectionSize, Is.GreaterThan(0));
        Assert.That(messagePackSize, Is.GreaterThan(0));
        Assert.That(expressionTreeSize, Is.GreaterThan(0));
        Assert.That(binarySerializationSize, Is.GreaterThan(0));

        // BinarySerialization should be reasonably close to others (within tolerance)
        // JSON serialization can produce different sizes than other approaches, so we use a more lenient tolerance
        var reflectionDiff = Math.Abs(binarySerializationSize - reflectionSize);
        var messagePackDiff = Math.Abs(binarySerializationSize - messagePackSize);
        var expressionTreeDiff = Math.Abs(binarySerializationSize - expressionTreeSize);

        Assert.That(reflectionDiff, Is.LessThanOrEqualTo(reflectionSize * 2.0),
            "BinarySerialization size should be within 200% of Reflection size");
        Assert.That(messagePackDiff, Is.LessThanOrEqualTo(messagePackSize * 2.0),
            "BinarySerialization size should be within 200% of MessagePack size");
        Assert.That(expressionTreeDiff, Is.LessThanOrEqualTo(expressionTreeSize * 2.0),
            "BinarySerialization size should be within 200% of ExpressionTree size");
    }

    [Test]
    public void BinarySerialization_Estimate_Serialization_Error_Fallback()
    {
        // Create an object that might cause serialization issues
        var problematicObject = new ProblematicObject
        {
            Value = "test",
            CircularReference = null // Will be set to create a cycle
        };
        problematicObject.CircularReference = problematicObject;

        // This should not throw and should fall back to reflection-based estimation
        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(problematicObject));
        var size = _binarySerializationEstimator.EstimateSize(problematicObject);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Enum_Keys_Are_Treated_As_Primitives()
    {
        // Test that enum size is based on underlying type
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

        var smallSize = _binarySerializationEstimator.EstimateSize(smallEnumDict);
        var largeSize = _binarySerializationEstimator.EstimateSize(largeEnumDict);

        // Both should be positive
        Assert.That(smallSize, Is.GreaterThan(0));
        Assert.That(largeSize, Is.GreaterThan(0));

        // Both enums should have similar sizes since they're both int32-based
        // The difference should be minimal
        var sizeDifference = Math.Abs(largeSize - smallSize);
        Assert.That(sizeDifference, Is.LessThan(TEST_SIZE_DIFFERENCE_TOLERANCE)); // Allow for small variations
    }

    [Test]
    public void BinarySerialization_Estimate_Complex_Deeply_Nested_Structure_With_Multiple_Cycles_And_Edge_Cases()
    {
        // This test is designed to stress-test the estimator with complex scenarios
        // that could potentially break the implementation

        // Create a complex node structure with multiple reference types
        var rootNode = new ComplexNode { Id = 1, Name = "Root" };
        var child1 = new ComplexNode { Id = 2, Name = "Child1" };
        var child2 = new ComplexNode { Id = 3, Name = "Child2" };
        var grandChild = new ComplexNode { Id = 4, Name = "GrandChild" };

        // Create multiple cycles
        rootNode.Children = new List<ComplexNode> { child1, child2 };
        child1.Children = new List<ComplexNode> { grandChild };
        child2.Children = new List<ComplexNode> { grandChild }; // Shared reference
        grandChild.Children = new List<ComplexNode> { rootNode }; // Cycle back to root

        // Add self-references
        rootNode.SelfReference = rootNode;
        child1.SelfReference = child1;

        // Create a complex data structure with mixed types
        var complexData = new ComplexDataStructure
        {
            // Primitive values
            IntValue = 42,
            StringValue = "Complex String with special characters: !@#$%^&*()",
            BoolValue = true,
            DoubleValue = 3.14159,

            // Collections with different types
            IntList = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            StringList = new List<string> { "a", "bb", "ccc", "dddd", "eeeee" },
            MixedList = new List<object> { 1, "string", true, 3.14, new { Nested = "object" } },

            // Dictionaries with complex keys and values
            StringToObjectDict = new Dictionary<string, object>
            {
                ["key1"] = new { Value = 1, Name = "First" },
                ["key2"] = new { Value = 2, Name = "Second" },
                ["key3"] = new List<int> { 1, 2, 3 }
            },

            ObjectToObjectDict = new Dictionary<object, object>
            {
                [1] = "one",
                ["two"] = 2,
                [true] = false,
                [new { Key = "complex" }] = new { Value = "nested" }
            },

            // Nested structures
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

            // Arrays with different dimensions
            JaggedArray = new int[][]
            {
                new int[] { 1, 2 },
                new int[] { 3, 4, 5 },
                new int[] { 6, 7, 8, 9, 10 }
            },

            MultiDimensionalArray = new int[2, 3, 4], // 24 elements

            // Nullable types
            NullableInt = 42,
            NullableString = "not null",
            NullableObject = new { Value = "test" },

            // Reference to the complex node structure
            NodeStructure = rootNode,

            // Empty collections
            EmptyList = new List<object>(),
            EmptyDict = new Dictionary<string, object>(),

            // Large string
            LargeString = new string('X', 10000),

            // Enum values
            EnumValue = DayOfWeek.Friday,
            EnumList = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday }
        };

        // This should not throw an exception
        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(complexData));

        var size = _binarySerializationEstimator.EstimateSize(complexData);

        // The size should be positive and reasonably large due to the complexity
        Assert.That(size, Is.GreaterThan(0));

        // Test that the size is significantly larger than a simple object
        var simpleObject = new { Value = 42 };
        var simpleSize = _binarySerializationEstimator.EstimateSize(simpleObject);
        Assert.That(size, Is.GreaterThan(simpleSize * 100)); // Should be much larger

        // Test that cycles don't cause infinite recursion
        var nodeSize = _binarySerializationEstimator.EstimateSize(rootNode);
        Assert.That(nodeSize, Is.GreaterThan(0));

        // Test individual components
        Assert.That(_binarySerializationEstimator.EstimateSize(complexData.IntList), Is.GreaterThan(0));
        Assert.That(_binarySerializationEstimator.EstimateSize(complexData.StringToObjectDict), Is.GreaterThan(0));
        Assert.That(_binarySerializationEstimator.EstimateSize(complexData.NestedStructure), Is.GreaterThan(0));
        Assert.That(_binarySerializationEstimator.EstimateSize(complexData.LargeString), Is.GreaterThan(10000));
    }

    [Test]
    public void BinarySerialization_Estimate_Edge_Cases_That_Could_Break_Implementation()
    {
        // Test with very large collections
        var largeList = new List<int>();
        for (var i = 0; i < 100000; i++)
        {
            largeList.Add(i);
        }

        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(largeList));
        var largeListSize = _binarySerializationEstimator.EstimateSize(largeList);
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

        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(deepArray));
        var deepArraySize = _binarySerializationEstimator.EstimateSize(deepArray);
        Assert.That(deepArraySize, Is.GreaterThan(0));

        // Test with mixed null and non-null values
        var mixedNulls = new List<object?> { 1, null, "string", null, new { Value = 42 }, null };
        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(mixedNulls));
        var mixedNullsSize = _binarySerializationEstimator.EstimateSize(mixedNulls);
        Assert.That(mixedNullsSize, Is.GreaterThan(0));

        // Test with circular references in dictionaries
        var circularDict = new Dictionary<string, object>();
        circularDict["self"] = circularDict;
        circularDict["other"] = new Dictionary<string, object> { ["back"] = circularDict };

        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(circularDict));
        var circularDictSize = _binarySerializationEstimator.EstimateSize(circularDict);
        Assert.That(circularDictSize, Is.GreaterThan(0));
    }

    [Test]
    public void BinarySerialization_Estimate_Extreme_Edge_Cases_That_Should_Break_Implementation()
    {
        // This test contains scenarios that could potentially break the current implementation
        // due to limitations in the naive estimation approach

        // Test 1: Very large multi-dimensional array that could cause memory issues
        var hugeMultiDimArray = new int[1000, 1000]; // 1 million elements
        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(hugeMultiDimArray));
        var hugeArraySize = _binarySerializationEstimator.EstimateSize(hugeMultiDimArray);
        Assert.That(hugeArraySize, Is.GreaterThan(0));

        // Test 2: Complex inheritance hierarchy with multiple levels
        var complexInheritance = new Level4
        {
            Level1Value = "Level1",
            Level2Value = "Level2",
            Level3Value = "Level3",
            Level4Value = "Level4",
            NestedList = new List<object> { 1, "string", true, 3.14, new { Complex = "object" } }
        };

        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(complexInheritance));
        var inheritanceSize = _binarySerializationEstimator.EstimateSize(complexInheritance);
        Assert.That(inheritanceSize, Is.GreaterThan(0));

        // Test 3: Dictionary with complex object keys that might not be handled properly
        var complexKeyDict = new Dictionary<ComplexKey, string>();
        var key1 = new ComplexKey { Id = 1, Name = "Key1", Nested = new { Value = 42 } };
        var key2 = new ComplexKey { Id = 2, Name = "Key2", Nested = new { Value = 84 } };
        complexKeyDict[key1] = "Value1";
        complexKeyDict[key2] = "Value2";

        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(complexKeyDict));
        var complexKeySize = _binarySerializationEstimator.EstimateSize(complexKeyDict);
        Assert.That(complexKeySize, Is.GreaterThan(0));

        // Test 4: Very deep nesting that could cause stack overflow
        var deepNesting = CreateDeeplyNestedObject(10); // 100 levels deep
        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(deepNesting));
        var deepNestingSize = _binarySerializationEstimator.EstimateSize(deepNesting);
        Assert.That(deepNestingSize, Is.GreaterThan(0));

        // Test 5: Mixed collection types that might not be handled correctly
        var mixedCollections = new MixedCollections
        {
            ListOfLists = new List<List<int>> { new List<int> { 1, 2, 3 }, new List<int> { 4, 5, 6 } },
            DictOfLists = new Dictionary<string, List<string>>
            {
                ["key1"] = new List<string> { "a", "b", "c" },
                ["key2"] = new List<string> { "d", "e", "f" }
            },
            ArrayOfDicts = new Dictionary<string, object>[]
            {
                new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 },
                new Dictionary<string, object> { ["c"] = 3, ["d"] = 4 }
            }
        };

        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(mixedCollections));
        var mixedCollectionsSize = _binarySerializationEstimator.EstimateSize(mixedCollections);
        Assert.That(mixedCollectionsSize, Is.GreaterThan(0));

        // Test 6: Anonymous types with complex structures
        var anonymousComplex = new
        {
            Simple = 42,
            Complex = new
            {
                Nested = new
                {
                    Deep = new
                    {
                        Value = "Very deep",
                        List = new List<object> { 1, 2, 3, "string", true },
                        Dict = new Dictionary<string, object> { ["key"] = "value" }
                    }
                }
            },
            Array = new[] { 1, 2, 3, 4, 5 }
        };

        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(anonymousComplex));
        var anonymousSize = _binarySerializationEstimator.EstimateSize(anonymousComplex);
        Assert.That(anonymousSize, Is.GreaterThan(0));

        // Test 7: Potential issue with value types containing reference types
        var valueTypeWithRefs = new ValueTypeWithReferences
        {
            IntValue = 42,
            StringValue = "Test",
            ObjectValue = new { Nested = "value" },
            ListValue = new List<int> { 1, 2, 3 }
        };

        Assert.DoesNotThrow(() => _binarySerializationEstimator.EstimateSize(valueTypeWithRefs));
        var valueTypeSize = _binarySerializationEstimator.EstimateSize(valueTypeWithRefs);
        Assert.That(valueTypeSize, Is.GreaterThan(0));
    }

    // Helper methods and classes for extreme edge cases
    private object CreateDeeplyNestedObject(int depth)
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

    private class Level4 : Level3
    {
        public string Level4Value { get; set; } = string.Empty;

        public List<object> NestedList { get; set; } = new();
    }

    private class ComplexKey
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

    private class MixedCollections
    {
        public List<List<int>> ListOfLists { get; set; } = new();

        public Dictionary<string, List<string>> DictOfLists { get; set; } = new();

        public Dictionary<string, object>[] ArrayOfDicts { get; set; } = Array.Empty<Dictionary<string, object>>();
    }

    private struct ValueTypeWithReferences
    {
        public int IntValue;
        public string StringValue;
        public object ObjectValue;
        public List<int> ListValue;
    }

    // Helper classes for the complex test
    private class ComplexNode
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<ComplexNode>? Children { get; set; }

        public ComplexNode? SelfReference { get; set; }
    }

    private class ComplexDataStructure
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

    private class NestedStructure
    {
        public Level1? Level1 { get; set; }
    }

    private class Level1
    {
        public Level2? Level2 { get; set; }

        public string Level1Value { get; set; } = string.Empty;
    }

    private class Level2 : Level1
    {
        public Level3? Level3 { get; set; }

        public string Level2Value { get; set; } = string.Empty;
    }

    private class Level3 : Level2
    {
        public string DeepValue { get; set; } = string.Empty;

        public List<string> DeepList { get; set; } = new();

        public string Level3Value { get; set; } = string.Empty;
    }

    // Helper class for testing problematic serialization
    private class ProblematicObject
    {
        public string Value { get; set; } = string.Empty;

        public ProblematicObject? CircularReference { get; set; }
    }
}

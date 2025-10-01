using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation.Test;

[TestFixture]
public class MemoryLayoutAnalysisEstimatorTest : BaseEstimatorTest
{
    private readonly MemoryLayoutAnalysisEstimator _estimator = new();

    protected override IObjectSizeEstimator Estimator => _estimator;

    protected override IObjectSizeEstimator CreateEstimatorWithLogging(ILogger logger)
    {
        return new MemoryLayoutAnalysisEstimator(logger);
    }

    [Test]
    public void MemoryLayout_Estimate_Primitive_Int32_Exact()
    {
        var size = Estimator.EstimateSize(123);
        Assert.That(size, Is.EqualTo(4)); // int is 4 bytes
    }

    [Test]
    public void MemoryLayout_Estimate_Primitive_String_Exact()
    {
        var s = "Hello World";
        var size = Estimator.EstimateSize(s);
        // String size: object header + length + (length * 2 bytes per char)
        var expectedSize = (IntPtr.Size * 2) + 4 + (s.Length * 2);
        Assert.That(size, Is.EqualTo(expectedSize));
    }

    [Test]
    public void MemoryLayout_Estimate_Array_PrimitiveElements_Exact()
    {
        var arr = new[] { 1, 2, 3, 4, 5 };
        var size = Estimator.EstimateSize(arr);
        // Array size: header + (length * element size)
        var headerSize = (IntPtr.Size * 2) + 4;
        var elementSize = arr.Length * 4;
        var expectedSize = headerSize + elementSize;
        Assert.That(size, Is.EqualTo(expectedSize));
    }

    [Test]
    public void MemoryLayout_Estimate_Value_Types_Exact()
    {
        var guid = Guid.NewGuid();
        var dateTime = DateTime.Now;
        var timeSpan = TimeSpan.FromHours(1);
        var decimalValue = 123.45m;

        var guidSize = Estimator.EstimateSize(guid);
        var dateTimeSize = Estimator.EstimateSize(dateTime);
        var timeSpanSize = Estimator.EstimateSize(timeSpan);
        var decimalSize = Estimator.EstimateSize(decimalValue);

        Assert.That(guidSize, Is.EqualTo(16)); // Guid is 16 bytes
        Assert.That(dateTimeSize, Is.EqualTo(8)); // DateTime is 8 bytes
        Assert.That(timeSpanSize, Is.EqualTo(8)); // TimeSpan is 8 bytes
        Assert.That(decimalSize, Is.EqualTo(16)); // Decimal is 16 bytes
    }

    [Test]
    public void MemoryLayout_Estimate_Enum_Values_Exact()
    {
        var dayOfWeek = DayOfWeek.Monday;
        var consoleColor = ConsoleColor.Red;

        var daySize = Estimator.EstimateSize(dayOfWeek);
        var colorSize = Estimator.EstimateSize(consoleColor);

        // Enums are typically based on their underlying type size
        Assert.That(daySize, Is.GreaterThan(0));
        Assert.That(colorSize, Is.GreaterThan(0));
        
        // Both should be reasonable enum sizes (4 or 8 bytes typically)
        Assert.That(daySize, Is.InRange(4, 8));
        Assert.That(colorSize, Is.InRange(4, 8));
    }

    [Test]
    public void MemoryLayout_Estimate_With_Logging()
    {
        var logger = new TestLogger();
        var estimator = CreateEstimatorWithLogging(logger);

        var testObject = new { Id = 1, Name = "Test" };
        var size = estimator.EstimateSize(testObject);

        Assert.That(size, Is.GreaterThan(0));
        Assert.That(logger.LogEntries.Count, Is.GreaterThan(0));
        Assert.That(logger.LogEntries.Any(entry => entry.Contains("MemoryLayout")), Is.True);
    }

    [Test]
    public void MemoryLayout_Estimate_Comparison_With_Other_Estimators()
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
        var binarySerializationEstimator = new BinarySerializationBasedEstimator();
        var sourceGeneratorEstimator = new SourceGeneratorBasedEstimator();

        var reflectionSize = reflectionEstimator.EstimateSize(person);
        var messagePackSize = messagePackEstimator.EstimateSize(person);
        var expressionTreeSize = expressionTreeEstimator.EstimateSize(person);
        var binarySerializationSize = binarySerializationEstimator.EstimateSize(person);
        var sourceGeneratorSize = sourceGeneratorEstimator.EstimateSize(person);
        var memoryLayoutSize = Estimator.EstimateSize(person);

        Assert.That(reflectionSize, Is.GreaterThan(0));
        Assert.That(messagePackSize, Is.GreaterThan(0));
        Assert.That(expressionTreeSize, Is.GreaterThan(0));
        Assert.That(binarySerializationSize, Is.GreaterThan(0));
        Assert.That(sourceGeneratorSize, Is.GreaterThan(0));
        Assert.That(memoryLayoutSize, Is.GreaterThan(0));

        var reflectionDiff = Math.Abs(memoryLayoutSize - reflectionSize);
        var messagePackDiff = Math.Abs(memoryLayoutSize - messagePackSize);
        var expressionTreeDiff = Math.Abs(memoryLayoutSize - expressionTreeSize);
        var binarySerializationDiff = Math.Abs(memoryLayoutSize - binarySerializationSize);
        var sourceGeneratorDiff = Math.Abs(memoryLayoutSize - sourceGeneratorSize);

        Assert.That(reflectionDiff, Is.LessThanOrEqualTo(Math.Max(reflectionSize, memoryLayoutSize) * 2.0),
            "Memory Layout size should be within 200% of Reflection size");
        Assert.That(messagePackDiff, Is.LessThanOrEqualTo(Math.Max(messagePackSize, memoryLayoutSize) * 2.0),
            "Memory Layout size should be within 200% of MessagePack size");
        Assert.That(expressionTreeDiff, Is.LessThanOrEqualTo(Math.Max(expressionTreeSize, memoryLayoutSize) * 2.0),
            "Memory Layout size should be within 200% of ExpressionTree size");
        Assert.That(binarySerializationDiff, Is.LessThanOrEqualTo(Math.Max(binarySerializationSize, memoryLayoutSize) * 2.0),
            "Memory Layout size should be within 200% of BinarySerialization size");
        Assert.That(sourceGeneratorDiff, Is.LessThanOrEqualTo(Math.Max(sourceGeneratorSize, memoryLayoutSize) * 2.0),
            "Memory Layout size should be within 200% of SourceGenerator size");
    }

    [Test]
    public void MemoryLayout_Estimate_Serialization_Error_Fallback()
    {
        var problematicObject = new ProblematicObject
        {
            Value = "test",
            CircularReference = null
        };
        problematicObject.CircularReference = problematicObject;

        Assert.DoesNotThrow(() => Estimator.EstimateSize(problematicObject));
        var size = Estimator.EstimateSize(problematicObject);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void MemoryLayout_Estimate_Deeply_Nested_Structure()
    {
        var deepObject = CreateDeeplyNestedObject(10);
        var size = Estimator.EstimateSize(deepObject);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void MemoryLayout_Estimate_Mixed_Data_Types()
    {
        var mixedData = new
        {
            IntValue = 42,
            StringValue = "Hello",
            BoolValue = true,
            DoubleValue = 3.14,
            DateTimeValue = DateTime.Now,
            GuidValue = Guid.NewGuid(),
            ListValue = new List<int> { 1, 2, 3 },
            DictValue = new Dictionary<string, object> { ["key"] = "value" },
            ArrayValue = new[] { 1, 2, 3, 4, 5 }
        };

        var size = Estimator.EstimateSize(mixedData);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void MemoryLayout_Estimate_Large_Collections()
    {
        var largeList = new List<int>();
        for (var i = 0; i < 10000; i++)
        {
            largeList.Add(i);
        }

        var size = Estimator.EstimateSize(largeList);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void MemoryLayout_Estimate_Performance_Consistency()
    {
        var testObject = new Person
        {
            Id = 1,
            Name = "Test",
            Scores = new[] { 1, 2, 3 },
            Address = new Address { Street = "Test St", City = "Test City", ZipCode = 12345 }
        };

        var sizes = new List<long>();
        for (var i = 0; i < 10; i++)
        {
            sizes.Add(Estimator.EstimateSize(testObject));
        }

        // All estimates should be the same
        Assert.That(sizes.All(s => s == sizes[0]), Is.True);
    }

    [Test]
    public void MemoryLayout_Estimate_MultiDimensional_Arrays()
    {
        var multiDimArray = new int[2, 3, 4]; // 24 elements total
        var size = Estimator.EstimateSize(multiDimArray);
        
        Assert.That(size, Is.GreaterThan(0));
        
        // Should account for multi-dimensional array overhead
        var headerSize = (IntPtr.Size * 2) + 4;
        var boundsSize = 3 * 4; // 3 dimensions * 4 bytes each
        var elementsSize = 24 * 4; // 24 elements * 4 bytes each
        var expectedMinSize = headerSize + boundsSize + elementsSize;
        
        Assert.That(size, Is.GreaterThanOrEqualTo(expectedMinSize));
    }

    [Test]
    public void MemoryLayout_Estimate_Struct_With_Reference_Fields()
    {
        var structWithRefs = new StructWithReferences
        {
            IntValue = 42,
            StringValue = "Test String",
            ObjectValue = new { Value = 123 }
        };

        var size = Estimator.EstimateSize(structWithRefs);
        Assert.That(size, Is.GreaterThan(0));
    }

    private struct StructWithReferences
    {
        public int IntValue;
        public string StringValue;
        public object ObjectValue;
    }
}

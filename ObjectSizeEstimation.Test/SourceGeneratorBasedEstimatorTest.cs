using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation.Test;

[TestFixture]
public class SourceGeneratorBasedEstimatorTest : BaseEstimatorTest
{
    private readonly SourceGeneratorBasedEstimator _estimator = new();

    protected override IObjectSizeEstimator Estimator => _estimator;

    protected override IObjectSizeEstimator CreateEstimatorWithLogging(ILogger logger)
    {
        return new SourceGeneratorBasedEstimator(logger);
    }

    [Test]
    public void SourceGenerator_Estimate_Primitive_Int32_Exact()
    {
        var size = Estimator.EstimateSize(123);
        Assert.That(size, Is.EqualTo(4)); // int32 is 4 bytes
    }

    [Test]
    public void SourceGenerator_Estimate_Primitive_String_Exact()
    {
        var s = "Hello World";
        var size = Estimator.EstimateSize(s);
        // String: 24 bytes overhead + 2 bytes per character
        Assert.That(size, Is.EqualTo(24 + (s.Length * 2)));
    }

    [Test]
    public void SourceGenerator_Estimate_Array_PrimitiveElements_Exact()
    {
        var arr = new[] { 1, 2, 3, 4, 5 };
        var size = Estimator.EstimateSize(arr);
        // Array: 24 bytes overhead + 4 bytes per int32 element
        Assert.That(size, Is.EqualTo(24 + (arr.Length * 4)));
    }

    [Test]
    public void SourceGenerator_Estimate_With_Logging()
    {
        var logger = new TestLogger();
        var estimator = CreateEstimatorWithLogging(logger);

        var simpleObject = new
        {
            Id = 1,
            Name = "Test Person",
            Value = 42.5
        };

        var size = estimator.EstimateSize(simpleObject);
        Assert.That(size, Is.GreaterThan(0));
        Assert.That(logger.LogEntries.Count, Is.GreaterThan(0));
        Assert.That(logger.LogEntries.Any(entry => entry.Contains("SourceGenerator: Estimated size")), Is.True);
    }

    [Test]
    public void SourceGenerator_Estimate_Comparison_With_Other_Estimators()
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

        var reflectionSize = reflectionEstimator.EstimateSize(person);
        var messagePackSize = messagePackEstimator.EstimateSize(person);
        var expressionTreeSize = expressionTreeEstimator.EstimateSize(person);
        var binarySerializationSize = binarySerializationEstimator.EstimateSize(person);
        var sourceGeneratorSize = Estimator.EstimateSize(person);

        Assert.That(reflectionSize, Is.GreaterThan(0));
        Assert.That(messagePackSize, Is.GreaterThan(0));
        Assert.That(expressionTreeSize, Is.GreaterThan(0));
        Assert.That(binarySerializationSize, Is.GreaterThan(0));
        Assert.That(sourceGeneratorSize, Is.GreaterThan(0));

        var reflectionDiff = Math.Abs(sourceGeneratorSize - reflectionSize);
        var messagePackDiff = Math.Abs(sourceGeneratorSize - messagePackSize);
        var expressionTreeDiff = Math.Abs(sourceGeneratorSize - expressionTreeSize);
        var binarySerializationDiff = Math.Abs(sourceGeneratorSize - binarySerializationSize);

        Assert.That(reflectionDiff, Is.LessThanOrEqualTo(Math.Max(reflectionSize, sourceGeneratorSize) * 0.5),
            "SourceGenerator size should be within 50% of Reflection size");
        Assert.That(messagePackDiff, Is.LessThanOrEqualTo(Math.Max(messagePackSize, sourceGeneratorSize) * 0.5),
            "SourceGenerator size should be within 50% of MessagePack size");
        Assert.That(expressionTreeDiff, Is.LessThanOrEqualTo(Math.Max(expressionTreeSize, sourceGeneratorSize) * 2.0),
            "SourceGenerator size should be within 200% of ExpressionTree size");
        Assert.That(binarySerializationDiff, Is.LessThanOrEqualTo(Math.Max(binarySerializationSize, sourceGeneratorSize) * 0.5),
            "SourceGenerator size should be within 50% of BinarySerialization size");
    }

    [Test]
    public void SourceGenerator_Estimate_Serialization_Error_Fallback()
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
    public void SourceGenerator_Estimate_Large_Collections()
    {
        var largeList = new List<int>();
        for (var i = 0; i < 10000; i++)
        {
            largeList.Add(i);
        }

        Assert.DoesNotThrow(() => Estimator.EstimateSize(largeList));
        var largeListSize = Estimator.EstimateSize(largeList);
        Assert.That(largeListSize, Is.GreaterThan(0));

        var largeDict = new Dictionary<string, int>();
        for (var i = 0; i < 5000; i++)
        {
            largeDict[$"key_{i}"] = i;
        }

        Assert.DoesNotThrow(() => Estimator.EstimateSize(largeDict));
        var largeDictSize = Estimator.EstimateSize(largeDict);
        Assert.That(largeDictSize, Is.GreaterThan(0));
    }

    [Test]
    public void SourceGenerator_Estimate_Deeply_Nested_Structure()
    {
        var deepObject = CreateDeeplyNestedObject(10);
        var size = Estimator.EstimateSize(deepObject);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void SourceGenerator_Estimate_Mixed_Data_Types()
    {
        var mixedData = new
        {
            IntValue = 42,
            StringValue = "Hello World",
            BoolValue = true,
            DoubleValue = 3.14159,
            IntArray = new[] { 1, 2, 3, 4, 5 },
            StringList = new List<string> { "a", "b", "c" },
            NestedObject = new
            {
                Value = "Nested",
                Number = 123,
                Items = new[] { "item1", "item2" }
            },
            Dictionary = new Dictionary<string, object>
            {
                ["key1"] = 1,
                ["key2"] = "value2",
                ["key3"] = true
            }
        };

        var size = Estimator.EstimateSize(mixedData);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void SourceGenerator_Estimate_Performance_Consistency()
    {
        var person = new Person
        {
            Id = 1,
            Name = "Test Person",
            Scores = new[] { 1, 2, 3, 4, 5 },
            Address = new Address
            {
                Street = "123 Test St",
                City = "Test City",
                ZipCode = 12345
            }
        };

        var sizes = new List<long>();
        for (var i = 0; i < 10; i++)
        {
            var size = Estimator.EstimateSize(person);
            sizes.Add(size);
        }

        // All sizes should be the same
        var firstSize = sizes[0];
        foreach (var size in sizes)
        {
            Assert.That(size, Is.EqualTo(firstSize), "SourceGenerator should return consistent results");
        }
    }
}

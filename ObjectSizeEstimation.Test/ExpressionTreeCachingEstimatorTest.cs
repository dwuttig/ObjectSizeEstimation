using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation.Test;

[TestFixture]
public class ExpressionTreeCachingEstimatorTest : BaseEstimatorTest
{
    private readonly ExpressionTreeCachingEstimator _estimator = new();

    protected override IObjectSizeEstimator Estimator => _estimator;

    protected override IObjectSizeEstimator CreateEstimatorWithLogging(ILogger logger)
    {
        return new ExpressionTreeCachingEstimator(logger);
    }

    [Test]
    public void ExpressionTree_Estimate_Primitive_Int32_Exact()
    {
        var size = Estimator.EstimateSize(123);
        Assert.That(size, Is.EqualTo(4)); // int32 should be 4 bytes
    }

    [Test]
    public void ExpressionTree_Estimate_String_Exact()
    {
        var s = "Hello World";
        var size = Estimator.EstimateSize(s);
        // String: 24 bytes overhead + 2 bytes per char
        Assert.That(size, Is.EqualTo(24 + (s.Length * 2)));
    }

    [Test]
    public void ExpressionTree_Estimate_Array_PrimitiveElements_Exact()
    {
        var arr = new[] { 1, 2, 3, 4, 5 };
        var size = Estimator.EstimateSize(arr);
        // Array: 24 bytes overhead + 5 * 4 bytes per int
        Assert.That(size, Is.EqualTo(24 + (5 * 4)));
    }

    [Test]
    public void ExpressionTree_Estimate_With_Logging()
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
        Assert.That(logger.LogEntries.Any(entry => entry.Contains("ExpressionTree estimated size")), Is.True);
    }

    [Test]
    public void ExpressionTree_Estimate_Comparison_With_Other_Estimators()
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

        Assert.That(reflectionSize, Is.GreaterThan(0));
        Assert.That(messagePackSize, Is.GreaterThan(0));
        Assert.That(expressionTreeSize, Is.GreaterThan(0));

        var reflectionDiff = Math.Abs(expressionTreeSize - reflectionSize);
        var messagePackDiff = Math.Abs(expressionTreeSize - messagePackSize);
        
        Assert.That(reflectionDiff, Is.LessThanOrEqualTo(Math.Max(reflectionSize, expressionTreeSize) * 2.0), 
            "ExpressionTree size should be within 200% of Reflection size");
        Assert.That(messagePackDiff, Is.LessThanOrEqualTo(Math.Max(messagePackSize, expressionTreeSize) * 2.0), 
            "ExpressionTree size should be within 200% of MessagePack size");
    }

    [Test]
    public void ExpressionTree_Caching_Behavior()
    {
        var logger = new TestLogger();
        var estimator = CreateEstimatorWithLogging(logger);

        // First call should compile the expression
        var person1 = new Person { Id = 1, Name = "Alice" };
        var size1 = estimator.EstimateSize(person1);
        
        // Second call should use cached expression
        var person2 = new Person { Id = 2, Name = "Bob" };
        var size2 = estimator.EstimateSize(person2);

        Assert.That(size1, Is.GreaterThan(0));
        Assert.That(size2, Is.GreaterThan(0));
        
        // Both should be similar sizes
        var sizeDiff = Math.Abs(size1 - size2);
        Assert.That(sizeDiff, Is.LessThan(TEST_SIZE_DIFFERENCE_TOLERANCE));
    }
}

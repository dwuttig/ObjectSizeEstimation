using MessagePack;
using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation.Test;

[TestFixture]
public class MessagePackBasedEstimatorTest : BaseEstimatorTest
{
    private readonly MessagePackBasedEstimator _estimator = new();

    protected override IObjectSizeEstimator Estimator => _estimator;

    protected override IObjectSizeEstimator CreateEstimatorWithLogging(ILogger logger)
    {
        return new MessagePackBasedEstimator(logger);
    }

    [Test]
    public void MessagePack_Estimate_With_Custom_Serializer_Options()
    {
        var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
        var estimator = new MessagePackBasedEstimator(serializerOptions: options);

        var person = new Person
        {
            Id = 1,
            Name = "Test Person",
            Scores = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }
        };

        var size = estimator.EstimateSize(person);
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    public void MessagePack_Estimate_With_Logging()
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
        Assert.That(logger.LogEntries.Any(entry => entry.Contains("MessagePack estimated size")), Is.True);
    }

    [Test]
    public void MessagePack_Estimate_Comparison_With_Reflection_Estimator()
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

        var reflectionSize = reflectionEstimator.EstimateSize(person);
        var messagePackSize = messagePackEstimator.EstimateSize(person);

        Assert.That(reflectionSize, Is.GreaterThan(0));
        Assert.That(messagePackSize, Is.GreaterThan(0));
    }

    [Test]
    public void MessagePack_Estimate_Serialization_Error_Fallback()
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
}


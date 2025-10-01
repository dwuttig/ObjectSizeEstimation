using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ObjectSizeEstimation.Test;

[TestFixture]
public class PerformanceComparisonTest
{
    private TestLogger _logger = null!;
    private MessagePackBasedEstimator _messagePackEstimator = null!;
    private ReflectionBasedEstimator _reflectionEstimator = null!;
    private ExpressionTreeCachingEstimator _expressionTreeEstimator = null!;
    private BinarySerializationBasedEstimator _binarySerializationEstimator = null!;
    private SourceGeneratorBasedEstimator _sourceGeneratorEstimator = null!;
    private MemoryLayoutAnalysisEstimator _memoryLayoutEstimator = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger();
        _messagePackEstimator = new MessagePackBasedEstimator(_logger);
        _reflectionEstimator = new ReflectionBasedEstimator(_logger);
        _expressionTreeEstimator = new ExpressionTreeCachingEstimator(_logger);
        _binarySerializationEstimator = new BinarySerializationBasedEstimator(_logger);
        _sourceGeneratorEstimator = new SourceGeneratorBasedEstimator(_logger);
        _memoryLayoutEstimator = new MemoryLayoutAnalysisEstimator(_logger);
    }

    [Test]
    public void Performance_Comparison_Depth_20_Nested_Object()
    {
        // Create a deeply nested object structure (depth 20)
        var deepObject = CreateDeeplyNestedObject(20);

        Console.WriteLine($"Testing with nested object of depth 20...");
        Console.WriteLine($"Object type: {deepObject.GetType().Name}");

        // Test MessagePack-based estimator
        var messagePackResult = MeasurePerformance("MessagePackBasedEstimator", () => _messagePackEstimator.EstimateSize(deepObject));

        // Test Reflection-based estimator
        var reflectionResult = MeasurePerformance("ReflectionBasedEstimator", () => _reflectionEstimator.EstimateSize(deepObject));

        // Test ExpressionTree-based estimator
        var expressionTreeResult = MeasurePerformance("ExpressionTreeCachingEstimator", () => _expressionTreeEstimator.EstimateSize(deepObject));

        // Test BinarySerialization-based estimator
        var binarySerializationResult = MeasurePerformance("BinarySerializationBasedEstimator", () => _binarySerializationEstimator.EstimateSize(deepObject));

        // Test SourceGenerator-based estimator
        var sourceGeneratorResult = MeasurePerformance("SourceGeneratorBasedEstimator", () => _sourceGeneratorEstimator.EstimateSize(deepObject));


        // Test MemoryLayout-based estimator
        var memoryLayoutResult = MeasurePerformance("MemoryLayoutAnalysisEstimator", () => _memoryLayoutEstimator.EstimateSize(deepObject));

        // Display results
        Console.WriteLine("\n=== Performance Comparison Results ===");
        Console.WriteLine($"MessagePack Estimator:");
        Console.WriteLine($"  Size: {messagePackResult.Size:N0} bytes");
        Console.WriteLine($"  Execution Time: {messagePackResult.ExecutionTime.TotalMilliseconds:F3} ms");
        Console.WriteLine($"  Memory Used: {messagePackResult.MemoryUsed:N0} bytes");
        Console.WriteLine($"  GC Collections (Gen 0/1/2): {messagePackResult.GC0}/{messagePackResult.GC1}/{messagePackResult.GC2}");

        Console.WriteLine($"\nReflection Estimator:");
        Console.WriteLine($"  Size: {reflectionResult.Size:N0} bytes");
        Console.WriteLine($"  Execution Time: {reflectionResult.ExecutionTime.TotalMilliseconds:F3} ms");
        Console.WriteLine($"  Memory Used: {reflectionResult.MemoryUsed:N0} bytes");
        Console.WriteLine($"  GC Collections (Gen 0/1/2): {reflectionResult.GC0}/{reflectionResult.GC1}/{reflectionResult.GC2}");

        Console.WriteLine($"\nExpressionTree Estimator:");
        Console.WriteLine($"  Size: {expressionTreeResult.Size:N0} bytes");
        Console.WriteLine($"  Execution Time: {expressionTreeResult.ExecutionTime.TotalMilliseconds:F3} ms");
        Console.WriteLine($"  Memory Used: {expressionTreeResult.MemoryUsed:N0} bytes");
        Console.WriteLine($"  GC Collections (Gen 0/1/2): {expressionTreeResult.GC0}/{expressionTreeResult.GC1}/{expressionTreeResult.GC2}");

        Console.WriteLine($"\nBinarySerialization Estimator:");
        Console.WriteLine($"  Size: {binarySerializationResult.Size:N0} bytes");
        Console.WriteLine($"  Execution Time: {binarySerializationResult.ExecutionTime.TotalMilliseconds:F3} ms");
        Console.WriteLine($"  Memory Used: {binarySerializationResult.MemoryUsed:N0} bytes");
        Console.WriteLine($"  GC Collections (Gen 0/1/2): {binarySerializationResult.GC0}/{binarySerializationResult.GC1}/{binarySerializationResult.GC2}");

        Console.WriteLine($"\nSourceGenerator Estimator:");
        Console.WriteLine($"  Size: {sourceGeneratorResult.Size:N0} bytes");
        Console.WriteLine($"  Execution Time: {sourceGeneratorResult.ExecutionTime.TotalMilliseconds:F3} ms");
        Console.WriteLine($"  Memory Used: {sourceGeneratorResult.MemoryUsed:N0} bytes");
        Console.WriteLine($"  GC Collections (Gen 0/1/2): {sourceGeneratorResult.GC0}/{sourceGeneratorResult.GC1}/{sourceGeneratorResult.GC2}");


        Console.WriteLine($"\nMemoryLayout Estimator:");
        Console.WriteLine($"  Size: {memoryLayoutResult.Size:N0} bytes");
        Console.WriteLine($"  Execution Time: {memoryLayoutResult.ExecutionTime.TotalMilliseconds:F3} ms");
        Console.WriteLine($"  Memory Used: {memoryLayoutResult.MemoryUsed:N0} bytes");
        Console.WriteLine($"  GC Collections (Gen 0/1/2): {memoryLayoutResult.GC0}/{memoryLayoutResult.GC1}/{memoryLayoutResult.GC2}");

        // Performance analysis
        Console.WriteLine($"\n=== Performance Analysis ===");
        var reflectionToMessagePackTime = reflectionResult.ExecutionTime.TotalMilliseconds / messagePackResult.ExecutionTime.TotalMilliseconds;
        var expressionTreeToMessagePackTime = expressionTreeResult.ExecutionTime.TotalMilliseconds / messagePackResult.ExecutionTime.TotalMilliseconds;
        var expressionTreeToReflectionTime = expressionTreeResult.ExecutionTime.TotalMilliseconds / reflectionResult.ExecutionTime.TotalMilliseconds;
        var binarySerializationToMessagePackTime = binarySerializationResult.ExecutionTime.TotalMilliseconds / messagePackResult.ExecutionTime.TotalMilliseconds;
        var binarySerializationToReflectionTime = binarySerializationResult.ExecutionTime.TotalMilliseconds / reflectionResult.ExecutionTime.TotalMilliseconds;
        var binarySerializationToExpressionTreeTime = binarySerializationResult.ExecutionTime.TotalMilliseconds / expressionTreeResult.ExecutionTime.TotalMilliseconds;
        var sourceGeneratorToMessagePackTime = sourceGeneratorResult.ExecutionTime.TotalMilliseconds / messagePackResult.ExecutionTime.TotalMilliseconds;
        var sourceGeneratorToReflectionTime = sourceGeneratorResult.ExecutionTime.TotalMilliseconds / reflectionResult.ExecutionTime.TotalMilliseconds;
        var sourceGeneratorToExpressionTreeTime = sourceGeneratorResult.ExecutionTime.TotalMilliseconds / expressionTreeResult.ExecutionTime.TotalMilliseconds;
        var sourceGeneratorToBinarySerializationTime = sourceGeneratorResult.ExecutionTime.TotalMilliseconds / binarySerializationResult.ExecutionTime.TotalMilliseconds;
        var memoryLayoutToMessagePackTime = memoryLayoutResult.ExecutionTime.TotalMilliseconds / messagePackResult.ExecutionTime.TotalMilliseconds;
        var memoryLayoutToReflectionTime = memoryLayoutResult.ExecutionTime.TotalMilliseconds / reflectionResult.ExecutionTime.TotalMilliseconds;
        var memoryLayoutToExpressionTreeTime = memoryLayoutResult.ExecutionTime.TotalMilliseconds / expressionTreeResult.ExecutionTime.TotalMilliseconds;
        var memoryLayoutToBinarySerializationTime = memoryLayoutResult.ExecutionTime.TotalMilliseconds / binarySerializationResult.ExecutionTime.TotalMilliseconds;
        var memoryLayoutToSourceGeneratorTime = memoryLayoutResult.ExecutionTime.TotalMilliseconds / sourceGeneratorResult.ExecutionTime.TotalMilliseconds;

        var reflectionToMessagePackMemory = (double)reflectionResult.MemoryUsed / messagePackResult.MemoryUsed;
        var expressionTreeToMessagePackMemory = (double)expressionTreeResult.MemoryUsed / messagePackResult.MemoryUsed;
        var expressionTreeToReflectionMemory = (double)expressionTreeResult.MemoryUsed / reflectionResult.MemoryUsed;
        var binarySerializationToMessagePackMemory = (double)binarySerializationResult.MemoryUsed / messagePackResult.MemoryUsed;
        var binarySerializationToReflectionMemory = (double)binarySerializationResult.MemoryUsed / reflectionResult.MemoryUsed;
        var binarySerializationToExpressionTreeMemory = (double)binarySerializationResult.MemoryUsed / expressionTreeResult.MemoryUsed;
        var sourceGeneratorToMessagePackMemory = (double)sourceGeneratorResult.MemoryUsed / messagePackResult.MemoryUsed;
        var sourceGeneratorToReflectionMemory = (double)sourceGeneratorResult.MemoryUsed / reflectionResult.MemoryUsed;
        var sourceGeneratorToExpressionTreeMemory = (double)sourceGeneratorResult.MemoryUsed / expressionTreeResult.MemoryUsed;
        var sourceGeneratorToBinarySerializationMemory = (double)sourceGeneratorResult.MemoryUsed / binarySerializationResult.MemoryUsed;
        var memoryLayoutToMessagePackMemory = (double)memoryLayoutResult.MemoryUsed / messagePackResult.MemoryUsed;
        var memoryLayoutToReflectionMemory = (double)memoryLayoutResult.MemoryUsed / reflectionResult.MemoryUsed;
        var memoryLayoutToExpressionTreeMemory = (double)memoryLayoutResult.MemoryUsed / expressionTreeResult.MemoryUsed;
        var memoryLayoutToBinarySerializationMemory = (double)memoryLayoutResult.MemoryUsed / binarySerializationResult.MemoryUsed;
        var memoryLayoutToSourceGeneratorMemory = (double)memoryLayoutResult.MemoryUsed / sourceGeneratorResult.MemoryUsed;

        Console.WriteLine($"Time Ratios:");
        Console.WriteLine($"  Reflection/MessagePack: {reflectionToMessagePackTime:F2}x");
        Console.WriteLine($"  ExpressionTree/MessagePack: {expressionTreeToMessagePackTime:F2}x");
        Console.WriteLine($"  ExpressionTree/Reflection: {expressionTreeToReflectionTime:F2}x");
        Console.WriteLine($"  BinarySerialization/MessagePack: {binarySerializationToMessagePackTime:F2}x");
        Console.WriteLine($"  BinarySerialization/Reflection: {binarySerializationToReflectionTime:F2}x");
        Console.WriteLine($"  BinarySerialization/ExpressionTree: {binarySerializationToExpressionTreeTime:F2}x");
        Console.WriteLine($"  SourceGenerator/MessagePack: {sourceGeneratorToMessagePackTime:F2}x");
        Console.WriteLine($"  SourceGenerator/Reflection: {sourceGeneratorToReflectionTime:F2}x");
        Console.WriteLine($"  SourceGenerator/ExpressionTree: {sourceGeneratorToExpressionTreeTime:F2}x");
        Console.WriteLine($"  SourceGenerator/BinarySerialization: {sourceGeneratorToBinarySerializationTime:F2}x");
        Console.WriteLine($"  MemoryLayout/MessagePack: {memoryLayoutToMessagePackTime:F2}x");
        Console.WriteLine($"  MemoryLayout/Reflection: {memoryLayoutToReflectionTime:F2}x");
        Console.WriteLine($"  MemoryLayout/ExpressionTree: {memoryLayoutToExpressionTreeTime:F2}x");
        Console.WriteLine($"  MemoryLayout/BinarySerialization: {memoryLayoutToBinarySerializationTime:F2}x");
        Console.WriteLine($"  MemoryLayout/SourceGenerator: {memoryLayoutToSourceGeneratorTime:F2}x");

        Console.WriteLine($"Memory Ratios:");
        Console.WriteLine($"  Reflection/MessagePack: {reflectionToMessagePackMemory:F2}x");
        Console.WriteLine($"  ExpressionTree/MessagePack: {expressionTreeToMessagePackMemory:F2}x");
        Console.WriteLine($"  ExpressionTree/Reflection: {expressionTreeToReflectionMemory:F2}x");
        Console.WriteLine($"  BinarySerialization/MessagePack: {binarySerializationToMessagePackMemory:F2}x");
        Console.WriteLine($"  BinarySerialization/Reflection: {binarySerializationToReflectionMemory:F2}x");
        Console.WriteLine($"  BinarySerialization/ExpressionTree: {binarySerializationToExpressionTreeMemory:F2}x");
        Console.WriteLine($"  SourceGenerator/MessagePack: {sourceGeneratorToMessagePackMemory:F2}x");
        Console.WriteLine($"  SourceGenerator/Reflection: {sourceGeneratorToReflectionMemory:F2}x");
        Console.WriteLine($"  SourceGenerator/ExpressionTree: {sourceGeneratorToExpressionTreeMemory:F2}x");
        Console.WriteLine($"  SourceGenerator/BinarySerialization: {sourceGeneratorToBinarySerializationMemory:F2}x");
        Console.WriteLine($"  MemoryLayout/MessagePack: {memoryLayoutToMessagePackMemory:F2}x");
        Console.WriteLine($"  MemoryLayout/Reflection: {memoryLayoutToReflectionMemory:F2}x");
        Console.WriteLine($"  MemoryLayout/ExpressionTree: {memoryLayoutToExpressionTreeMemory:F2}x");
        Console.WriteLine($"  MemoryLayout/BinarySerialization: {memoryLayoutToBinarySerializationMemory:F2}x");
        Console.WriteLine($"  MemoryLayout/SourceGenerator: {memoryLayoutToSourceGeneratorMemory:F2}x");

        // Determine fastest estimator
        var fastestTime = Math.Min(
            Math.Min(
                Math.Min(
                    Math.Min(
                        Math.Min(
                            messagePackResult.ExecutionTime.TotalMilliseconds,
                            reflectionResult.ExecutionTime.TotalMilliseconds),
                        expressionTreeResult.ExecutionTime.TotalMilliseconds),
                    binarySerializationResult.ExecutionTime.TotalMilliseconds),
                sourceGeneratorResult.ExecutionTime.TotalMilliseconds),
            memoryLayoutResult.ExecutionTime.TotalMilliseconds);

        if (fastestTime == messagePackResult.ExecutionTime.TotalMilliseconds)
        {
            Console.WriteLine($"\n🏆 MessagePack is the fastest estimator");
        }
        else if (fastestTime == expressionTreeResult.ExecutionTime.TotalMilliseconds)
        {
            Console.WriteLine($"\n🏆 ExpressionTree is the fastest estimator");
        }
        else if (fastestTime == binarySerializationResult.ExecutionTime.TotalMilliseconds)
        {
            Console.WriteLine($"\n🏆 BinarySerialization is the fastest estimator");
        }
        else if (fastestTime == sourceGeneratorResult.ExecutionTime.TotalMilliseconds)
        {
            Console.WriteLine($"\n🏆 SourceGenerator is the fastest estimator");
        }
        else if (fastestTime == memoryLayoutResult.ExecutionTime.TotalMilliseconds)
        {
            Console.WriteLine($"\n🏆 MemoryLayout is the fastest estimator");
        }
        else
        {
            Console.WriteLine($"\n🏆 Reflection is the fastest estimator");
        }

        // Determine most memory efficient
        var leastMemory = Math.Min(
            Math.Min(
                Math.Min(
                    Math.Min(
                        Math.Min(
                            messagePackResult.MemoryUsed,
                            reflectionResult.MemoryUsed),
                        expressionTreeResult.MemoryUsed),
                    binarySerializationResult.MemoryUsed),
                sourceGeneratorResult.MemoryUsed),
            memoryLayoutResult.MemoryUsed);

        if (leastMemory == messagePackResult.MemoryUsed)
        {
            Console.WriteLine($"💾 MessagePack uses the least memory");
        }
        else if (leastMemory == expressionTreeResult.MemoryUsed)
        {
            Console.WriteLine($"💾 ExpressionTree uses the least memory");
        }
        else if (leastMemory == binarySerializationResult.MemoryUsed)
        {
            Console.WriteLine($"💾 BinarySerialization uses the least memory");
        }
        else if (leastMemory == sourceGeneratorResult.MemoryUsed)
        {
            Console.WriteLine($"💾 SourceGenerator uses the least memory");
        }
        else if (leastMemory == memoryLayoutResult.MemoryUsed)
        {
            Console.WriteLine($"💾 MemoryLayout uses the least memory");
        }
        else
        {
            Console.WriteLine($"💾 Reflection uses the least memory");
        }

        // Assertions
        Assert.That(messagePackResult.Size, Is.GreaterThan(0), "MessagePack estimator should return positive size");
        Assert.That(reflectionResult.Size, Is.GreaterThan(0), "Reflection estimator should return positive size");
        Assert.That(expressionTreeResult.Size, Is.GreaterThan(0), "ExpressionTree estimator should return positive size");
        Assert.That(binarySerializationResult.Size, Is.GreaterThan(0), "BinarySerialization estimator should return positive size");
        Assert.That(sourceGeneratorResult.Size, Is.GreaterThan(0), "SourceGenerator estimator should return positive size");
        Assert.That(memoryLayoutResult.Size, Is.GreaterThan(0), "MemoryLayout estimator should return positive size");
        Assert.That(messagePackResult.ExecutionTime.TotalMilliseconds, Is.LessThan(30000), "MessagePack estimator should complete within 30 seconds");
        Assert.That(reflectionResult.ExecutionTime.TotalMilliseconds, Is.LessThan(30000), "Reflection estimator should complete within 30 seconds");
        Assert.That(expressionTreeResult.ExecutionTime.TotalMilliseconds, Is.LessThan(30000), "ExpressionTree estimator should complete within 30 seconds");
        Assert.That(binarySerializationResult.ExecutionTime.TotalMilliseconds, Is.LessThan(30000), "BinarySerialization estimator should complete within 30 seconds");
        Assert.That(sourceGeneratorResult.ExecutionTime.TotalMilliseconds, Is.LessThan(30000), "SourceGenerator estimator should complete within 30 seconds");
        Assert.That(memoryLayoutResult.ExecutionTime.TotalMilliseconds, Is.LessThan(30000), "MemoryLayout estimator should complete within 30 seconds");
    }

    [Test]
    public void Performance_Comparison_Multiple_Depths()
    {
        var depths = new[] { 5, 10, 15, 20 };
        var results = new List<(int Depth, PerformanceResult MessagePack, PerformanceResult Reflection, PerformanceResult ExpressionTree, PerformanceResult BinarySerialization, PerformanceResult SourceGenerator, PerformanceResult MemoryLayout)>();

        Console.WriteLine("=== Performance Comparison Across Different Depths ===");
        Console.WriteLine("Depth | MessagePack (ms) | Reflection (ms) | ExpressionTree (ms) | BinarySerial (ms) | SourceGen (ms) | MemoryLayout (ms) | Best Time | Best Memory");
        Console.WriteLine("------|------------------|----------------|---------------------|------------------|----------------|------------------|-----------|------------");

        foreach (var depth in depths)
        {
            var deepObject = CreateDeeplyNestedObject(depth);

            var messagePackResult = MeasurePerformance($"MessagePack_Depth_{depth}", () => _messagePackEstimator.EstimateSize(deepObject));

            var reflectionResult = MeasurePerformance($"Reflection_Depth_{depth}", () => _reflectionEstimator.EstimateSize(deepObject));

            var expressionTreeResult = MeasurePerformance($"ExpressionTree_Depth_{depth}", () => _expressionTreeEstimator.EstimateSize(deepObject));

            var binarySerializationResult = MeasurePerformance($"BinarySerialization_Depth_{depth}", () => _binarySerializationEstimator.EstimateSize(deepObject));

            var sourceGeneratorResult = MeasurePerformance($"SourceGenerator_Depth_{depth}", () => _sourceGeneratorEstimator.EstimateSize(deepObject));


            var memoryLayoutResult = MeasurePerformance($"MemoryLayout_Depth_{depth}", () => _memoryLayoutEstimator.EstimateSize(deepObject));

            results.Add((depth, messagePackResult, reflectionResult, expressionTreeResult, binarySerializationResult, sourceGeneratorResult, memoryLayoutResult));

            var bestTime = Math.Min(
                Math.Min(
                    Math.Min(
                        Math.Min(
                            Math.Min(
                                messagePackResult.ExecutionTime.TotalMilliseconds,
                                reflectionResult.ExecutionTime.TotalMilliseconds),
                            expressionTreeResult.ExecutionTime.TotalMilliseconds),
                        binarySerializationResult.ExecutionTime.TotalMilliseconds),
                    sourceGeneratorResult.ExecutionTime.TotalMilliseconds),
                memoryLayoutResult.ExecutionTime.TotalMilliseconds);

            var bestMemory = Math.Min(
                Math.Min(
                    Math.Min(
                        Math.Min(
                            Math.Min(
                                messagePackResult.MemoryUsed,
                                reflectionResult.MemoryUsed),
                            expressionTreeResult.MemoryUsed),
                        binarySerializationResult.MemoryUsed),
                    sourceGeneratorResult.MemoryUsed),
                memoryLayoutResult.MemoryUsed);

            var bestTimeName = bestTime == messagePackResult.ExecutionTime.TotalMilliseconds ? "MP" :
                bestTime == expressionTreeResult.ExecutionTime.TotalMilliseconds ? "ET" :
                bestTime == binarySerializationResult.ExecutionTime.TotalMilliseconds ? "BS" :
                bestTime == sourceGeneratorResult.ExecutionTime.TotalMilliseconds ? "SG" :
                bestTime == memoryLayoutResult.ExecutionTime.TotalMilliseconds ? "ML" : "RF";

            var bestMemoryName = bestMemory == messagePackResult.MemoryUsed ? "MP" :
                bestMemory == expressionTreeResult.MemoryUsed ? "ET" :
                bestMemory == binarySerializationResult.MemoryUsed ? "BS" :
                bestMemory == sourceGeneratorResult.MemoryUsed ? "SG" :
                bestMemory == memoryLayoutResult.MemoryUsed ? "ML" : "RF";

            Console.WriteLine($"{depth,5} | {messagePackResult.ExecutionTime.TotalMilliseconds,15:F2} | {reflectionResult.ExecutionTime.TotalMilliseconds,13:F2} | {expressionTreeResult.ExecutionTime.TotalMilliseconds,19:F2} | {binarySerializationResult.ExecutionTime.TotalMilliseconds,16:F2} | {sourceGeneratorResult.ExecutionTime.TotalMilliseconds,14:F2} | {memoryLayoutResult.ExecutionTime.TotalMilliseconds,17:F2} | {bestTimeName,9} | {bestMemoryName,10}");
        }

        // Verify all tests completed successfully
        foreach (var (depth, messagePack, reflection, expressionTree, binarySerialization, sourceGenerator, memoryLayout) in results)
        {
            Assert.That(messagePack.Size, Is.GreaterThan(0), $"MessagePack should return positive size for depth {depth}");
            Assert.That(reflection.Size, Is.GreaterThan(0), $"Reflection should return positive size for depth {depth}");
            Assert.That(expressionTree.Size, Is.GreaterThan(0), $"ExpressionTree should return positive size for depth {depth}");
            Assert.That(binarySerialization.Size, Is.GreaterThan(0), $"BinarySerialization should return positive size for depth {depth}");
            Assert.That(sourceGenerator.Size, Is.GreaterThan(0), $"SourceGenerator should return positive size for depth {depth}");
            Assert.That(memoryLayout.Size, Is.GreaterThan(0), $"MemoryLayout should return positive size for depth {depth}");
        }
    }

    [Test]
    public void Performance_Comparison_Large_Collections()
    {
        Console.WriteLine("=== Performance Comparison with Large Collections ===");

        // Test with large collections
        var testCases = new (string Name, object TestObject)[]
        {
            ("Large List (10,000 ints)", CreateLargeList(10000)),
            ("Large Dictionary (5,000 pairs)", CreateLargeDictionary(5000)),
            ("Large Array (20,000 ints)", CreateLargeArray(20000)),
            ("Mixed Collection (1,000 objects)", CreateMixedCollection(1000))
        };

        foreach (var (name, testObject) in testCases)
        {
            Console.WriteLine($"\nTesting: {name}");

            var messagePackResult = MeasurePerformance($"MessagePack_{name}", () => { return _messagePackEstimator.EstimateSize(testObject); });

            var reflectionResult = MeasurePerformance($"Reflection_{name}", () => { return _reflectionEstimator.EstimateSize(testObject); });

            var expressionTreeResult = MeasurePerformance($"ExpressionTree_{name}", () => { return _expressionTreeEstimator.EstimateSize(testObject); });

            var binarySerializationResult = MeasurePerformance($"BinarySerialization_{name}", () => { return _binarySerializationEstimator.EstimateSize(testObject); });

            var sourceGeneratorResult = MeasurePerformance($"SourceGenerator_{name}", () => { return _sourceGeneratorEstimator.EstimateSize(testObject); });


            var bestTime = Math.Min(
                Math.Min(
                    Math.Min(
                        Math.Min(
                            messagePackResult.ExecutionTime.TotalMilliseconds,
                            reflectionResult.ExecutionTime.TotalMilliseconds),
                        expressionTreeResult.ExecutionTime.TotalMilliseconds),
                    binarySerializationResult.ExecutionTime.TotalMilliseconds),
                sourceGeneratorResult.ExecutionTime.TotalMilliseconds);

            var bestMemory = Math.Min(
                Math.Min(
                    Math.Min(
                        Math.Min(
                            messagePackResult.MemoryUsed,
                            reflectionResult.MemoryUsed),
                        expressionTreeResult.MemoryUsed),
                    binarySerializationResult.MemoryUsed),
                sourceGeneratorResult.MemoryUsed);

            var bestTimeName = bestTime == messagePackResult.ExecutionTime.TotalMilliseconds ? "MessagePack" :
                bestTime == expressionTreeResult.ExecutionTime.TotalMilliseconds ? "ExpressionTree" :
                bestTime == binarySerializationResult.ExecutionTime.TotalMilliseconds ? "BinarySerialization" :
                bestTime == sourceGeneratorResult.ExecutionTime.TotalMilliseconds ? "SourceGenerator" :
 "Reflection";

            var bestMemoryName = bestMemory == messagePackResult.MemoryUsed ? "MessagePack" :
                bestMemory == expressionTreeResult.MemoryUsed ? "ExpressionTree" :
                bestMemory == binarySerializationResult.MemoryUsed ? "BinarySerialization" :
                bestMemory == sourceGeneratorResult.MemoryUsed ? "SourceGenerator" :
 "Reflection";

            Console.WriteLine($"  MessagePack:        {messagePackResult.ExecutionTime.TotalMilliseconds:F2}ms, {messagePackResult.MemoryUsed:N0} bytes");
            Console.WriteLine($"  Reflection:         {reflectionResult.ExecutionTime.TotalMilliseconds:F2}ms, {reflectionResult.MemoryUsed:N0} bytes");
            Console.WriteLine($"  ExpressionTree:     {expressionTreeResult.ExecutionTime.TotalMilliseconds:F2}ms, {expressionTreeResult.MemoryUsed:N0} bytes");
            Console.WriteLine($"  BinarySerialization: {binarySerializationResult.ExecutionTime.TotalMilliseconds:F2}ms, {binarySerializationResult.MemoryUsed:N0} bytes");
            Console.WriteLine($"  SourceGenerator:    {sourceGeneratorResult.ExecutionTime.TotalMilliseconds:F2}ms, {sourceGeneratorResult.MemoryUsed:N0} bytes");
            Console.WriteLine($"  🏆 Fastest: {bestTimeName} | 💾 Most Efficient: {bestMemoryName}");

            Assert.That(messagePackResult.Size, Is.GreaterThan(0), $"MessagePack should return positive size for {name}");
            Assert.That(reflectionResult.Size, Is.GreaterThan(0), $"Reflection should return positive size for {name}");
            Assert.That(expressionTreeResult.Size, Is.GreaterThan(0), $"ExpressionTree should return positive size for {name}");
            Assert.That(binarySerializationResult.Size, Is.GreaterThan(0), $"BinarySerialization should return positive size for {name}");
            Assert.That(sourceGeneratorResult.Size, Is.GreaterThan(0), $"SourceGenerator should return positive size for {name}");
        }
    }

    private PerformanceResult MeasurePerformance(string testName, Func<long> operation)
    {
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Get initial memory and GC counts
        var initialMemory = GC.GetTotalMemory(false);
        var initialGC0 = GC.CollectionCount(0);
        var initialGC1 = GC.CollectionCount(1);
        var initialGC2 = GC.CollectionCount(2);

        // Measure execution time
        var stopwatch = Stopwatch.StartNew();
        var result = operation();
        stopwatch.Stop();

        // Get final memory and GC counts
        var finalMemory = GC.GetTotalMemory(false);
        var finalGC0 = GC.CollectionCount(0);
        var finalGC1 = GC.CollectionCount(1);
        var finalGC2 = GC.CollectionCount(2);

        return new PerformanceResult
        {
            Size = result,
            ExecutionTime = stopwatch.Elapsed,
            MemoryUsed = finalMemory - initialMemory,
            GC0 = finalGC0 - initialGC0,
            GC1 = finalGC1 - initialGC1,
            GC2 = finalGC2 - initialGC2
        };
    }

    private object CreateDeeplyNestedObject(int depth)
    {
        if (depth <= 0)
        {
            return new LeafNode
            {
                Value = "Leaf",
                Number = 42,
                Items = new List<string> { "item1", "item2", "item3" }
            };
        }

        return new NestedNode
        {
            Value = $"Level {depth}",
            Number = depth * 10,
            Items = new List<string> { $"item{depth}_1", $"item{depth}_2", $"item{depth}_3" },
            Child = CreateDeeplyNestedObject(depth - 1),
            Sibling = CreateDeeplyNestedObject(depth - 1),
            Metadata = new Dictionary<string, object>
            {
                ["level"] = depth,
                ["timestamp"] = DateTime.Now,
                ["data"] = new { Id = depth, Name = $"Node{depth}" }
            }
        };
    }

    private List<int> CreateLargeList(int count)
    {
        var list = new List<int>();
        for (var i = 0; i < count; i++)
        {
            list.Add(i);
        }

        return list;
    }

    private Dictionary<string, int> CreateLargeDictionary(int count)
    {
        var dict = new Dictionary<string, int>();
        for (var i = 0; i < count; i++)
        {
            dict[$"key_{i}"] = i;
        }

        return dict;
    }

    private int[] CreateLargeArray(int count)
    {
        var array = new int[count];
        for (var i = 0; i < count; i++)
        {
            array[i] = i;
        }

        return array;
    }

    private List<object> CreateMixedCollection(int count)
    {
        var list = new List<object>();
        for (var i = 0; i < count; i++)
        {
            list.Add(
                new
                {
                    Id = i,
                    Name = $"Item{i}",
                    Value = i * 1.5,
                    Items = new List<string> { $"sub{i}_1", $"sub{i}_2" },
                    Metadata = new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["created"] = DateTime.Now.AddMinutes(-i),
                        ["data"] = new { X = i, Y = i * 2, Z = i * 3 }
                    }
                });
        }

        return list;
    }

    private class NestedNode
    {
        public string Value { get; set; } = string.Empty;

        public int Number { get; set; }

        public List<string> Items { get; set; } = new ();

        public object? Child { get; set; }

        public object? Sibling { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new ();
    }

    private class LeafNode
    {
        public string Value { get; set; } = string.Empty;

        public int Number { get; set; }

        public List<string> Items { get; set; } = new ();
    }

    private class PerformanceResult
    {
        public long Size { get; set; }

        public TimeSpan ExecutionTime { get; set; }

        public long MemoryUsed { get; set; }

        public int GC0 { get; set; }

        public int GC1 { get; set; }

        public int GC2 { get; set; }
    }
}
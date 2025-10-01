using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation;

/// <summary>
/// High-performance object size estimator using compiled expression trees with caching.
/// This approach pre-compiles size calculation expressions for each type and caches them,
/// providing near-native performance after the initial compilation cost.
/// </summary>
public class ExpressionTreeCachingEstimator : IObjectSizeEstimator
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<Type, Func<object, long>> _sizeCalculators = new();
    private readonly ConcurrentDictionary<Type, Func<object, long>> _primitiveCalculators = new();
    private const int MAX_RECURSION_DEPTH = 50;
    private const int CACHE_SIZE_LIMIT = 1000; // Prevent memory leaks from too many cached types

    public ExpressionTreeCachingEstimator(ILogger? logger = null)
    {
        _logger = logger;
        
        // Pre-compile calculators for common primitive types
        PrecompilePrimitiveCalculators();
    }

    public long EstimateSize(object instanceToEstimate)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        
        try
        {
            if (instanceToEstimate == null)
            {
                return 0;
            }

            var type = instanceToEstimate.GetType();
            
            // Check if we have a pre-compiled primitive calculator
            if (_primitiveCalculators.TryGetValue(type, out var primitiveCalculator))
            {
                var result = primitiveCalculator(instanceToEstimate);
                LogPerformance(start, "Primitive", result);
                return result;
            }

            // Get or create calculator for this type
            var calculator = GetOrCreateCalculator(type);
            var size = calculator(instanceToEstimate);
            
            LogPerformance(start, type.Name, size);
            return size;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in ExpressionTreeCachingEstimator for type {Type}", 
                instanceToEstimate?.GetType().Name ?? "null");
            
            // Fallback to simple estimation
            return GetFallbackEstimate(instanceToEstimate);
        }
    }

    private Func<object, long> GetOrCreateCalculator(Type type)
    {
        // Check cache first
        if (_sizeCalculators.TryGetValue(type, out var cachedCalculator))
        {
            return cachedCalculator;
        }

        // Prevent cache from growing too large
        if (_sizeCalculators.Count >= CACHE_SIZE_LIMIT)
        {
            _logger?.LogWarning("Cache size limit reached, using fallback estimation for type {Type}", type.Name);
            return GetFallbackCalculator(type);
        }

        // Create new calculator
        var calculator = CreateCalculator(type);
        
        // Try to add to cache (might fail if another thread added it first)
        _sizeCalculators.TryAdd(type, calculator);
        
        return calculator;
    }

    private Func<object, long> CreateCalculator(Type type)
    {
        try
        {
            // Create parameter expression for the object
            var objParam = Expression.Parameter(typeof(object), "obj");
            var typedParam = Expression.Convert(objParam, type);

            // Create the size calculation expression
            var sizeExpression = CreateSizeExpression(typedParam, type, new HashSet<Type>(), 0);

            // Compile the expression
            var lambda = Expression.Lambda<Func<object, long>>(sizeExpression, objParam);
            var compiled = lambda.Compile();

            _logger?.LogDebug("Created compiled calculator for type {Type}", type.Name);
            return compiled;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create calculator for type {Type}, using fallback", type.Name);
            return GetFallbackCalculator(type);
        }
    }

    private Expression CreateSizeExpression(Expression obj, Type type, HashSet<Type> visitedTypes, int depth)
    {
        // Prevent infinite recursion
        if (depth > MAX_RECURSION_DEPTH || visitedTypes.Contains(type))
        {
            return Expression.Constant(0L);
        }

        visitedTypes.Add(type);

        try
        {
            // Handle null check
            var nullCheck = Expression.Equal(obj, Expression.Constant(null, type));
            var nullResult = Expression.Constant(0L);
            
            // Handle different types
            Expression sizeCalculation;

            if (TryGetPrimitiveSize(type, out var primitiveSize))
            {
                sizeCalculation = Expression.Constant((long)primitiveSize);
            }
            else if (type == typeof(string))
            {
                // String: 24 bytes overhead + 2 bytes per character
                var lengthProperty = Expression.Property(obj, "Length");
                var stringSize = Expression.Add(
                    Expression.Constant(24L),
                    Expression.Multiply(Expression.Convert(lengthProperty, typeof(long)), Expression.Constant(2L))
                );
                sizeCalculation = stringSize;
            }
            else if (type.IsArray)
            {
                sizeCalculation = CreateArraySizeExpression(obj, type, visitedTypes, depth);
            }
            else if (typeof(System.Collections.ICollection).IsAssignableFrom(type))
            {
                sizeCalculation = CreateCollectionSizeExpression(obj, type, visitedTypes, depth);
            }
            else if (type.IsValueType)
            {
                sizeCalculation = CreateValueTypeSizeExpression(obj, type, visitedTypes, depth);
            }
            else
            {
                sizeCalculation = CreateReferenceTypeSizeExpression(obj, type, visitedTypes, depth);
            }

            visitedTypes.Remove(type);
            return Expression.Condition(nullCheck, nullResult, sizeCalculation);
        }
        catch
        {
            visitedTypes.Remove(type);
            return Expression.Constant(1000L); // Conservative fallback
        }
    }

    private Expression CreateArraySizeExpression(Expression obj, Type arrayType, HashSet<Type> visitedTypes, int depth)
    {
        // Array overhead + element size * length
        var lengthProperty = Expression.Property(obj, "Length");
        var elementType = arrayType.GetElementType()!;
        
        if (TryGetPrimitiveSize(elementType, out var elementSize))
        {
            var totalElementSize = Expression.Multiply(
                Expression.Convert(lengthProperty, typeof(long)),
                Expression.Constant((long)elementSize)
            );
            return Expression.Add(Expression.Constant(24L), totalElementSize);
        }
        else
        {
            // For non-primitive arrays, create a more accurate estimation
            // We'll use a loop to sum up individual element sizes
            return CreateArrayElementSizeExpression(obj, arrayType, elementType, visitedTypes, depth);
        }
    }

    private Expression CreateArrayElementSizeExpression(Expression obj, Type arrayType, Type elementType, HashSet<Type> visitedTypes, int depth)
    {
        // Create a more sophisticated array size calculation
        // This will estimate based on element type characteristics
        
        var lengthProperty = Expression.Property(obj, "Length");
        var arrayOverhead = Expression.Constant(24L);
        
        if (elementType.IsValueType)
        {
            // For value types, calculate based on field sizes
            var valueTypeSize = CalculateValueTypeSize(elementType);
            var totalElementSize = Expression.Multiply(
                Expression.Convert(lengthProperty, typeof(long)),
                Expression.Constant((long)valueTypeSize)
            );
            return Expression.Add(arrayOverhead, totalElementSize);
        }
        else
        {
            // For reference types, use a more realistic estimate
            // Based on typical object overhead + field estimates
            var referenceTypeSize = EstimateReferenceTypeSize(elementType);
            var totalElementSize = Expression.Multiply(
                Expression.Convert(lengthProperty, typeof(long)),
                Expression.Constant((long)referenceTypeSize)
            );
            return Expression.Add(arrayOverhead, totalElementSize);
        }
    }

    private Expression CreateCollectionSizeExpression(Expression obj, Type collectionType, HashSet<Type> visitedTypes, int depth)
    {
        // Collection overhead + estimated element size * count
        var countProperty = Expression.Property(obj, "Count");
        var collectionOverhead = Expression.Constant(24L);
        
        // Try to determine the element type for more accurate estimation
        var elementType = GetCollectionElementType(collectionType);
        
        if (elementType != null)
        {
            if (TryGetPrimitiveSize(elementType, out var elementSize))
            {
                var totalElementSize = Expression.Multiply(
                    Expression.Convert(countProperty, typeof(long)),
                    Expression.Constant((long)elementSize)
                );
                return Expression.Add(collectionOverhead, totalElementSize);
            }
            else if (elementType.IsValueType)
            {
                var valueTypeSize = CalculateValueTypeSize(elementType);
                var totalElementSize = Expression.Multiply(
                    Expression.Convert(countProperty, typeof(long)),
                    Expression.Constant((long)valueTypeSize)
                );
                return Expression.Add(collectionOverhead, totalElementSize);
            }
            else
            {
                var referenceTypeSize = EstimateReferenceTypeSize(elementType);
                var totalElementSize = Expression.Multiply(
                    Expression.Convert(countProperty, typeof(long)),
                    Expression.Constant((long)referenceTypeSize)
                );
                return Expression.Add(collectionOverhead, totalElementSize);
            }
        }
        else
        {
            // Fallback to more realistic estimate based on collection type
            var estimatedElementSize = GetCollectionTypeElementEstimate(collectionType);
            var totalElementSize = Expression.Multiply(
                Expression.Convert(countProperty, typeof(long)),
                Expression.Constant((long)estimatedElementSize)
            );
            return Expression.Add(collectionOverhead, totalElementSize);
        }
    }

    private Expression CreateValueTypeSizeExpression(Expression obj, Type valueType, HashSet<Type> visitedTypes, int depth)
    {
        // Sum of all field sizes
        var fields = valueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        if (fields.Length == 0)
        {
            return Expression.Constant(0L);
        }

        Expression totalSize = Expression.Constant(0L);
        
        foreach (var field in fields)
        {
            var fieldValue = Expression.Field(obj, field);
            var fieldSize = CreateSizeExpression(fieldValue, field.FieldType, new HashSet<Type>(visitedTypes), depth + 1);
            totalSize = Expression.Add(totalSize, fieldSize);
        }

        return totalSize;
    }

    private Expression CreateReferenceTypeSizeExpression(Expression obj, Type referenceType, HashSet<Type> visitedTypes, int depth)
    {
        // For complex objects, use a hybrid approach that falls back to runtime analysis
        // when the object structure is too complex for compile-time estimation
        
        var fields = referenceType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        // If this is a complex object with many fields or deep nesting, use runtime estimation
        if (fields.Length > 10 || depth > 5)
        {
            // Create a call to runtime estimation method
            var runtimeEstimateMethod = typeof(ExpressionTreeCachingEstimator)
                .GetMethod(nameof(EstimateComplexObjectSize), BindingFlags.NonPublic | BindingFlags.Instance);
            
            return Expression.Call(
                Expression.Constant(this),
                runtimeEstimateMethod!,
                obj,
                Expression.Constant(referenceType),
                Expression.Constant(depth)
            );
        }
        
        // Use more accurate object overhead calculation
        var objectOverhead = Expression.Constant((long)(IntPtr.Size * 3)); // Object header + type pointer + sync block
        Expression totalSize = objectOverhead;
        
        foreach (var field in fields)
        {
            var fieldValue = Expression.Field(obj, field);
            var fieldSize = CreateSizeExpression(fieldValue, field.FieldType, new HashSet<Type>(visitedTypes), depth + 1);
            totalSize = Expression.Add(totalSize, fieldSize);
        }

        // Add padding for alignment
        var alignedSize = Expression.And(
            Expression.Add(totalSize, Expression.Constant(7L)),
            Expression.Constant(~7L)
        );
        
        // Ensure minimum object size
        return Expression.Condition(
            Expression.GreaterThan(alignedSize, Expression.Constant(24L)),
            alignedSize,
            Expression.Constant(24L)
        );
    }

    private void PrecompilePrimitiveCalculators()
    {
        var primitiveTypes = new[]
        {
            typeof(bool), typeof(byte), typeof(sbyte), typeof(char), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double),
            typeof(decimal), typeof(DateTime), typeof(Guid)
        };

        foreach (var type in primitiveTypes)
        {
            if (TryGetPrimitiveSize(type, out var size))
            {
                var calculator = CreatePrimitiveCalculator(type, size);
                _primitiveCalculators.TryAdd(type, calculator);
            }
        }
    }

    private Func<object, long> CreatePrimitiveCalculator(Type type, int size)
    {
        var objParam = Expression.Parameter(typeof(object), "obj");
        var typedParam = Expression.Convert(objParam, type);
        var sizeExpression = Expression.Constant((long)size);
        var lambda = Expression.Lambda<Func<object, long>>(sizeExpression, objParam);
        return lambda.Compile();
    }

    private Func<object, long> GetFallbackCalculator(Type type)
    {
        return obj =>
        {
            if (obj == null) return 0;
            
            // More accurate fallback estimation
            if (TryGetPrimitiveSize(type, out var primitiveSize))
            {
                return primitiveSize;
            }
            
            if (obj is string s)
            {
                return 24 + (s.Length * 2);
            }
            
            if (obj is Array arr)
            {
                var elementType = arr.GetType().GetElementType();
                var elementSize = elementType != null && TryGetPrimitiveSize(elementType, out var es) ? es : 24;
                return 24 + (arr.LongLength * elementSize);
            }
            
            if (obj is System.Collections.ICollection collection)
            {
                var elementType = GetCollectionElementType(type);
                var elementSize = elementType != null && TryGetPrimitiveSize(elementType, out var es) ? es : 32;
                return 24 + (collection.Count * elementSize);
            }
            
            // For complex objects, use more realistic estimation
            if (type.IsValueType)
            {
                return CalculateValueTypeSize(type);
            }
            else
            {
                return EstimateReferenceTypeSize(type);
            }
        };
    }

    private long GetFallbackEstimate(object obj)
    {
        if (obj == null) return 0;
        
        var type = obj.GetType();
        
        if (TryGetPrimitiveSize(type, out var primitiveSize))
        {
            return primitiveSize;
        }
        
        if (obj is string s)
        {
            return 24 + (s.Length * 2);
        }
        
        if (obj is Array arr)
        {
            var elementType = arr.GetType().GetElementType();
            var elementSize = elementType != null && TryGetPrimitiveSize(elementType, out var es) ? es : 24;
            return 24 + (arr.LongLength * elementSize);
        }
        
        if (obj is System.Collections.ICollection collection)
        {
            var elementType = GetCollectionElementType(type);
            var elementSize = elementType != null && TryGetPrimitiveSize(elementType, out var es) ? es : 32;
            return 24 + (collection.Count * elementSize);
        }
        
        // For complex objects, use more realistic estimation
        if (type.IsValueType)
        {
            return CalculateValueTypeSize(type);
        }
        else
        {
            return EstimateReferenceTypeSize(type);
        }
    }

    private void LogPerformance(long start, string typeName, long size)
    {
        var end = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsedMs = (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _logger?.LogInformation("ExpressionTree estimated size for {Type} in {ElapsedMs:F3} ms: {Size} bytes", 
            typeName, elapsedMs, size);
    }

    private static bool TryGetPrimitiveSize(Type type, out int size)
    {
        // Handle common primitives and well-known structs
        if (type.IsEnum)
        {
            size = System.Runtime.InteropServices.Marshal.SizeOf(Enum.GetUnderlyingType(type));
            return true;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean: size = 1; return true;
            case TypeCode.Byte: size = 1; return true;
            case TypeCode.SByte: size = 1; return true;
            case TypeCode.Char: size = 2; return true;
            case TypeCode.Int16: size = 2; return true;
            case TypeCode.UInt16: size = 2; return true;
            case TypeCode.Int32: size = 4; return true;
            case TypeCode.UInt32: size = 4; return true;
            case TypeCode.Int64: size = 8; return true;
            case TypeCode.UInt64: size = 8; return true;
            case TypeCode.Single: size = 4; return true;
            case TypeCode.Double: size = 8; return true;
            case TypeCode.Decimal: size = 16; return true;
            case TypeCode.DateTime: size = 8; return true;
        }

        if (type == typeof(Guid))
        {
            size = 16;
            return true;
        }

        size = 0;
        return false;
    }

    private int CalculateValueTypeSize(Type valueType)
    {
        if (TryGetPrimitiveSize(valueType, out var primitiveSize))
        {
            return primitiveSize;
        }

        // Calculate size based on fields
        var fields = valueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var totalSize = 0;

        foreach (var field in fields)
        {
            if (TryGetPrimitiveSize(field.FieldType, out var fieldSize))
            {
                totalSize += fieldSize;
            }
            else if (field.FieldType.IsValueType)
            {
                totalSize += CalculateValueTypeSize(field.FieldType);
            }
            else
            {
                // Reference type field in value type - just pointer size
                totalSize += IntPtr.Size;
            }
        }

        return Math.Max(totalSize, 1); // At least 1 byte
    }

    private int EstimateReferenceTypeSize(Type referenceType)
    {
        // Object overhead (24 bytes on 64-bit, 12 bytes on 32-bit)
        var objectOverhead = IntPtr.Size * 3; // Object header + type pointer + sync block
        
        var fields = referenceType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var totalSize = objectOverhead;

        foreach (var field in fields)
        {
            if (TryGetPrimitiveSize(field.FieldType, out var fieldSize))
            {
                totalSize += fieldSize;
            }
            else if (field.FieldType.IsValueType)
            {
                totalSize += CalculateValueTypeSize(field.FieldType);
            }
            else
            {
                // Reference type field - just pointer size
                totalSize += IntPtr.Size;
            }
        }

        // Add some padding for alignment
        totalSize = (totalSize + 7) & ~7; // Align to 8-byte boundary

        return Math.Max(totalSize, 24); // Minimum object size
    }

    private Type? GetCollectionElementType(Type collectionType)
    {
        // Try to get the element type from generic collections
        if (collectionType.IsGenericType)
        {
            var genericArgs = collectionType.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                return genericArgs[0];
            }
        }

        // Try to get element type from array
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        // Try to get element type from IEnumerable<T>
        var interfaces = collectionType.GetInterfaces();
        foreach (var iface in interfaces)
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IEnumerable<>))
            {
                return iface.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private int GetCollectionTypeElementEstimate(Type collectionType)
    {
        // Provide more realistic estimates based on common collection types
        if (collectionType.IsGenericType)
        {
            var genericTypeDef = collectionType.GetGenericTypeDefinition();
            
            if (genericTypeDef == typeof(System.Collections.Generic.List<>) ||
                genericTypeDef == typeof(System.Collections.Generic.IList<>) ||
                genericTypeDef == typeof(System.Collections.Generic.ICollection<>))
            {
                return 32; // List overhead per element
            }
            
            if (genericTypeDef == typeof(System.Collections.Generic.Dictionary<,>) ||
                genericTypeDef == typeof(System.Collections.Generic.IDictionary<,>))
            {
                return 48; // Dictionary entry overhead
            }
            
            if (genericTypeDef == typeof(System.Collections.Generic.HashSet<>) ||
                genericTypeDef == typeof(System.Collections.Generic.ISet<>))
            {
                return 40; // HashSet entry overhead
            }
        }

        // Default estimate for unknown collections
        return 32;
    }

    private long EstimateComplexObjectSize(object obj, Type objectType, int depth)
    {
        if (obj == null) return 0;
        
        // Use a more sophisticated runtime estimation for complex objects
        var visited = new HashSet<object>();
        return EstimateComplexObjectSizeRecursive(obj, objectType, visited, depth);
    }

    private long EstimateComplexObjectSizeRecursive(object obj, Type objectType, HashSet<object> visited, int depth)
    {
        if (obj == null || depth > MAX_RECURSION_DEPTH || visited.Contains(obj))
        {
            return 0;
        }

        visited.Add(obj);

        try
        {
            // Handle primitives
            if (TryGetPrimitiveSize(objectType, out var primitiveSize))
            {
                return primitiveSize;
            }

            // Handle strings
            if (obj is string str)
            {
                return 24 + (str.Length * 2);
            }

            // Handle arrays
            if (obj is Array arr)
            {
                var elementType = arr.GetType().GetElementType();
                var elementSize = elementType != null ? EstimateComplexObjectSizeRecursive(arr.GetValue(0)!, elementType, new HashSet<object>(visited), depth + 1) : 24;
                return 24 + (arr.LongLength * elementSize);
            }

            // Handle collections
            if (obj is System.Collections.ICollection collection)
            {
                var elementType = GetCollectionElementType(objectType);
                var elementSize = elementType != null ? EstimateComplexObjectSizeRecursive(collection.Cast<object>().FirstOrDefault()!, elementType, new HashSet<object>(visited), depth + 1) : 32;
                return 24 + (collection.Count * elementSize);
            }

            // Handle value types
            if (objectType.IsValueType)
            {
                return CalculateValueTypeSize(objectType);
            }

            // Handle reference types with deep analysis
            var fields = objectType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var totalSize = (long)(IntPtr.Size * 3); // Object overhead

            foreach (var field in fields)
            {
                try
                {
                    var fieldValue = field.GetValue(obj);
                    if (fieldValue != null)
                    {
                        var fieldSize = EstimateComplexObjectSizeRecursive(fieldValue, field.FieldType, new HashSet<object>(visited), depth + 1);
                        totalSize += fieldSize;
                    }
                    else if (field.FieldType.IsValueType)
                    {
                        // Null value type - just the size of the value type
                        totalSize += CalculateValueTypeSize(field.FieldType);
                    }
                    else
                    {
                        // Null reference - just pointer size
                        totalSize += IntPtr.Size;
                    }
                }
                catch
                {
                    // If we can't access the field, use a conservative estimate
                    totalSize += EstimateReferenceTypeSize(field.FieldType);
                }
            }

            // Add padding for alignment
            totalSize = (totalSize + 7) & ~7;

            return Math.Max(totalSize, 24);
        }
        finally
        {
            visited.Remove(obj);
        }
    }
}

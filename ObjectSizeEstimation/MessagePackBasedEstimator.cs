using MessagePack;
using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation;

public class MessagePackBasedEstimator : IObjectSizeEstimator
{
    private readonly ILogger? _logger;
    private readonly MessagePackSerializerOptions _serializerOptions;
    private const int MAX_RECURSION_DEPTH = 50; // Prevent stack overflow from deeply nested structures

    public MessagePackBasedEstimator(ILogger? logger = null, MessagePackSerializerOptions? serializerOptions = null)
    {
        _logger = logger;
        _serializerOptions = serializerOptions ?? MessagePackSerializerOptions.Standard;
    }

    public long EstimateSize(object instanceToEstimate)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        
        try
        {
            // Check for null input
            if (instanceToEstimate == null)
            {
                return 0;
            }

            // For complex objects, always use safe fallback to prevent stack overflow
            if (IsComplexObject(instanceToEstimate))
            {
                _logger?.LogInformation("Object of type {ObjectType} is complex. Using safe fallback estimation to prevent stack overflow.", 
                                    instanceToEstimate.GetType().Name);
                return EstimateSizeSafely(instanceToEstimate);
            }

            // Use a very short timeout for MessagePack serialization to prevent stack overflow
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1)); // 1 second timeout
            var task = Task.Run(() => MessagePackSerializer.Serialize(instanceToEstimate, _serializerOptions), cancellationTokenSource.Token);
            
            if (task.Wait(TimeSpan.FromSeconds(1)))
            {
                var serializedBytes = task.Result;
                var size = serializedBytes.Length;
                
                var end = System.Diagnostics.Stopwatch.GetTimestamp();
                var elapsedMs = (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                _logger?.LogInformation("MessagePack estimated size in {ElapsedMs:F3} ms: {Size} bytes", elapsedMs, size);
                
                return size;
            }
            else
            {
                _logger?.LogWarning("MessagePack serialization timed out for object of type {ObjectType}. Using safe fallback estimation.", 
                                    instanceToEstimate.GetType().Name);
                return EstimateSizeSafely(instanceToEstimate);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("MessagePack serialization was cancelled for object of type {ObjectType}. Using safe fallback estimation.", 
                                instanceToEstimate?.GetType().Name ?? "null");
            
            // Use safe fallback estimation
            return EstimateSizeSafely(instanceToEstimate!);
        }
        catch (MessagePackSerializationException ex)
        {
            _logger?.LogWarning("MessagePack serialization failed for object of type {ObjectType}: {Error}. Using safe fallback estimation.", 
                                instanceToEstimate?.GetType().Name ?? "null", ex.Message);
            
            // Use safe fallback estimation instead of potentially problematic reflection-based estimation
            return EstimateSizeSafely(instanceToEstimate!);
        }
        catch (StackOverflowException)
        {
            _logger?.LogError("Stack overflow detected during MessagePack serialization for object of type {ObjectType}. Using safe fallback estimation.", 
                              instanceToEstimate?.GetType().Name ?? "null");
            
            // Use safe fallback estimation
            return EstimateSizeSafely(instanceToEstimate!);
        }
        catch (OutOfMemoryException ex)
        {
            _logger?.LogError(ex, "Out of memory during MessagePack serialization for object of type {ObjectType}. Using safe fallback estimation.", 
                              instanceToEstimate?.GetType().Name ?? "null");
            
            // Use safe fallback estimation
            return EstimateSizeSafely(instanceToEstimate!);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during MessagePack serialization for object of type {ObjectType}. Using safe fallback estimation.", 
                              instanceToEstimate?.GetType().Name ?? "null");
            
            // Use safe fallback estimation
            return EstimateSizeSafely(instanceToEstimate!);
        }
    }

    private bool IsComplexObject(object obj)
    {
        if (obj == null) return false;

        var type = obj.GetType();

        // Always consider non-primitive, non-string objects as potentially complex
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
        {
            return false;
        }

        // Check for collections and arrays - these are often complex
        if (obj is System.Collections.ICollection || obj is Array)
        {
            return true;
        }

        // Check for objects with many fields
        var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (fields.Length > 5) // Objects with more than 5 fields are considered complex
        {
            return true;
        }

        // Check for objects that might have nested structures
        foreach (var field in fields)
        {
            try
            {
                var fieldValue = field.GetValue(obj);
                if (fieldValue != null && !field.FieldType.IsPrimitive && field.FieldType != typeof(string) && !field.FieldType.IsEnum)
                {
                    return true; // Has non-primitive fields
                }
            }
            catch
            {
                // If we can't access the field, assume it's complex
                return true;
            }
        }

        return false;
    }

    private long EstimateSizeSafely(object obj)
    {
        if (obj == null) return 0;

        try
        {
            // Use a simple, safe estimation approach that won't cause stack overflow
            return EstimateSizeWithDepthLimit(obj, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
        }
        catch (StackOverflowException)
        {
            _logger?.LogError("Stack overflow in safe estimation fallback. Returning conservative estimate.");
            return GetConservativeEstimate(obj);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in safe estimation fallback. Returning conservative estimate.");
            return GetConservativeEstimate(obj);
        }
    }

    private long EstimateSizeWithDepthLimit(object? obj, HashSet<object> visited, int depth)
    {
        if (obj == null || depth > MAX_RECURSION_DEPTH)
        {
            return 0;
        }

        var type = obj.GetType();

        // Avoid double-counting reference types
        if (!type.IsValueType)
        {
            if (!visited.Add(obj))
            {
                return 0; // Circular reference detected
            }
        }

        long size = 0;

        // Handle primitives
        if (TryGetPrimitiveSize(type, out var primitiveSize))
        {
            size = primitiveSize;
        }
        // Handle strings
        else if (obj is string s)
        {
            size = 24 + (s.Length * 2); // 24 bytes overhead + 2 bytes per char
        }
        // Handle arrays
        else if (obj is Array arr)
        {
            size = 24; // Array overhead
            var elementType = type.GetElementType();
            if (elementType != null && TryGetPrimitiveSize(elementType, out var elementPrimitiveSize))
            {
                size += arr.LongLength * elementPrimitiveSize;
            }
            else
            {
                // For non-primitive arrays, limit the number of elements we process
                var maxElements = Math.Min(arr.LongLength, 1000);
                for (long i = 0; i < maxElements; i++)
                {
                    size += EstimateSizeWithDepthLimit(arr.GetValue(i), visited, depth + 1);
                }
                // Add estimated size for remaining elements
                if (arr.LongLength > maxElements)
                {
                    size += (arr.LongLength - maxElements) * 24; // Conservative estimate for remaining elements
                }
            }
        }
        // Handle collections
        else if (obj is System.Collections.ICollection collection)
        {
            size = 24; // Collection overhead
            var count = Math.Min(collection.Count, 1000); // Limit processing to prevent stack overflow
            var enumerator = collection.GetEnumerator();
            var processed = 0;
            while (enumerator.MoveNext() && processed < count)
            {
                size += EstimateSizeWithDepthLimit(enumerator.Current, visited, depth + 1);
                processed++;
            }
            // Add estimated size for remaining elements
            if (collection.Count > count)
            {
                size += (collection.Count - count) * 24; // Conservative estimate
            }
        }
        // Handle value types
        else if (type.IsValueType)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(obj);
                size += EstimateSizeWithDepthLimit(fieldValue, visited, depth + 1);
            }
        }
        // Handle reference types
        else
        {
            size = 24; // Object overhead
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(obj);
                size += EstimateSizeWithDepthLimit(fieldValue, visited, depth + 1);
            }
        }

        return size;
    }

    private long GetConservativeEstimate(object obj)
    {
        if (obj == null) return 0;

        var type = obj.GetType();

        // Very conservative estimates to avoid any computation that might cause issues
        if (TryGetPrimitiveSize(type, out var primitiveSize))
        {
            return primitiveSize;
        }

        if (obj is string s)
        {
            return Math.Min(24 + (s.Length * 2), 10000); // Cap string size estimation
        }

        if (obj is System.Collections.ICollection collection)
        {
            return Math.Min(24 + (collection.Count * 24), 100000); // Cap collection size estimation
        }

        if (obj is Array arr)
        {
            return Math.Min(24 + (arr.LongLength * 24), 100000); // Cap array size estimation
        }

        // For any other object, return a conservative estimate
        return 1000;
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
}
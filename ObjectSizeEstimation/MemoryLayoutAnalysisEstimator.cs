using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation;

/// <summary>
/// Object size estimator based on Memory Layout Analysis.
/// This approach analyzes the actual memory layout of objects using unsafe code and reflection,
/// providing highly accurate size estimates based on the real memory footprint.
/// </summary>
public unsafe class MemoryLayoutAnalysisEstimator : IObjectSizeEstimator
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<Type, long> _typeSizeCache = new();
    private readonly ConcurrentDictionary<Type, FieldInfo[]> _fieldCache = new();
    private const int MAX_RECURSION_DEPTH = 50;
    private const int CACHE_SIZE_LIMIT = 1000;

    public MemoryLayoutAnalysisEstimator(ILogger? logger = null)
    {
        _logger = logger;
    }

    public long EstimateSize(object? obj)
    {
        if (obj == null)
        {
            _logger?.LogDebug("MemoryLayout: null object, size = 0");
            return 0;
        }

        var type = obj.GetType();
        _logger?.LogDebug("MemoryLayout: Starting size estimation for {ObjectType}", type.Name);

        try
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var size = AnalyzeMemoryLayout(obj, type, visited, 0);
            _logger?.LogDebug("MemoryLayout: Estimated size for {ObjectType} = {Size} bytes", type.Name, size);
            return size;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "MemoryLayout: Failed to estimate size for {ObjectType}, falling back to reflection-based estimation", type.Name);
            return EstimateSizeWithReflection(obj);
        }
    }

    private long AnalyzeMemoryLayout(object obj, Type objectType, HashSet<object> visited, int depth)
    {
        if (obj == null || visited.Contains(obj) || depth >= MAX_RECURSION_DEPTH)
        {
            return 0;
        }

        visited.Add(obj);

        try
        {
            // Handle primitive types
            if (TryGetPrimitiveSize(objectType, out var primitiveSize))
            {
                return primitiveSize;
            }

            // Handle strings
            if (objectType == typeof(string))
            {
                return AnalyzeStringLayout((string)obj);
            }

            // Handle arrays
            if (objectType.IsArray)
            {
                return AnalyzeArrayLayout((Array)obj, visited, depth);
            }

            // Handle collections
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(objectType) && objectType != typeof(string))
            {
                return AnalyzeCollectionLayout(obj, objectType, visited, depth);
            }

            // Handle value types
            if (objectType.IsValueType)
            {
                return AnalyzeValueTypeLayout(obj, objectType);
            }

            // Handle reference types
            return AnalyzeReferenceTypeLayout(obj, objectType, visited, depth);
        }
        finally
        {
            visited.Remove(obj);
        }
    }

    private long AnalyzeStringLayout(string str)
    {
        if (str == null) return 0;
        
        // String layout: object header + length + characters
        // Object header: IntPtr.Size * 2 (sync block + type handle)
        // Length: 4 bytes
        // Characters: length * 2 bytes (UTF-16)
        return (IntPtr.Size * 2) + 4 + (str.Length * 2);
    }

    private long AnalyzeArrayLayout(Array array, HashSet<object> visited, int depth)
    {
        if (array == null) return 0;

        var elementType = array.GetType().GetElementType();
        if (elementType == null) return 0;

        // Array header: sync block + type handle + length + bounds
        var headerSize = (IntPtr.Size * 2) + 4; // sync block + type handle + length
        
        // Add bounds for multi-dimensional arrays
        var rank = array.Rank;
        if (rank > 1)
        {
            headerSize += rank * 4; // bounds for each dimension
        }

        // Calculate element size
        long elementSize;
        if (TryGetPrimitiveSize(elementType, out var primitiveSize))
        {
            elementSize = primitiveSize;
        }
        else
        {
            // For reference types, analyze the first element
            if (array.Length > 0)
            {
                var firstElement = array.GetValue(0);
                if (firstElement != null)
                {
                    elementSize = AnalyzeMemoryLayout(firstElement, elementType, new HashSet<object>(visited), depth + 1);
                }
                else
                {
                    elementSize = IntPtr.Size; // Reference size
                }
            }
            else
            {
                elementSize = IntPtr.Size; // Default reference size
            }
        }

        return headerSize + (array.LongLength * elementSize);
    }

    private long AnalyzeCollectionLayout(object collection, Type collectionType, HashSet<object> visited, int depth)
    {
        if (collection == null) return 0;

        // Base collection overhead
        long totalSize = (IntPtr.Size * 2) + 4; // sync block + type handle + count

        // Analyze collection elements
        if (collection is System.Collections.IEnumerable enumerable)
        {
            var elementType = GetCollectionElementType(collectionType);
            
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    var itemSize = elementType != null 
                        ? AnalyzeMemoryLayout(item, elementType, new HashSet<object>(visited), depth + 1)
                        : AnalyzeMemoryLayout(item, item.GetType(), new HashSet<object>(visited), depth + 1);
                    totalSize += itemSize;
                }
                else if (elementType != null && elementType.IsValueType)
                {
                    totalSize += CalculateValueTypeSize(elementType);
                }
                else
                {
                    totalSize += IntPtr.Size; // Null reference
                }
            }
        }

        return totalSize;
    }

    private long AnalyzeValueTypeLayout(object obj, Type valueType)
    {
        if (obj == null) return 0;

        // Use Marshal.SizeOf for value types when possible
        try
        {
            if (TryGetPrimitiveSize(valueType, out var primitiveSize))
            {
                return primitiveSize;
            }

            // For complex value types, analyze fields
            var fields = GetOrCacheFields(valueType);
            var totalSize = 0L;

            foreach (var field in fields)
            {
                try
                {
                    var fieldValue = field.GetValue(obj);
                    if (fieldValue != null)
                    {
                        totalSize += AnalyzeMemoryLayout(fieldValue, field.FieldType, new HashSet<object>(), 0);
                    }
                    else if (field.FieldType.IsValueType)
                    {
                        totalSize += CalculateValueTypeSize(field.FieldType);
                    }
                }
                catch
                {
                    // Fallback for inaccessible fields
                    totalSize += EstimateFieldSize(field.FieldType);
                }
            }

            // Add padding for alignment
            totalSize = (totalSize + 7) & ~7;
            return Math.Max(totalSize, 1);
        }
        catch
        {
            return EstimateValueTypeSize(valueType);
        }
    }

    private long AnalyzeReferenceTypeLayout(object obj, Type referenceType, HashSet<object> visited, int depth)
    {
        if (obj == null) return 0;

        // Object header: sync block + type handle
        long totalSize = (IntPtr.Size * 2);

        // Analyze instance fields
        var fields = GetOrCacheFields(referenceType);
        
        foreach (var field in fields)
        {
            try
            {
                var fieldValue = field.GetValue(obj);
                if (fieldValue != null)
                {
                    var fieldSize = AnalyzeMemoryLayout(fieldValue, field.FieldType, new HashSet<object>(visited), depth + 1);
                    totalSize += fieldSize;
                }
                else if (field.FieldType.IsValueType)
                {
                    totalSize += CalculateValueTypeSize(field.FieldType);
                }
                else
                {
                    totalSize += IntPtr.Size; // Null reference
                }
            }
            catch
            {
                // Fallback for inaccessible fields
                totalSize += EstimateFieldSize(field.FieldType);
            }
        }

        // Add padding for alignment
        totalSize = (totalSize + 7) & ~7;

        // Ensure minimum object size
        return Math.Max(totalSize, 24);
    }

    private FieldInfo[] GetOrCacheFields(Type type)
    {
        if (_fieldCache.Count >= CACHE_SIZE_LIMIT)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        return _fieldCache.GetOrAdd(type, t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }

    private bool TryGetPrimitiveSize(Type type, out long size)
    {
        size = type switch
        {
            _ when type == typeof(bool) => 1,
            _ when type == typeof(byte) => 1,
            _ when type == typeof(sbyte) => 1,
            _ when type == typeof(char) => 2,
            _ when type == typeof(short) => 2,
            _ when type == typeof(ushort) => 2,
            _ when type == typeof(int) => 4,
            _ when type == typeof(uint) => 4,
            _ when type == typeof(long) => 8,
            _ when type == typeof(ulong) => 8,
            _ when type == typeof(float) => 4,
            _ when type == typeof(double) => 8,
            _ when type == typeof(decimal) => 16,
            _ when type == typeof(DateTime) => 8,
            _ when type == typeof(TimeSpan) => 8,
            _ when type == typeof(Guid) => 16,
            _ when type == typeof(IntPtr) => IntPtr.Size,
            _ when type == typeof(UIntPtr) => IntPtr.Size,
            _ => 0
        };

        return size > 0;
    }

    private long CalculateValueTypeSize(Type valueType)
    {
        if (TryGetPrimitiveSize(valueType, out var primitiveSize))
        {
            return primitiveSize;
        }

        try
        {
            return Marshal.SizeOf(valueType);
        }
        catch
        {
            return EstimateValueTypeSize(valueType);
        }
    }

    private long EstimateValueTypeSize(Type valueType)
    {
        var fields = GetOrCacheFields(valueType);
        var totalSize = 0L;

        foreach (var field in fields)
        {
            if (TryGetPrimitiveSize(field.FieldType, out var fieldSize))
            {
                totalSize += fieldSize;
            }
            else
            {
                totalSize += 8; // Conservative estimate
            }
        }

        return Math.Max(totalSize, 1);
    }

    private long EstimateFieldSize(Type fieldType)
    {
        if (TryGetPrimitiveSize(fieldType, out var primitiveSize))
        {
            return primitiveSize;
        }

        if (fieldType.IsValueType)
        {
            return CalculateValueTypeSize(fieldType);
        }

        return IntPtr.Size; // Reference size
    }

    private Type? GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        if (collectionType.IsGenericType)
        {
            var genericArgs = collectionType.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                return genericArgs[0];
            }
        }

        return null;
    }

    private long EstimateSizeWithReflection(object obj)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return EstimateSizeWithReflectionRecursive(obj, obj.GetType(), visited, 0);
    }

    private long EstimateSizeWithReflectionRecursive(object obj, Type objectType, HashSet<object> visited, int depth)
    {
        if (obj == null || visited.Contains(obj) || depth >= MAX_RECURSION_DEPTH)
        {
            return 0;
        }

        visited.Add(obj);

        try
        {
            // Handle primitive types and strings
            if (TryGetPrimitiveSize(objectType, out var primitiveSize))
            {
                return primitiveSize;
            }

            if (obj is string str)
            {
                return 24 + (str.Length * 2);
            }

            // Handle arrays
            if (obj is Array arr)
            {
                var elementType = arr.GetType().GetElementType();
                var elementSize = 24L; // Default element size

                if (elementType != null && arr.Length > 0)
                {
                    try
                    {
                        elementSize = EstimateSizeWithReflectionRecursive(arr.GetValue(0)!, elementType, new HashSet<object>(visited), depth + 1);
                    }
                    catch
                    {
                        elementSize = 24L; // Fallback to default
                    }
                }

                return 24 + (arr.LongLength * elementSize);
            }

            // Handle collections
            if (obj is System.Collections.IEnumerable enumerable && obj.GetType() != typeof(string))
            {
                var elementType = GetCollectionElementType(objectType);
                var collectionTotalSize = 24L;

                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        var itemSize = elementType != null ? EstimateSizeWithReflectionRecursive(item, elementType, new HashSet<object>(visited), depth + 1) : 24;
                        collectionTotalSize += itemSize;
                    }
                }

                return collectionTotalSize;
            }

            // Handle value types
            if (objectType.IsValueType)
            {
                return CalculateValueTypeSize(objectType);
            }

            // Handle reference types
            var fields = objectType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var totalSize = (long)(IntPtr.Size * 3); // Object overhead

            foreach (var field in fields)
            {
                try
                {
                    var fieldValue = field.GetValue(obj);
                    if (fieldValue != null)
                    {
                        var fieldSize = EstimateSizeWithReflectionRecursive(fieldValue, field.FieldType, new HashSet<object>(visited), depth + 1);
                        totalSize += fieldSize;
                    }
                    else if (field.FieldType.IsValueType)
                    {
                        totalSize += CalculateValueTypeSize(field.FieldType);
                    }
                    else
                    {
                        totalSize += IntPtr.Size;
                    }
                }
                catch
                {
                    totalSize += EstimateFieldSize(field.FieldType);
                }
            }

            return totalSize;
        }
        finally
        {
            visited.Remove(obj);
        }
    }
}

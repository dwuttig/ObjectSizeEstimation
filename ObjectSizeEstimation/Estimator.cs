using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Generic;
namespace ObjectSizeEstimation;

public class Estimator
{
    private readonly ILogger? _logger;

    public Estimator(ILogger? logger = null)
    {
        _logger = logger;
    }

    public long EstimateSize(object instanceToEstimate)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var size = EstimateObjectSize(instanceToEstimate, new HashSet<object>(ReferenceEqualityComparer.Instance));
        var end = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsedMs = (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _logger?.LogInformation("Estimated size in {ElapsedMs:F3} ms: {Size} bytes", elapsedMs, size);
        return size;
    }

    private long EstimateObjectSize(object? obj, HashSet<object> visited)
    {
        if (obj is null)
        {
            return 0;
        }

        var type = obj.GetType();

        // Avoid double-counting reference types
        if (!type.IsValueType)
        {
            if (!visited.Add(obj))
            {
                return 0;
            }
        }

        long size = 0;

        // Primitives and well-known value types
        if (TryGetPrimitiveSize(type, out var primitiveSize))
        {
            size = primitiveSize;
        }
        // Strings
        else if (obj is string s)
        {
            // Naive: 24 bytes overhead + 2 bytes per char
            size = 24 + (s.Length * 2);
        }
        // Arrays
        else if (obj is Array arr)
        {
            size = 24; // naive array overhead
            var elementType = type.GetElementType();
            if (elementType is not null && TryGetPrimitiveSize(elementType, out var elementPrimitiveSize))
            {
                size += arr.LongLength * elementPrimitiveSize;
            }
            else
            {
                foreach (var item in arr)
                {
                    size += EstimateObjectSize(item, visited);
                }
            }
        }
        // Collections - handle various collection types
        else if (TryEstimateCollectionSize(obj, type, visited, out var collectionSize))
        {
            size = collectionSize;
        }
        // Value types (structs): sum fields
        else if (type.IsValueType)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(obj);
                size += EstimateObjectSize(fieldValue, visited);
            }
        }
        // Reference types (classes): naive object header + fields
        else
        {
            size = 24; // naive object header
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(obj);
                size += EstimateObjectSize(fieldValue, visited);
            }
        }

        // Log the object name and size
        var objectName = GetObjectName(obj, type);
        _logger?.LogInformation("Estimated object: {ObjectName} = {Size} bytes", objectName, size);

        return size;
    }

    private string GetObjectName(object obj, Type type)
    {
        // For primitives, show the value
        if (TryGetPrimitiveSize(type, out _))
        {
            return $"{type.Name}({obj})";
        }

        // For strings, show truncated content
        if (obj is string s)
        {
            var truncated = s.Length > 50 ? s.Substring(0, 50) + "..." : s;
            return $"String[{s.Length}](\"{truncated}\")";
        }

        // For arrays, show type and length
        if (obj is Array arr)
        {
            return $"{type.Name}[{arr.Length}]";
        }

        // For collections, show type and count
        if (obj is ICollection collection)
        {
            return $"{type.Name}[{collection.Count}]";
        }

        // For dictionaries, show type and count
        if (obj is IDictionary dictionary)
        {
            return $"{type.Name}[{dictionary.Count}]";
        }

        // For other objects, show the type name
        return type.Name;
    }

    private bool TryEstimateCollectionSize(object obj, Type type, HashSet<object> visited, out long size)
    {
        size = 0;

        // Handle generic collections
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            
            // List<T>, HashSet<T>, Queue<T>, Stack<T>, etc.
            if (genericTypeDef == typeof(List<>) || 
                genericTypeDef == typeof(HashSet<>) ||
                genericTypeDef == typeof(Queue<>) ||
                genericTypeDef == typeof(Stack<>) ||
                genericTypeDef == typeof(LinkedList<>) ||
                genericTypeDef == typeof(SortedSet<>))
            {
                size = EstimateGenericCollectionSize(obj, type, visited);
                return true;
            }

            // Dictionary<TKey, TValue>, SortedDictionary<TKey, TValue>, etc.
            if (genericTypeDef == typeof(Dictionary<,>) ||
                genericTypeDef == typeof(SortedDictionary<,>) ||
                genericTypeDef == typeof(SortedList<,>))
            {
                size = EstimateDictionarySize(obj, type, visited);
                return true;
            }
        }

        // Handle non-generic collections
        if (obj is ICollection collection)
        {
            size = EstimateNonGenericCollectionSize(collection, visited);
            return true;
        }

        // Handle IDictionary
        if (obj is IDictionary dictionary)
        {
            size = EstimateNonGenericDictionarySize(dictionary, visited);
            return true;
        }

        return false;
    }

    private long EstimateGenericCollectionSize(object obj, Type type, HashSet<object> visited)
    {
        long size = 24; // collection object overhead
        
        var elementType = type.GetGenericArguments()[0];
        var countProperty = type.GetProperty("Count");
        var count = (int)(countProperty?.GetValue(obj) ?? 0);

        if (TryGetPrimitiveSize(elementType, out var elementPrimitiveSize))
        {
            size += (long)count * elementPrimitiveSize;
        }
        else
        {
            // For non-primitive elements, we need to iterate
            if (obj is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    size += EstimateObjectSize(item, visited);
                }
            }
        }

        return size;
    }

    private long EstimateDictionarySize(object obj, Type type, HashSet<object> visited)
    {
        long size = 24; // dictionary object overhead
        
        var keyType = type.GetGenericArguments()[0];
        var valueType = type.GetGenericArguments()[1];

        // Check if both key and value are primitives for optimized calculation
        bool keyIsPrimitive = TryGetPrimitiveSize(keyType, out var keyPrimitiveSize);
        bool valueIsPrimitive = TryGetPrimitiveSize(valueType, out var valuePrimitiveSize);

        if (keyIsPrimitive && valueIsPrimitive)
        {
            // Both are primitives - use count-based calculation for efficiency
            var countProperty = type.GetProperty("Count");
            var count = (int)(countProperty?.GetValue(obj) ?? 0);
            size += (long)count * (keyPrimitiveSize + valuePrimitiveSize);
        }
        else
        {
            // At least one is non-primitive - iterate through all entries for accurate estimation
            if (obj is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    // This is a KeyValuePair, we need to extract the key and value
                    var kvpType = item.GetType();
                    var keyProperty = kvpType.GetProperty("Key");
                    var valueProperty = kvpType.GetProperty("Value");
                    
                    if (keyProperty != null && valueProperty != null)
                    {
                        var key = keyProperty.GetValue(item);
                        var value = valueProperty.GetValue(item);
                        
                        // Estimate key size
                        if (keyIsPrimitive)
                        {
                            size += keyPrimitiveSize;
                        }
                        else
                        {
                            size += EstimateObjectSize(key, visited);
                        }
                        
                        // Estimate value size
                        if (valueIsPrimitive)
                        {
                            size += valuePrimitiveSize;
                        }
                        else
                        {
                            size += EstimateObjectSize(value, visited);
                        }
                    }
                }
            }
        }

        return size;
    }

    private long EstimateNonGenericCollectionSize(ICollection collection, HashSet<object> visited)
    {
        long size = 24; // collection object overhead
        
        foreach (var item in collection)
        {
            size += EstimateObjectSize(item, visited);
        }

        return size;
    }

    private long EstimateNonGenericDictionarySize(IDictionary dictionary, HashSet<object> visited)
    {
        long size = 24; // dictionary object overhead
        
        foreach (DictionaryEntry entry in dictionary)
        {
            size += EstimateObjectSize(entry.Key, visited);
            size += EstimateObjectSize(entry.Value, visited);
        }

        return size;
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

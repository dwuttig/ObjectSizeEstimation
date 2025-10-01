using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation;

/// <summary>
/// Object size estimator based on JSON serialization.
/// This approach serializes objects to JSON and measures the resulting size.
/// It provides a different perspective on object size estimation compared to other approaches.
/// </summary>
public class BinarySerializationBasedEstimator : IObjectSizeEstimator
{
    private readonly ILogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private const int MAX_RECURSION_DEPTH = 50;
    private const int CACHE_SIZE_LIMIT = 1000;

    public BinarySerializationBasedEstimator(ILogger? logger = null)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };
    }

    public long EstimateSize(object? obj)
    {
        if (obj == null)
        {
            _logger?.LogDebug("JsonSerialization: null object, size = 0");
            return 0;
        }

        try
        {
            _logger?.LogDebug("JsonSerialization: Starting size estimation for {ObjectType}", obj.GetType().Name);

            // Use JSON serialization to get the actual serialized size
            var json = JsonSerializer.Serialize(obj, _jsonOptions);
            var size = System.Text.Encoding.UTF8.GetByteCount(json);

            _logger?.LogDebug("JsonSerialization: Estimated size for {ObjectType} = {Size} bytes", obj.GetType().Name, size);

            return size;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "JsonSerialization: Failed to serialize {ObjectType}, falling back to reflection-based estimation", obj.GetType().Name);
            
            // Fallback to reflection-based estimation for objects that can't be serialized
            return EstimateSizeWithReflection(obj);
        }
    }

    private long EstimateSizeWithReflection(object obj)
    {
        try
        {
            var type = obj.GetType();
            var size = 0L;

            // Object header
            size += 24; // Standard object header size

            // Get all properties and fields
            var properties = type.GetProperties();
            var fields = type.GetFields();

            foreach (var property in properties)
            {
                if (property.CanRead)
                {
                    try
                    {
                        var value = property.GetValue(obj);
                        size += EstimatePropertySize(property.PropertyType, value);
                    }
                    catch
                    {
                        // Skip properties that can't be read
                    }
                }
            }

            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(obj);
                    size += EstimatePropertySize(field.FieldType, value);
                }
                catch
                {
                    // Skip fields that can't be read
                }
            }

            return size;
        }
        catch
        {
            // If reflection fails, return a minimal estimate
            return 24; // Just the object header
        }
    }

    private long EstimatePropertySize(Type type, object? value)
    {
        if (value == null) return 0;

        // Handle primitive types
        if (type == typeof(int)) return 4;
        if (type == typeof(long)) return 8;
        if (type == typeof(double)) return 8;
        if (type == typeof(float)) return 4;
        if (type == typeof(bool)) return 1;
        if (type == typeof(char)) return 2;
        if (type == typeof(byte)) return 1;
        if (type == typeof(short)) return 2;
        if (type == typeof(ushort)) return 2;
        if (type == typeof(uint)) return 4;
        if (type == typeof(ulong)) return 8;
        if (type == typeof(decimal)) return 16;
        if (type == typeof(DateTime)) return 8;
        if (type == typeof(TimeSpan)) return 8;
        if (type == typeof(Guid)) return 16;

        // Handle strings
        if (type == typeof(string))
        {
            var str = (string)value;
            return 24 + (str.Length * 2); // String overhead + characters
        }

        // Handle arrays
        if (type.IsArray)
        {
            var array = (Array)value;
            var elementType = type.GetElementType()!;
            var elementSize = EstimatePropertySize(elementType, null); // Estimate element size
            return 24 + (array.Length * elementSize); // Array overhead + elements
        }

        // Handle collections
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            var enumerable = (System.Collections.IEnumerable)value;
            var count = 0;
            var elementSize = 0L;

            foreach (var item in enumerable)
            {
                count++;
                if (count == 1) // Estimate based on first element
                {
                    elementSize = EstimatePropertySize(item?.GetType() ?? typeof(object), item);
                }
            }

            return 24 + (count * elementSize); // Collection overhead + elements
        }

        // Handle enums
        if (type.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(type);
            return EstimatePropertySize(underlyingType, null);
        }

        // Handle nullable types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            return EstimatePropertySize(underlyingType!, value);
        }

        // For complex objects, estimate based on type
        return 24; // Default object size
    }
}

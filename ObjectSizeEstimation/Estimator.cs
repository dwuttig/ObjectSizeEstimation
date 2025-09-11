using Microsoft.Extensions.Logging;
namespace ObjectSizeEstimation;

public class Estimator
{
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;

    public Estimator(Microsoft.Extensions.Logging.ILogger? logger = null)
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

    private static long EstimateObjectSize(object? obj, HashSet<object> visited)
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

        // Primitives and well-known value types
        if (TryGetPrimitiveSize(type, out var primitiveSize))
        {
            return primitiveSize;
        }

        // Strings
        if (obj is string s)
        {
            // Naive: 24 bytes overhead + 2 bytes per char
            return 24 + (s.Length * 2);
        }

        // Arrays
        if (obj is Array arr)
        {
            long size = 24; // naive array overhead
            var elementType = type.GetElementType();
            if (elementType is not null && TryGetPrimitiveSize(elementType, out var elementPrimitiveSize))
            {
                size += (long)arr.LongLength * elementPrimitiveSize;
            }
            else
            {
                foreach (var item in arr)
                {
                    size += EstimateObjectSize(item, visited);
                }
            }
            return size;
        }

        // Value types (structs): sum fields
        if (type.IsValueType)
        {
            long size = 0;
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(obj);
                size += EstimateObjectSize(fieldValue, visited);
            }
            return size;
        }

        // Reference types (classes): naive object header + fields
        {
            long size = 24; // naive object header
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(obj);
                size += EstimateObjectSize(fieldValue, visited);
            }
            return size;
        }
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
using System.Collections;
using System.Numerics;

namespace Haven.Testing.Common;

public static class DataGen
{
    private const int MinCollectionSize = 1;
    private const int MaxCollectionSize = 5;
    private const int MaxDepth = 3;

    public static T GenerateRandomizedObject<T>(T obj)
    {
        var properties = typeof(T).GetProperties();
        foreach (var property in properties)
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
                continue;

            if (TryGenerateValue(property.PropertyType, 0, out var value))
            {
                property.SetValue(obj, value);
            }
        }

        return obj;
    }

    private static bool TryGenerateValue(Type type, int depth, out object? value)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        value = null;

        if (type == typeof(string))
            value = GenerateRandomString(10);
        else if (type == typeof(int))
            value = GenerateRandomNumber(1, 100);
        else if (type == typeof(double))
            value = GenerateRandomNumber(1.0, 100.0);
        else if (type == typeof(bool))
            value = new Random().Next(0, 2) == 0;
        else if (type == typeof(Guid))
            value = Guid.NewGuid();
        else if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            value = values.Length == 0 ? null : values.GetValue(new Random().Next(values.Length));
            return value is not null;
        }
        else if (depth < MaxDepth)
            return TryGenerateCollection(type, depth + 1, out value);
        else
            return false;

        return true;
    }

    private static bool TryGenerateCollection(Type type, int depth, out object? value)
    {
        value = null;
        var count = new Random().Next(MinCollectionSize, MaxCollectionSize + 1);

        if (type.IsArray)
        {
            if (type.GetArrayRank() != 1)
                return false;

            var elementType = type.GetElementType()!;
            var array = Array.CreateInstance(elementType, count);
            for (var i = 0; i < count; i++)
            {
                if (!TryGenerateValue(elementType, depth, out var item))
                    return false;
                array.SetValue(item, i);
            }

            value = array;
            return true;
        }

        if (type == typeof(string) || !type.IsGenericType && !typeof(IEnumerable).IsAssignableFrom(type))
            return false;

        var concreteType = ResolveConcreteType(type);
        if (concreteType is null)
            return false;

        var instance = Activator.CreateInstance(concreteType)!;

        if (instance is IDictionary dictionary)
        {
            var dictionaryArgs = GetGenericArguments(concreteType, typeof(IDictionary<,>));
            if (dictionaryArgs is null)
                return false;

            for (var i = 0; i < count; i++)
            {
                if (!TryGenerateValue(dictionaryArgs[0], depth, out var key) || key is null ||
                    !TryGenerateValue(dictionaryArgs[1], depth, out var item))
                    return false;
                dictionary[key] = item;
            }

            value = instance;
            return true;
        }

        var collectionArgs = GetGenericArguments(concreteType, typeof(ICollection<>));
        if (collectionArgs is null)
            return false;

        var add = typeof(ICollection<>).MakeGenericType(collectionArgs[0]).GetMethod("Add")!;
        for (var i = 0; i < count; i++)
        {
            if (!TryGenerateValue(collectionArgs[0], depth, out var item))
                return false;
            add.Invoke(instance, [item]);
        }

        value = instance;
        return true;
    }

    /// <summary>
    /// Maps interface types (IList&lt;T&gt;, ISet&lt;T&gt;, IReadOnlyDictionary&lt;K,V&gt;, ...) to a concrete,
    /// instantiable type that is assignable to the requested one.
    /// </summary>
    private static Type? ResolveConcreteType(Type type)
    {
        if (type.IsGenericType && type.IsInterface)
        {
            var definition = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();

            if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                return typeof(Dictionary<,>).MakeGenericType(args);

            if (definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>))
                return typeof(HashSet<>).MakeGenericType(args);

            if (definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>) ||
                definition == typeof(IList<>) || definition == typeof(IReadOnlyCollection<>) ||
                definition == typeof(IReadOnlyList<>))
                return typeof(List<>).MakeGenericType(args);

            return null;
        }

        if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) is null)
            return null;

        return type;
    }

    private static Type[]? GetGenericArguments(Type type, Type interfaceDefinition)
    {
        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceDefinition)
            ?.GetGenericArguments();
    }

    public static string GenerateRandomString(int length, string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
    {
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public static INumber<T> GenerateRandomNumber<T>(T min, T max) where T : INumber<T>
    {
        var random = new Random();
        var range = max - min;
        var randomValue = (T)(object)random.NextDouble() * range + min;
        return randomValue;
    }
}

// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Test.Monitors16;

public static class JsonComparer
{
    /// <summary>
    /// Compares two JSON strings semantically, checking if all properties and values match logically.
    /// </summary>
    /// <param name="json1">First JSON string to compare.</param>
    /// <param name="json2">Second JSON string to compare.</param>
    /// <param name="ignoreArrayOrder">If true, arrays are compared as sets (order-independent). Default is false.</param>
    /// <returns>True if the JSON structures are semantically equivalent, false otherwise.</returns>
    public static bool AreEqual(string json1, string json2, bool ignoreArrayOrder = false)
    {
        ArgumentNullException.ThrowIfNull(json1);
        ArgumentNullException.ThrowIfNull(json2);

        try
        {
            using JsonDocument doc1 = JsonDocument.Parse(json1);
            using JsonDocument doc2 = JsonDocument.Parse(json2);

            return AreElementsEqual(doc1.RootElement, doc2.RootElement, ignoreArrayOrder);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Compares two JSON files semantically.
    /// </summary>
    /// <param name="filePath1">Path to first JSON file.</param>
    /// <param name="filePath2">Path to second JSON file.</param>
    /// <param name="ignoreArrayOrder">If true, arrays are compared as sets (order-independent). Default is false.</param>
    /// <returns>True if the JSON structures are semantically equivalent, false otherwise.</returns>
    public static bool AreFilesEqual(string filePath1, string filePath2, bool ignoreArrayOrder = false)
    {
        ArgumentNullException.ThrowIfNull(filePath1);
        ArgumentNullException.ThrowIfNull(filePath2);

        string json1 = File.ReadAllText(filePath1);
        string json2 = File.ReadAllText(filePath2);

        return AreEqual(json1, json2, ignoreArrayOrder);
    }

    /// <summary>
    /// Compares two JsonElement objects recursively.
    /// </summary>
    private static bool AreElementsEqual(JsonElement element1, JsonElement element2, bool ignoreArrayOrder)
    {
        // Check if value types match
        if (element1.ValueKind != element2.ValueKind)
        {
            return false;
        }

        return element1.ValueKind switch
        {
            JsonValueKind.Object => AreObjectsEqual(element1, element2, ignoreArrayOrder),
            JsonValueKind.Array => AreArraysEqual(element1, element2, ignoreArrayOrder),
            JsonValueKind.String => element1.GetString() == element2.GetString(),
            JsonValueKind.Number => AreNumbersEqual(element1, element2),
            JsonValueKind.True or JsonValueKind.False => element1.GetBoolean() == element2.GetBoolean(),
            JsonValueKind.Null => true,
            _ => false
        };
    }

    /// <summary>
    /// Compares two JSON objects by checking all properties.
    /// </summary>
    private static bool AreObjectsEqual(JsonElement obj1, JsonElement obj2, bool ignoreArrayOrder)
    {
        var properties1 = obj1.EnumerateObject().ToList();
        var properties2 = obj2.EnumerateObject().ToList();

        // Check if property counts match
        if (properties1.Count != properties2.Count)
        {
            return false;
        }

        // Check each property exists in both objects with same value
        foreach (var prop1 in properties1)
        {
            if (!obj2.TryGetProperty(prop1.Name, out JsonElement prop2Value))
            {
                return false; // Property doesn't exist in second object
            }

            if (!AreElementsEqual(prop1.Value, prop2Value, ignoreArrayOrder))
            {
                return false; // Property values don't match
            }
        }

        return true;
    }

    /// <summary>
    /// Compares two JSON arrays.
    /// </summary>
    private static bool AreArraysEqual(JsonElement arr1, JsonElement arr2, bool ignoreArrayOrder)
    {
        var items1 = arr1.EnumerateArray().ToList();
        var items2 = arr2.EnumerateArray().ToList();

        // Check if array lengths match
        if (items1.Count != items2.Count)
        {
            return false;
        }

        if (ignoreArrayOrder)
        {
            // Compare as sets - each item in arr1 must have a match in arr2
            var unmatchedItems2 = items2.ToList();

            foreach (var item1 in items1)
            {
                bool foundMatch = false;

                for (int i = 0; i < unmatchedItems2.Count; i++)
                {
                    if (AreElementsEqual(item1, unmatchedItems2[i], ignoreArrayOrder))
                    {
                        unmatchedItems2.RemoveAt(i);
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    return false;
                }
            }

            return unmatchedItems2.Count == 0;
        }
        else
        {
            // Compare in order
            for (int i = 0; i < items1.Count; i++)
            {
                if (!AreElementsEqual(items1[i], items2[i], ignoreArrayOrder))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Compares two JSON numbers, handling both integer and decimal values.
    /// </summary>
    private static bool AreNumbersEqual(JsonElement num1, JsonElement num2)
    {
        // Try to compare as decimals for precision
        if (num1.TryGetDecimal(out decimal dec1) && num2.TryGetDecimal(out decimal dec2))
        {
            return dec1 == dec2;
        }

        // Fallback to double comparison
        if (num1.TryGetDouble(out double dbl1) && num2.TryGetDouble(out double dbl2))
        {
            return Math.Abs(dbl1 - dbl2) < double.Epsilon;
        }

        return false;
    }

    /// <summary>
    /// Gets the differences between two JSON strings.
    /// </summary>
    /// <param name="json1">First JSON string.</param>
    /// <param name="json2">Second JSON string.</param>
    /// <returns>List of difference descriptions.</returns>
    public static List<string> GetDifferences(string json1, string json2)
    {
        ArgumentNullException.ThrowIfNull(json1);
        ArgumentNullException.ThrowIfNull(json2);

        var differences = new List<string>();

        try
        {
            using JsonDocument doc1 = JsonDocument.Parse(json1);
            using JsonDocument doc2 = JsonDocument.Parse(json2);

            FindDifferences(doc1.RootElement, doc2.RootElement, "", differences);
        }
        catch (JsonException ex)
        {
            differences.Add($"JSON Parse Error: {ex.Message}");
        }

        return differences;
    }

    /// <summary>
    /// Recursively finds differences between two JSON elements.
    /// </summary>
    private static void FindDifferences(JsonElement element1, JsonElement element2, string path, List<string> differences)
    {
        if (element1.ValueKind != element2.ValueKind)
        {
            differences.Add($"Path '{path}': Type mismatch - {element1.ValueKind} vs {element2.ValueKind}");
            return;
        }

        switch (element1.ValueKind)
        {
            case JsonValueKind.Object:
                FindObjectDifferences(element1, element2, path, differences);
                break;

            case JsonValueKind.Array:
                FindArrayDifferences(element1, element2, path, differences);
                break;

            case JsonValueKind.String:
                if (element1.GetString() != element2.GetString())
                {
                    differences.Add($"Path '{path}': '{element1.GetString()}' != '{element2.GetString()}'");
                }
                break;

            case JsonValueKind.Number:
                if (!AreNumbersEqual(element1, element2))
                {
                    differences.Add($"Path '{path}': {element1.GetRawText()} != {element2.GetRawText()}");
                }
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                if (element1.GetBoolean() != element2.GetBoolean())
                {
                    differences.Add($"Path '{path}': {element1.GetBoolean()} != {element2.GetBoolean()}");
                }
                break;
        }
    }

    private static void FindObjectDifferences(JsonElement obj1, JsonElement obj2, string path, List<string> differences)
    {
        var props1 = obj1.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var props2 = obj2.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        // Check for properties in obj1 not in obj2
      /*  foreach (var prop in props1.Keys.Except(props2.Keys))
        {
            differences.Add($"Path '{path}.{prop}': Missing in second JSON");
        }*/

        // Check for properties in obj2 not in obj1
        foreach (var prop in props2.Keys.Except(props1.Keys))
        {
            differences.Add($"Path '{path}.{prop}': Missing in first JSON");
        }

        // Check common properties
        foreach (var prop in props1.Keys.Intersect(props2.Keys))
        {
            string newPath = string.IsNullOrEmpty(path) ? prop : $"{path}.{prop}";
            FindDifferences(props1[prop], props2[prop], newPath, differences);
        }
    }

    private static void FindArrayDifferences(JsonElement arr1, JsonElement arr2, string path, List<string> differences)
    {
        var items1 = arr1.EnumerateArray().ToList();
        var items2 = arr2.EnumerateArray().ToList();

        if (items1.Count != items2.Count)
        {
            differences.Add($"Path '{path}': Array length mismatch - {items1.Count} vs {items2.Count}");
            return;
        }

        for (int i = 0; i < items1.Count; i++)
        {
            FindDifferences(items1[i], items2[i], $"{path}[{i}]", differences);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Test.Monitors16;

public static class MonitorExporter
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowUsage();
            return;
        }

        string command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "export":
                if (args.Length < 2)
                {
                    Console.WriteLine("❌ Error: Export requires DLL path.");
                    Console.WriteLine("Usage: dotnet run export <dllPath> [outputDirectory]");
                    return;
                }
                ExportMonitors(args[1], args.Length > 2 ? args[2] : "ExportedMonitors");
                break;

            case "compare":
                if (args.Length < 3)
                {
                    Console.WriteLine("❌ Error: Compare requires two file paths.");
                    Console.WriteLine("Usage: dotnet run compare <file1.json> <file2.json>");
                    return;
                }
                CompareJsonFiles(args[1], args[2]);
                break;

            case "compare-batch":
                if (args.Length < 2)
                {
                    Console.WriteLine("❌ Error: Compare-batch requires mapping file path.");
                    Console.WriteLine("Usage: dotnet run compare-batch <mappingFile.json>");
                    return;
                }
                CompareBatchFiles(args[1]);
                break;

            default:
                Console.WriteLine($"❌ Unknown command: {command}");
                ShowUsage();
                break;
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Geneva Monitor Exporter & Comparator");
        Console.WriteLine("=====================================\n");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run export <dllPath> [outputDirectory]");
        Console.WriteLine("      Exports all monitors from the specified DLL to JSON files\n");
        Console.WriteLine("  dotnet run compare <file1.json> <file2.json>");
        Console.WriteLine("      Compares two JSON files semantically\n");
        Console.WriteLine("  dotnet run compare-batch <mappingFile.json>");
        Console.WriteLine("      Compares multiple pairs of JSON files from a mapping file\n");
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run export MyMonitors.dll");
        Console.WriteLine("  dotnet run export MyMonitors.dll C:\\Output\\Monitors");
        Console.WriteLine("  dotnet run compare ExportedMonitors/Monitor1.json OriginalMonitors/Monitor1.json");
        Console.WriteLine("  dotnet run compare-batch comparisons.json");
        Console.WriteLine("\nMapping file format (comparisons.json):");
        Console.WriteLine("  [");
        Console.WriteLine("    {");
        Console.WriteLine("      \"file1Path\": \"ExportedMonitors/Monitor1.json\",");
        Console.WriteLine("      \"file2Path\": \"OriginalMonitors/Monitor1.json\"");
        Console.WriteLine("    },");
        Console.WriteLine("    {");
        Console.WriteLine("      \"file1Path\": \"ExportedMonitors/Monitor2.json\",");
        Console.WriteLine("      \"file2Path\": \"OriginalMonitors/Monitor2.json\"");
        Console.WriteLine("    }");
        Console.WriteLine("  ]");
    }

    /// <summary>
    /// Compares multiple pairs of JSON files from a mapping file.
    /// </summary>
    private static void CompareBatchFiles(string mappingFilePath)
    {
        Console.WriteLine("🔄 Starting batch comparison...\n");

        // Check if mapping file exists
        if (!File.Exists(mappingFilePath))
        {
            Console.WriteLine($"❌ Mapping file not found: {Path.GetFullPath(mappingFilePath)}");
            return;
        }

        try
        {
            // Read and parse the mapping file
            string mappingJson = File.ReadAllText(mappingFilePath);
            var filePairs = JsonSerializer.Deserialize<List<FileComparisonPair>>(mappingJson);

            if (filePairs == null || filePairs.Count == 0)
            {
                Console.WriteLine("⚠️  No file pairs found in the mapping file.");
                return;
            }

            Console.WriteLine($"📋 Found {filePairs.Count} file pair(s) to compare\n");
            Console.WriteLine(new string('=', 80));

            int totalPairs = filePairs.Count;
            int successfulComparisons = 0;
            int failedComparisons = 0;
            int equalPairs = 0;
            int differentPairs = 0;

            for (int i = 0; i < filePairs.Count; i++)
            {
                var pair = filePairs[i];
                Console.WriteLine($"\n[{i + 1}/{totalPairs}] Comparing:");
                Console.WriteLine($"  File 1: {pair.File1Path}");
                Console.WriteLine($"  File 2: {pair.File2Path}");
                Console.WriteLine(new string('-', 80));

                // Validate file paths
                if (string.IsNullOrWhiteSpace(pair.File1Path) || string.IsNullOrWhiteSpace(pair.File2Path))
                {
                    Console.WriteLine("  ❌ Invalid file paths in mapping");
                    failedComparisons++;
                    continue;
                }

                if (!File.Exists(pair.File1Path))
                {
                    Console.WriteLine($"  ❌ File 1 not found: {pair.File1Path}");
                    failedComparisons++;
                    continue;
                }

                if (!File.Exists(pair.File2Path))
                {
                    Console.WriteLine($"  ❌ File 2 not found: {pair.File2Path}");
                    failedComparisons++;
                    continue;
                }

                try
                {
                    // Perform comparison
                    bool areEqual = JsonComparer.AreFilesEqual(pair.File1Path, pair.File2Path);

                    if (areEqual)
                    {
                        Console.WriteLine("  ✅ Files are semantically equal!");
                        equalPairs++;
                    }
                    else
                    {
                        Console.WriteLine("  ❌ Files have differences:");

                        var differences = JsonComparer.GetDifferences(
                            File.ReadAllText(pair.File1Path),
                            File.ReadAllText(pair.File2Path)
                        );

                        int maxDiffsToShow = 5;
                        for (int j = 0; j < differences.Count; j++)
                        {
                            Console.WriteLine($"     {j + 1}. {differences[j]}");
                        }

                        Console.WriteLine($"  📝 Total: {differences.Count} difference(s)");
                        differentPairs++;
                    }

                    successfulComparisons++;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"  ❌ JSON parsing error: {ex.Message}");
                    failedComparisons++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Console.WriteLine($"  ❌ File access error: {ex.Message}");
                    failedComparisons++;
                }
            }

            // Print summary
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("📊 BATCH COMPARISON SUMMARY");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"Total pairs:              {totalPairs}");
            Console.WriteLine($"Successful comparisons:   {successfulComparisons}");
            Console.WriteLine($"Failed comparisons:       {failedComparisons}");
            Console.WriteLine($"Files equal:              {equalPairs}");
            Console.WriteLine($"Files with differences:   {differentPairs}");
            Console.WriteLine(new string('=', 80));

            if (equalPairs == totalPairs && failedComparisons == 0)
            {
                Console.WriteLine("\n✅ All file pairs are equal!");
            }
            else if (differentPairs > 0)
            {
                Console.WriteLine($"\n⚠️  {differentPairs} file pair(s) have differences");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"❌ Failed to parse mapping file: {ex.Message}");
            Console.WriteLine("\nExpected format:");
            Console.WriteLine("  [");
            Console.WriteLine("    { \"file1Path\": \"path1.json\", \"file2Path\": \"path2.json\" },");
            Console.WriteLine("    { \"file1Path\": \"path3.json\", \"file2Path\": \"path4.json\" }");
            Console.WriteLine("  ]");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error reading mapping file: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports all Geneva monitors from the specified DLL to JSON files.
    /// </summary>
    private static void ExportMonitors(string dllPath, string outputDirectory)
    {
        Console.WriteLine("🔄 Starting monitor export...\n");

        // Validate DLL path
        if (!File.Exists(dllPath))
        {
            Console.WriteLine($"❌ DLL file not found: {Path.GetFullPath(dllPath)}");
            return;
        }

        // Load the assembly
        Assembly assembly;
        try
        {
            Console.WriteLine($"📦 Loading assembly: {Path.GetFullPath(dllPath)}");
            assembly = Assembly.LoadFrom(dllPath);
            Console.WriteLine($"✓ Assembly loaded successfully: {assembly.GetName().Name}\n");
        }
        catch (Exception ex) when (ex is FileNotFoundException or BadImageFormatException or FileLoadException)
        {
            Console.WriteLine($"❌ Failed to load assembly: {ex.Message}");
            return;
        }

        // Create output directory
        Directory.CreateDirectory(outputDirectory);

        // Configure JSON serialization options
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = 
            {
                new FlattenAdditionalPropertiesConverter(),
                new PolymorphicCollectionConverter()
            }
        };

        // Find all IMonitorProvider implementations in the loaded assembly
        var monitorProviders = GetMonitorProviders(assembly);

        if (!monitorProviders.Any())
        {
            Console.WriteLine("⚠️  No IMonitorProvider implementations found in the assembly.");
            Console.WriteLine("    Make sure the DLL contains classes implementing IMonitorProvider.");
            return;
        }

        Console.WriteLine($"Found {monitorProviders.Count()} monitor provider(s)\n");
        int exportedCount = 0;
        int providerIndex = 0;

        foreach (var provider in monitorProviders)
        {
            providerIndex++;
            Console.WriteLine($"[{providerIndex}/{monitorProviders.Count()}] Processing: {provider.FullName}");

            try
            {
                var instance = Activator.CreateInstance(provider);
                Console.WriteLine($"    ✓ Instance created: {instance?.GetType().Name ?? "null"}");

                if (instance == null)
                {
                    Console.WriteLine($"    ⚠️  Failed to create instance (null returned)");
                    continue;
                }

                // Try to get GetMonitors method
                var getMonitorsMethod = provider.GetMethod("GetMonitors");
                if (getMonitorsMethod == null)
                {
                    Console.WriteLine($"    ⚠️  GetMonitors method not found");
                    continue;
                }

                var monitors = getMonitorsMethod.Invoke(instance, null);
                Console.WriteLine($"    ✓ GetMonitors invoked, result type: {monitors?.GetType().Name ?? "null"}");

                if (monitors is not System.Collections.IEnumerable monitorEnumerable)
                {
                    Console.WriteLine($"    ⚠️  GetMonitors did not return IEnumerable");
                    continue;
                }

                int monitorCount = 0;
                foreach (var monitor in monitorEnumerable)
                {
                    monitorCount++;
                    Console.WriteLine($"      Processing monitor #{monitorCount}: {monitor?.GetType().Name ?? "null"}");

                    if (monitor == null)
                    {
                        Console.WriteLine($"      ⚠️  Monitor is null");
                        continue;
                    }

                    // Get the monitor configuration using reflection
                    var monitorType = monitor.GetType();
                    var getV1Method = monitorType.GetMethod("GetMonitorConfigurationV1");
                    var getV2Method = monitorType.GetMethod("GetMonitorConfigurationV2");

                    object? monitorConfig = null;

                    if (getV1Method != null)
                    {
                        monitorConfig = getV1Method.Invoke(monitor, null);
                        Console.WriteLine($"      GetMonitorConfigurationV1: {(monitorConfig != null ? "✓ Found" : "null")}");
                    }

                    if (monitorConfig == null && getV2Method != null)
                    {
                        monitorConfig = getV2Method.Invoke(monitor, null);
                        Console.WriteLine($"      GetMonitorConfigurationV2: {(monitorConfig != null ? "✓ Found" : "null")}");
                    }

                    if (monitorConfig == null)
                    {
                        Console.WriteLine($"      ⚠️  Both V1 and V2 configurations are null");
                        continue;
                    }

                    // Use the provider class name as the file name
                    string fileName = $"{provider.Name}.json";
                    string filePath = Path.Combine(outputDirectory, fileName);

                    string json = JsonSerializer.Serialize(monitorConfig, monitorConfig.GetType(), options);
                    File.WriteAllText(filePath, json);

                    Console.WriteLine($"      ✓ Exported: {fileName} ({json.Length} bytes)");
                    exportedCount++;
                }

                if (monitorCount == 0)
                {
                    Console.WriteLine($"    ⚠️  No monitors returned from GetMonitors()");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ Error: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"    Inner: {ex.InnerException.Message}");
                }
                Console.WriteLine($"    Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
            }

            Console.WriteLine();
        }

        Console.WriteLine($"✅ Successfully exported {exportedCount} monitor(s) to '{Path.GetFullPath(outputDirectory)}'");
    }

    /// <summary>
    /// Configures polymorphic serialization for collections.
    /// </summary>
    private static void AddPolymorphicSerialization(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind == JsonTypeInfoKind.Object)
        {
            typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType
            };
        }
    }
    /// <summary>
    /// Compares two JSON files and displays all differences.
    /// </summary>
    private static void CompareJsonFiles(string file1Path, string file2Path)
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("📊 JSON File Comparison");
        Console.WriteLine(new string('=', 70));
        Console.WriteLine($"File 1: {Path.GetFullPath(file1Path)}");
        Console.WriteLine($"File 2: {Path.GetFullPath(file2Path)}");
        Console.WriteLine(new string('-', 70));

        // Check if files exist
        if (!File.Exists(file1Path))
        {
            Console.WriteLine($"\n❌ File not found: {file1Path}");
            return;
        }

        if (!File.Exists(file2Path))
        {
            Console.WriteLine($"\n❌ File not found: {file2Path}");
            return;
        }

        try
        {
            // Perform comparison
            bool areEqual = JsonComparer.AreFilesEqual(file1Path, file2Path);

            if (areEqual)
            {
                Console.WriteLine("\n✅ SUCCESS: Files are semantically equal!");
                Console.WriteLine("   All properties and values match.\n");
            }
            else
            {
                Console.WriteLine("\n❌ DIFFERENCES FOUND:\n");

                var differences = JsonComparer.GetDifferences(
                    File.ReadAllText(file1Path),
                    File.ReadAllText(file2Path)
                );

                int diffNumber = 1;
                foreach (var diff in differences)
                {
                    Console.WriteLine($"  {diffNumber,3}. {diff}");
                    diffNumber++;
                }

                Console.WriteLine($"\n📝 Total: {differences.Count} difference(s) detected\n");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"\n❌ JSON parsing error: {ex.Message}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"\n❌ File access error: {ex.Message}\n");
        }

        Console.WriteLine(new string('=', 70));
    }

    /// <summary>
    /// Gets all IMonitorProvider implementations from the specified assembly.
    /// </summary>
    private static IEnumerable<Type> GetMonitorProviders(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .Where(t => t.GetInterfaces().Any(i => i.Name == "IMonitorProvider")
                         && !t.IsInterface
                         && !t.IsAbstract);
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Handle cases where some types in the assembly cannot be loaded
            Console.WriteLine("⚠️  Some types could not be loaded from the assembly:");
            foreach (var loaderException in ex.LoaderExceptions.Take(5))
            {
                if (loaderException != null)
                {
                    Console.WriteLine($"    - {loaderException.Message}");
                }
            }

            // Return the types that were successfully loaded
            return ex.Types.Where(t => t != null
                                     && t.GetInterfaces().Any(i => i.Name == "IMonitorProvider")
                                     && !t.IsInterface
                                     && !t.IsAbstract)!;
        }
    }

    /// <summary>
    /// Modifies serialization to flatten AdditionalProperties to parent level.
    /// </summary>
    private static void ApplyAdditionalPropertiesConverter(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        // Find and mark AdditionalProperties property
        foreach (var property in typeInfo.Properties)
        {
            if (property.Name.Equals("additionalProperties", StringComparison.OrdinalIgnoreCase))
            {
                // Exclude from normal serialization - we'll handle it manually
                property.ShouldSerialize = (obj, value) => false;
            }
        }

        // Store original serialization action
        var originalOnSerialized = typeInfo.OnSerializing;

        // Override to add manual serialization logic
        typeInfo.OnSerializing = (obj) =>
        {
            originalOnSerialized?.Invoke(obj);
            // Custom serialization will be handled by a specialized converter
        };
    }
}
/// <summary>
/// Represents a pair of files to compare.
/// </summary>
public class FileComparisonPair
{
    [JsonPropertyName("file1Path")]
    public string File1Path { get; set; } = string.Empty;

    [JsonPropertyName("file2Path")]
    public string File2Path { get; set; } = string.Empty;
}

/// <summary>
/// Converter that serializes collection items using their actual runtime type.
/// </summary>
public class PolymorphicCollectionConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        // Skip strings
        if (typeToConvert == typeof(string))
            return false;

        // Skip dictionaries - they should serialize normally or be handled by FlattenAdditionalPropertiesConverter
        if (typeToConvert.IsGenericType)
        {
            var genericDef = typeToConvert.GetGenericTypeDefinition();
            if (genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>))
                return false;
        }

        // Handle any other type that implements IEnumerable
        return typeof(System.Collections.IEnumerable).IsAssignableFrom(typeToConvert);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // Get the element type
        Type? elementType = null;

        if (typeToConvert.IsArray)
        {
            elementType = typeToConvert.GetElementType();
        }
        else if (typeToConvert.IsGenericType)
        {
            elementType = typeToConvert.GetGenericArguments()[0];
        }

        if (elementType == null)
        {
            // Fallback for non-generic IEnumerable
            elementType = typeof(object);
        }

        Type converterType = typeof(PolymorphicCollectionConverterInner<>).MakeGenericType(elementType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private class PolymorphicCollectionConverterInner<T> : JsonConverter<System.Collections.IEnumerable>
    {
        public override System.Collections.IEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException("Deserialization not supported");
        }

        public override void Write(Utf8JsonWriter writer, System.Collections.IEnumerable value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var item in value)
            {
                if (item == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    // Get the actual runtime type
                    Type itemType = item.GetType();

                    // Serialize using the actual runtime type
                    // This will recursively handle nested collections and objects
                    JsonSerializer.Serialize(writer, item, itemType, options);
                }
            }

            writer.WriteEndArray();
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert != typeof(string) &&
                   typeof(System.Collections.IEnumerable).IsAssignableFrom(typeToConvert);
        }
    }
}
/// <summary>
/// Converter that writes objects and flattens their AdditionalProperties and uxParameters dictionaries to the parent level.
/// </summary>
public class FlattenAdditionalPropertiesConverter : JsonConverter<object>
{
    private static readonly string[] PropertiesToFlatten = { "AdditionalProperties" };

    public override bool CanConvert(Type typeToConvert)
    {
        // Check if type has any of the properties to flatten
        foreach (var propName in PropertiesToFlatten)
        {
            var prop = typeToConvert.GetProperty(propName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop != null)
            {
                var propType = prop.PropertyType;
                if (propType.IsGenericType)
                {
                    var genericDef = propType.GetGenericTypeDefinition();
                    if (genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException("Deserialization not supported");
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        var type = value.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var propsToFlatten = new List<IDictionary<string, object>>();

        // First pass: write all regular properties and collect properties to flatten
        foreach (var prop in properties)
        {
            // Check if this is a property to flatten
            if (PropertiesToFlatten.Any(name => prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                // Store for later flattening
                var propValue = prop.GetValue(value);
                if (propValue is IDictionary<string, object> dict)
                {
                    propsToFlatten.Add(dict);
                }
                continue;
            }

            // Write regular property
            var propName = options.PropertyNamingPolicy?.ConvertName(prop.Name) ?? prop.Name;
            writer.WritePropertyName(propName);

            var propVal = prop.GetValue(value);
            if (propVal == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                JsonSerializer.Serialize(writer, propVal, propVal.GetType(), options);
            }
        }

        // Second pass: write all flattened properties at same level
        foreach (var flattenedProps in propsToFlatten)
        {
            foreach (var kvp in flattenedProps)
            {
                var propName = options.PropertyNamingPolicy?.ConvertName(kvp.Key) ?? kvp.Key;
                writer.WritePropertyName(propName);

                if (kvp.Value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options);
                }
            }
        }

        writer.WriteEndObject();
    }
}

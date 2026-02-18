Geneva Monitor Exporter & Comparator
=====================================
`dotnet run export <dllPath> [outputDirectory]`
Exports all monitors from the specified DLL to JSON files

  `dotnet run compare <file1.json> <file2.json>`
      Compares two JSON files semantically

  `dotnet run compare-batch <mappingFile.json>`
      Compares multiple pairs of JSON files from a mapping file

Examples:

`dotnet run export MyMonitors.dll`
`dotnet run export MyMonitors.dll C:\Output\Monitors`
`dotnet run compare ExportedMonitors/Monitor1.json OriginalMonitors/Monitor1.json`
`dotnet run compare-batch comparisons.json`

Mapping file format (comparisons.json):
  [
    {
      "file1Path": "ExportedMonitors/Monitor1.json",
      "file2Path": "OriginalMonitors/Monitor1.json"
    },
    {
      "file1Path": "ExportedMonitors/Monitor2.json",
      "file2Path": "OriginalMonitors/Monitor2.json"
    }
  ]

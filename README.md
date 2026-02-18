Geneva Monitor Exporter & Comparator
=====================================
This tool allows you to export monitors from a specified DLL to JSON files and compare them semantically. It provides two main functionalities: exporting monitors and comparing JSON files.

#### Exports all monitors from the specified DLL to JSON files
`dotnet run export <dllPath> [outputDirectory]`

#### Compares two JSON files semantically
  `dotnet run compare <file1.json> <file2.json>`
 
  #### Compares multiple pairs of JSON files from a mapping file
  `dotnet run compare-batch <mappingFile.json>`
     

### Examples:

``dotnet run export MyMonitors.dll``<br>
``dotnet run export MyMonitors.dll C:\Output\Monitors`` <br>
``dotnet run compare ExportedMonitors/Monitor1.json OriginalMonitors/Monitor1.json``<br>
``dotnet run compare-batch comparisons.json``<br>

#### Mapping file format (comparisons.json):
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


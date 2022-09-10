using System;
using Microsoft.Build.Locator;

foreach (var instance in MSBuildLocator.QueryVisualStudioInstances())
    Console.WriteLine(instance.MSBuildPath);
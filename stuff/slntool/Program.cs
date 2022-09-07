using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.CodeAnalysis;

#if false
using Onion.SolutionParser.Parser;
using Onion.SolutionParser.Parser.Model;

string[] inputs = args;
string output = Path.GetFullPath(@"Output.sln");
string outputDirectory = Path.GetDirectoryName(output);
Debug.Assert(outputDirectory != null, nameof(outputDirectory) + " != null");

if (File.Exists(output))
    File.Delete(output);

List<Project> projects = new();
Dictionary<string, Project> pathToProject = new();
Dictionary<Guid, Project> guidToProject = new();


foreach (var arg in inputs)
{
    string fullPath = Path.GetFullPath(arg);
    string relativePath = fullPath [outputDirectory);
    string directory = Path.GetDirectoryName(arg);
            
    void AddProject(Project p)
    {
        Console.WriteLine("Adding project");
        projects.Add(p);
        pathToProject.Add(p.Path, p);
        guidToProject.Add(p.Guid, p);
    }
    
    if (arg.EndsWith("proj"))
    {
        // Add single project.
        if (pathToProject.ContainsKey(relativePath))
            continue;
        AddProject(new Project(
            typeGuid: new Guid(),
            guid: new Guid(),
            name: Path.GetFileNameWithoutExtension(fullPath),
            path: relativePath));
    }
    else if (arg.EndsWith(".sln"))
    {
        // Add all projects.
        var sln = SolutionParser.Parse(fullPath);
        
        foreach (var p in sln.Projects)
        {
            Debug.Assert(!Path.IsPathRooted(p.Path));
            string projectRelativePath = Path.Join(relativePath, p.Path);
            p.Path = projectRelativePath;
            
            if (pathToProject.ContainsKey(projectRelativePath))
                continue;
            
            if (guidToProject.ContainsKey(p.Guid))
            {
                Project pcopy = new Project(
                    typeGuid: p.TypeGuid,
                    name: p.Name,
                    path: projectRelativePath, 
                    guid: new Guid());
                pcopy.ProjectSection = p.ProjectSection;
                AddProject(pcopy);
            }
            else
            {
                AddProject(p);
            }
        }
    }
}

{
    var sln = new Solution();
    sln.Header.Add(@"
Microsoft Visual Studio Solution File, Format Version 12.00" + 
"# Visual Studio Version 16" + @"
VisualStudioVersion = 16.0.30114.105
MinimumVisualStudioVersion = 10.0.40219.1");
    foreach (var p in projects)
        sln.Projects.Add(p);
    string content = SolutionRenderer.Render(sln);
    File.WriteAllText(output, content);
}
#else

var workspace = new AdhocWorkspace();

#endif
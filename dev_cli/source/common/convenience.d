module common.convenience;

import std.process;
import std.stdio;

auto spawnProcess2(const(char[])[] args, const char[] workDir)
{
    File stdin = std.stdio.stdin;
    File stdout = std.stdio.stdout;
    File stderr = std.stdio.stderr;
    const string[string] env = null;
    Config config = Config.none;

    writeln(escapeShellCommand(args));

    return std.process.spawnProcess(args, stdin, stdout, stderr, env, config, workDir);
}

auto execute2(const(char[])[] args, const(char)[] workDir)
{
    const string[string] env = null;
    Config config = Config.none;
    size_t maxOutput = size_t.max;

    writeln(escapeShellCommand(args));

    return std.process.execute(args, env, config, maxOutput, workDir);
}

bool canFindProgram(string programName)
{
    return std.process.execute([programName, "--version"]).status == 0;
}


static import std.file;
import std.path;

void replaceFile(string fileFrom, string fileTo)
{
    if (std.file.exists(fileTo))
        std.file.remove(fileTo);
    std.file.copy(fileFrom, fileTo);
}

void maybeCreateDirectoriesUntilDirectoryOf(string filePath)
{
    const parentDirName = dirName(filePath);
    if (!std.file.exists(parentDirName))
        std.file.mkdirRecurse(parentDirName);
}

void unzip(string zipPath, string outPath)
{
    import std.file : read;
    auto zipBytes = read(zipPath);
    unzip(zipBytes, outPath);
}

void unzip(void[] zipBytes, string outPath)
{
    import std.zip;
    import std.file : write, exists, mkdirRecurse;
    import std.path;
    auto zip = new ZipArchive(zipBytes);
    foreach (ArchiveMember m; zip.directory)
    {
        const bytes = zip.expand(m);
        const path = buildPath(outPath, m.name);
        const folderPath = dirName(path);
        if (!exists(folderPath))
            mkdirRecurse(folderPath);
        write(path, bytes);
    }
}

template exe(string a)
{
    version (Windows)
        enum exe = a ~ ".exe";
    else 
        enum exe = a;
} 

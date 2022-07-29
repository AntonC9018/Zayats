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
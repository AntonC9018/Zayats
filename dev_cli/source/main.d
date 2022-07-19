module main;

import jcli;
import std.stdio;


int main(string[] args)
{
    import commands.context : Context;
    return matchAndExecuteFromRootCommands!(bindArgumentSimple, Context)(args[1 .. $]);
}

using System;
class PatchCli {
 static int Main(string[] args){try{if(args.Length!=2)throw new Exception("Usage: PatchCli.exe apply|restore game-path");if(args[0]=="apply")Patcher.Apply(args[1]);else if(args[0]=="restore")Patcher.Restore(args[1]);else throw new Exception("Unknown action");Console.WriteLine(Patcher.Status(args[1]));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

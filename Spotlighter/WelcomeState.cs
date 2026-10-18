using System;
using System.IO;

namespace Spotlighter;

// Kept separate from light settings: restoring defaults must not reset help.
internal static class WelcomeState
{
    internal const int Version=1;
    internal static string FilePath=>Path.Combine(LightSettings.Folder,"welcome.version");
    internal static bool NeedsGuide(string path)
    {
        try{return !int.TryParse(File.ReadAllText(path),out int version)||version<Version;}
        catch(IOException){return true;}
        catch(UnauthorizedAccessException){return true;}
    }
    internal static bool MarkSeen(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path+".tmp",Version.ToString());
            File.Move(path+".tmp",path,true);return true;
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
    }
}

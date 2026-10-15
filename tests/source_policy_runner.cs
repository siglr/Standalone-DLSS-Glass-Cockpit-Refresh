using System;
internal static class SourcePolicyRunner {
    static int Main() {
        string status = null;
        bool result = StandaloneAppUpdate.CheckAsync("invalid-source-build-path", text => status = text,
            () => { throw new Exception("Source build attempted update restart"); }).GetAwaiter().GetResult();
        if (result || status != "Source build: automatic app updates disabled." ||
            StandaloneAppUpdate.Startup("invalid-source-build-path")) return 1;
        Console.WriteLine("PASS source-build update entry points disabled");
        return 0;
    }
}

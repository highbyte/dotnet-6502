namespace Highbyte.DotNet6502.Tests.Helpers;

/// <summary>
/// xUnit collection serializing every test class that downloads/assembles Klaus test
/// artifacts. They share files in the working directory — most critically the AS65
/// assembler folder, which <see cref="FunctionalTestCompiler"/> deletes and re-extracts
/// per build: if one test is EXECUTING as65.exe while another deletes the folder,
/// Windows throws UnauthorizedAccessException (running executables are locked).
/// Same-collection classes never run concurrently, eliminating the race.
/// </summary>
[CollectionDefinition(Name)]
public class KlausTestArtifactsCollection
{
    public const string Name = "KlausTestArtifacts";
}

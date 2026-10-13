// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoneControl.Tools.Architecture;

internal static class Program
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);
    private static readonly CSharpCompilationOptions CompileOptions = new(OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Enable);
    private static readonly SyntaxTree ImplicitImports = Parse("""
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        """);
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => !Path.GetFileName(path).StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private static readonly string[] ForbiddenHostTypes = [
        "System.IO.File", "System.IO.Directory", "System.IO.FileStream", "System.Diagnostics.Process",
        "Microsoft.Win32.Registry", "Microsoft.Win32.RegistryKey", "System.Net.Sockets.Socket",
        "System.Net.Sockets.TcpClient", "System.Net.Sockets.TcpListener",
        "System.Net.Http.HttpClient", "System.Runtime.InteropServices.DllImportAttribute",
        "System.Runtime.InteropServices.LibraryImportAttribute"
    ];

    public static int Main(string[] args)
    {
        try {
            SelfTest();
            string root = Path.GetFullPath(args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "../../../../../") : args[0]);
            string source = Path.Combine(root, "src/PhoneControl");
            var files = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"))
                .Order().ToArray();
            if (files.Length == 0) throw new InvalidDataException("No application source found.");
            var trees = new Dictionary<string, SyntaxTree>();
            foreach (string file in files) {
                string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                var tree = Parse(File.ReadAllText(file), relative);
                CheckLayout(tree, relative);
                trees.Add(relative, tree);
            }
            // Compile each inner layer using only the .NET runtime. Domain has no
            // application assembly; application has Domain but no outer adapters.
            var domain = CheckLayer("Domain", trees.Where(p => p.Key.StartsWith("Domain/")).Select(p => p.Value), References);
            using var metadata = new MemoryStream();
            var emitted = domain.Emit(metadata);
            if (!emitted.Success) throw new InvalidDataException("Cannot emit the domain contract for dependency validation.");
            var domainReference = MetadataReference.CreateFromImage(metadata.ToArray());
            CheckLayer("Application", trees.Where(p => p.Key.StartsWith("Application/")).Select(p => p.Value), References.Append(domainReference));
            Console.WriteLine($"PASS architecture: {files.Length} matching type files; Domain and Application compile without outer layers.");
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static SyntaxTree Parse(string source, string path = "") => CSharpSyntaxTree.ParseText(source, ParseOptions, path);

    private static void CheckLayout(SyntaxTree tree, string relative)
    {
        var root = tree.GetCompilationUnitRoot();
        var namespaces = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().ToArray();
        if (namespaces.Length != 1 || root.Members.Count != 1)
            throw new InvalidDataException(relative + ": use one file-scoped namespace.");
        var members = namespaces[0].Members;
        if (members.Count != 1 || members[0] is not BaseTypeDeclarationSyntax type)
            throw new InvalidDataException(relative + ": use one top-level type; keep owned helper types nested.");
        string expectedName = Path.GetFileName(relative).Replace(".xaml.cs", ".cs");
        if (expectedName != type.Identifier.ValueText + ".cs")
            throw new InvalidDataException(relative + ": filename must match " + type.Identifier.ValueText + ".");
        string folder = relative[..relative.LastIndexOf('/')].Replace('/', '.');
        if (namespaces[0].Name.ToString() != "PhoneControl." + folder)
            throw new InvalidDataException(relative + ": namespace must follow its folder.");
    }

    private static CSharpCompilation CheckLayer(string name, IEnumerable<SyntaxTree> trees, IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create("PhoneControl." + name, trees.Append(ImplicitImports), references, CompileOptions);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidDataException(name + " depends on unavailable outer code:\n" + string.Join("\n", errors.Select(d => d.ToString())));
        foreach (var tree in compilation.SyntaxTrees.Where(t => t != ImplicitImports)) {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes()) {
                var symbol = model.GetSymbolInfo(node).Symbol;
                string? type = symbol is INamedTypeSymbol named ? named.ToDisplayString() : symbol?.ContainingType?.ToDisplayString();
                if (type is not null && ForbiddenHostTypes.Contains(type))
                    throw new InvalidDataException(tree.FilePath + ": host operation " + type + " belongs behind an adapter port.");
            }
        }
        return compilation;
    }

    private static void SelfTest()
    {
        CheckLayout(Parse("namespace PhoneControl.Domain; public class Sample { private class Helper {} }"), "Domain/Sample.cs");
        CheckLayout(Parse("namespace PhoneControl.Composition; public partial class App {}"), "Composition/App.xaml.cs");
        ExpectRejected(() => CheckLayout(Parse("namespace PhoneControl.Domain; public class Other {}"), "Domain/Sample.cs"));
        ExpectRejected(() => CheckLayout(Parse("namespace PhoneControl.Domain; public class Sample {} public class Other {}"), "Domain/Sample.cs"));
        ExpectRejected(() => CheckLayout(Parse("namespace PhoneControl.Adapters; public class Sample {}"), "Domain/Sample.cs"));
        ExpectRejected(() => CheckLayer("Domain", [Parse("namespace PhoneControl.Domain; public class Sample { PhoneControl.Application.Input.Engine value; }")], References));
        ExpectRejected(() => CheckLayer("Domain", [Parse("namespace PhoneControl.Domain; public class Sample { public bool Exists() => global::System.IO.File.Exists(\"fixture\"); }")], References));
        ExpectRejected(() => CheckLayer("Application", [Parse("namespace PhoneControl.Application; public class Sample { PhoneControl.Adapters.Android.Session value; }")], References));
        Console.WriteLine("PASS architecture checker regression cases.");
    }

    private static void ExpectRejected(Action check)
    {
        try { check(); }
        catch (InvalidDataException) { return; }
        throw new InvalidDataException("Architecture checker accepted an invalid fixture.");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;

namespace ExecuteScript
{

    class Program
    {
        /* How to Execute: 
        > dotnet run <csharp program>

        Example
        > dotnet run "
        using System;

        namespace RoslynCompileSample
        {
            public class Writer
            {
                public static void Main(string[] message)
                {
                    Console.WriteLine($\"Shout it out {message[0]}! \");
                }
            }
        }
        "
        */


        static void Main(string[] args)
        {
            /* check if argument number is valid */
            if (args.Length <= 0)
            {
                throw new Exception("No Argument Supplied, Format: dotnet run <csharp program>");
            }
            string code = args[0];

            /*
                string code=@"
                using System;

                namespace SomeNameSpace
                {
                    public class SomeClass
                    {
                        public static void Main(string[] args)
                        {
                            Console.WriteLine($"" Shout it out {args[0]} "");
                        }
                    }
                }";
                */
            try
            {
                /* Parse the code into a CSharpSyntaxTree */
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
                string assemblyName = Path.GetRandomFileName();
                var refPaths = new[] {
                    typeof(System.Object).GetTypeInfo().Assembly.Location,
                    typeof(Console).GetTypeInfo().Assembly.Location,
                    Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "System.Runtime.dll")
                };
                MetadataReference[] references = refPaths.Select(r => MetadataReference.CreateFromFile(r)).ToArray();

                /* Create a CSharp compilation object from the CSharpSyntaxTree */
                CSharpCompilation compilation = CSharpCompilation.Create(
                    assemblyName,
                    syntaxTrees: new[] { syntaxTree },
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                /* Create a Semantic Model Object from the CSharpSyntaxTree */
                SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

                int index = 0;
                SyntaxNode mainMethodNode = null, mainClassNode = null;
                ISymbol mainClassSymbol = null;
                bool mainMethodFound = false;
                /*
                  iteration over subtrees of the syntax tree that are of type method declarations
                */
                foreach (var i in syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    index++;
                    //Console.WriteLine($"{index} {i.Identifier} {i.ToString()} \n");
                    /* Look for Main method, find its parent node*/
                    if (i.Identifier.ToString() == "Main")
                    {
                        mainMethodNode = i;
                        mainMethodFound = true;
                        mainClassNode = mainMethodNode.Parent;
                        mainClassSymbol = semanticModel.GetDeclaredSymbol(mainClassNode) as ITypeSymbol;
                    }
                }
                if (!mainMethodFound || (mainClassSymbol == null))
                {
                    throw new Exception("A class with main method could not be found");
                }

                //mainClassNode=syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Last();

                /* Retrieve name of the class that encloses Main methods*/
                string mainClassName = mainClassSymbol.ToString();
                //Console.WriteLine(mainClassName);
                using (var ms = new MemoryStream())
                {
                    EmitResult result = compilation.Emit(ms);
                    if (!result.Success)
                    {
                        string errString = "Compilation failed!";
                        IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                            diagnostic.IsWarningAsError ||
                            diagnostic.Severity == DiagnosticSeverity.Error);

                        foreach (Diagnostic diagnostic in failures)
                        {
                            errString += $" {diagnostic.Id}: {diagnostic.GetMessage()}";
                        }
                        throw new Exception(errString);
                    }
                    else
                    {
                        /* Invoke the main method */
                        ms.Seek(0, SeekOrigin.Begin);
                        Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                        var type = assembly.GetType(mainClassName);
                        var instance = assembly.CreateInstance(mainClassName);
                        var meth = type?.GetMember("Main").First() as MethodInfo;
                        //meth.Invoke(instance, new [] { new []{"mathew"}});
                        meth?.Invoke(instance, new[] { new[] { " Param " } });
                    }
                }

            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

        }
    }
}


// See https://aka.ms/new-console-template for more information

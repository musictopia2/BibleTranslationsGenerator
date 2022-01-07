namespace BibleTranslationsGenerator;
[Generator]
public class MySourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<SyntaxNode> declares = context.SyntaxProvider.CreateSyntaxProvider(
            (_, _) => false,
            (_, _) => GetTarget())
            .Where(m => m != null)!;
        IncrementalValueProvider<(Compilation, ImmutableArray<SyntaxNode>)> compilation
            = context.CompilationProvider.Combine(declares.Collect());
        context.RegisterSourceOutput(compilation, (spc, source) =>
        {
            Execute(source.Item1, spc);
        });
    }
    private SyntaxNode? GetTarget() //needs this.  otherwise, can't work unfortunately.
    {
        return null; //only so i can get information about the compilation.
    }
    private BasicList<ReferenceInfo> GetBibleReferences(Compilation compilation)
    {
        BasicList<ReferenceInfo> output = new();
        foreach (var item in compilation.ReferencedAssemblyNames)
        {
            string display = item.Name!;
            if (display.EndsWith("TranslationLibrary") && display.StartsWith("Bible"))
            {
                string translation = display.Replace("TranslationLibrary", "");
                translation = translation.Replace("Bible", "");
                output.Add(new(display, translation));
            }
        }
        return output;
    }
    private void Execute(Compilation compilation, SourceProductionContext context)
    {
        var list = GetBibleReferences(compilation);
        if (list.Count == 0)
        {
            return;
        }
        Process(list, context, compilation);
    }
    private void Process(BasicList<ReferenceInfo> list, SourceProductionContext context, Compilation compilation)
    {
        var name = compilation.AssemblyName!;
        WriteDataService(context, name, list);
        WriteExtension(context, name);
    }
    private void WriteDataService(SourceProductionContext context, string ns, BasicList<ReferenceInfo> references)
    {
        SourceCodeStringBuilder builder = new();
        builder.WriteLine(w =>
        {
            w.Write("namespace ")
               .Write(ns)
               .Write(";");
        })
        .WriteLine("public class BookDataService : global::BibleDatabaseLibrary.Services.IBookDataService")
        .WriteCodeBlock(w =>
        {
            w.WriteLine("async Task<global::BibleDatabaseLibrary.Entities.BookInformation> IBookDataService.GetBookInformationAsync(string name, string translationAbb)")
            .WriteCodeBlock(w =>
            {
                w.WriteLine("string text;")
                .WriteLine(w =>
                {
                    w.Write("string book = name.Replace(")
                    .AppendDoubleQuote(" ")
                    .Write(", ")
                    .AppendDoubleQuote()
                    .Write(");");
                })
                .WriteLine(w =>
                {
                    w.Write("text = $")
                    .AppendDoubleQuote("{translationAbb}{book}")
                    .Write(";");
                })
                .WriteLine("global::BibleDatabaseLibrary.JsonContextProcesses.GlobalJsonContextClass.AddJsonContexts();");
                WriteDataService(w, references);
                w.WriteLine(w =>
                {
                    w.CustomExceptionLine(w =>
                    {
                        w.Write("Book with name of {name} and translation abbreviation of {translationAbb} was not found");
                    });
                });
            });
        });
        context.AddSource("BookDataService.g", builder.ToString());
    }
    private void WriteDataService(ICodeBlock w, BasicList<ReferenceInfo> references)
    {
        foreach (var item in references)
        {
            w.WriteLine(w =>
            {
                w.Write("if (translationAbb == ")
                .AppendDoubleQuote(item.TranslationAbb)
                .Write(")");
            })
            .WriteCodeBlock(w =>
            {
                w.WriteLine(w =>
                {
                    w.Write("return await global::")
                    .Write(item.AssemblyName)
                    .Write(".Resources.SummaryClass.GetResourceAsync<global::BibleDatabaseLibrary.Entities.BookInformation>(text);");
                });
            });
        }
    }
    private void WriteExtension(SourceProductionContext context, string ns)
    {
        SourceCodeStringBuilder builder = new();
        builder.WriteLine("using Microsoft.Extensions.DependencyInjection;")
            .WriteLine(w =>
            {
                w.Write("namespace ")
                .Write(ns)
                .Write(";");
            })
            .WriteLine("public static partial class Extensions")
            .WriteCodeBlock(w =>
            {
                w.WriteLine("public static IServiceCollection RegisterBookDataServices<T>(this IServiceCollection services)")
                .WriteLine("where T : class, global::BibleDatabaseLibrary.Services.ITranslationService")
                .WriteCodeBlock(w =>
                {
                    w.WriteLine(w =>
                    {
                        w.Write("services.AddSingleton<global::BibleDatabaseLibrary.Services.IBookDataService, global::")
                        .Write(ns)
                        .Write(".BookDataService>();");
                    })
                    .WriteLine("services.AddScoped<global::BibleDatabaseLibrary.Services.ITranslationService, T>();")
                    .WriteLine("return services;");
                });
            });
        context.AddSource("Extensions.g", builder.ToString());
    }
}
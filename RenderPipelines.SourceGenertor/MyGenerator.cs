using Microsoft.CodeAnalysis;
using System;
using System.IO;

namespace RenderPipelines.SourceGenertor
{
    [Generator]
    internal class MyGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<AdditionalText> vxmlFiles = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".vxml"));
            context.RegisterSourceOutput(vxmlFiles, (a, file) =>
            {
                var xmlPassReader = new XMLPassReader();
                if (string.Equals(Path.GetExtension(file.Path), ".vxml", StringComparison.InvariantCultureIgnoreCase))
                {
                    var stream = new FileStream(file.Path, FileMode.Open);
                    xmlPassReader.ReadStream(stream);
                    stream.Dispose();
                }

                CodeGenerator1 codeGenerator1 = new CodeGenerator1();
                codeGenerator1.components = xmlPassReader.components;
                a.AddSource($"{Path.GetFileNameWithoutExtension(file.Path)}.g.cs", codeGenerator1.GenerateCode());
            });
        }
    }
}

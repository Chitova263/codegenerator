using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class EfCoreModelGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // No initialization required
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Find the dbconfig.json file
        var configFile = context.AdditionalFiles
            .FirstOrDefault(f => f.Path.EndsWith("dbconfig.json"));

        if (configFile == null)
            return;

        // Read and parse the configuration
        var configText = configFile.GetText(context.CancellationToken)?.ToString();
        if (string.IsNullOrEmpty(configText))
            return;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<DbConfig>(configText, options);
        if (config?.Entities == null || config.Entities.Count == 0)
            return;

        // Generate code for each entity
        foreach (var entity in config.Entities)
        {
            var source = GenerateEntityClass(entity);
            context.AddSource($"{entity.Name}.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        // Generate DbContext
        var dbContextSource = GenerateDbContext(config.Entities);
        context.AddSource("GeneratedDbContext.g.cs", SourceText.From(dbContextSource, Encoding.UTF8));
    }

    private string GenerateEntityClass(EntityConfig entity)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
        sb.AppendLine();
        sb.AppendLine("namespace GeneratedModels");
        sb.AppendLine("{");
        sb.AppendLine($"    [Table(\"{entity.TableName}\")]");
        sb.AppendLine($"    public partial class {entity.Name}");
        sb.AppendLine("    {");

        foreach (var prop in entity.Properties)
        {
            // Add attributes
            if (prop.IsPrimaryKey)
                sb.AppendLine("        [Key]");
            
            if (prop.IsRequired && prop.Type == "string")
                sb.AppendLine("        [Required]");
            
            if (prop.MaxLength.HasValue)
                sb.AppendLine($"        [MaxLength({prop.MaxLength})]");
            
            if (prop.Precision.HasValue && prop.Scale.HasValue)
                sb.AppendLine($"        [Column(TypeName = \"decimal({prop.Precision}, {prop.Scale})\")]");

            // Add property
            var nullability = prop.Type == "string" ? "?" : "";
            sb.AppendLine($"        public {prop.Type}{nullability} {prop.Name} {{ get; set; }}");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateDbContext(List<EntityConfig> entities)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine("namespace GeneratedModels");
        sb.AppendLine("{");
        sb.AppendLine("    public partial class GeneratedDbContext : DbContext");
        sb.AppendLine("    {");
        sb.AppendLine("        public GeneratedDbContext(DbContextOptions<GeneratedDbContext> options)");
        sb.AppendLine("            : base(options)");
        sb.AppendLine("        {");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var entity in entities)
        {
            sb.AppendLine($"        public DbSet<{entity.Name}> {entity.TableName} {{ get; set; }}");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

// Configuration classes
public class DbConfig
{
    public List<EntityConfig> Entities { get; set; } = new();
}

public class EntityConfig
{
    public string Name { get; set; }
    public string TableName { get; set; }
    public List<PropertyConfig> Properties { get; set; } = new();
}

public class PropertyConfig
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsRequired { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
}